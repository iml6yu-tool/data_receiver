# MQTT 数据采集实战：主题、报文格式与 iml6yu.DataReceive.Mqtt

> 关键词：MQTT、MQTTnet、数据采集、边缘网关、中控 InPlant、发布订阅、.NET

## 前言

Modbus、S7、OPC UA 都是"我去读设备"，MQTT 反过来："设备/网关把数据推给我"。这个模型差异带来两个直接变化：

- 没有轮询周期，`interval` 在采集侧意义不大（但仍用于缓存/事件节奏的判定）；
- 报文格式必须先约定，否则收到的 JSON 根本不知道哪个字段是点位。

`iml6yu.DataReceive.Mqtt` 把连接、订阅、报文解析、缓存、变化事件、发布写入都封装好了，并提供了两种开箱即用的报文格式：

| 包 | 报文格式 |
| --- | --- |
| `iml6yu.DataReceive.Mqtt.Default` | 本系统的 `DataReceiveContract`（`datas` 数组） |
| `iml6yu.DataReceive.Mqtt.InPlant` | 中控 MQTT 的 `RTValue` 数组 |

如果现场是自定义报文，用基础包 + 一个解析函数即可。

---

## 一、适用场景与业务环境

**典型场景**：

| 场景 | 说明 |
| --- | --- |
| 设备主动上报 | PLC/网关/仪表按变化或周期发布 JSON 到 Broker |
| 跨网段/跨厂区 | 通过 Broker 解耦，采集端不需要直达设备网段 |
| 弱网/边缘缓存 | 设备侧 QoS1 + 本地缓存，网络恢复后补发 |
| 多系统分发 | 一个采集端订阅后，转成统一结构再发布给 MES/看板 |

**业务环境要求**：

- 有一个可用的 MQTT Broker（EMQX、Mosquitto、HiveMQ、云 IoT 平台等）；
- 设备/网关侧已经按约定主题发布数据；
- 采集端与 Broker 网络可达（1883 或 8883/TLS）。

**不适合的场景**：设备不支持 MQTT、只能被轮询读取。这种情况请用 Modbus TCP/RTU 或 S7/OPC UA。

---

## 二、安装与最小接入

```bash
# 基础包（自定义报文解析）
dotnet add package iml6yu.DataReceive.Mqtt

# 本系统默认报文格式
dotnet add package iml6yu.DataReceive.Mqtt.Default

# 中控 InPlant 报文格式
dotnet add package iml6yu.DataReceive.Mqtt.InPlant
```

基础包的最小接入（自定义解析）：

```csharp
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.Mqtt;

var builder = Host.CreateApplicationBuilder(args);

DataReceiverMqttOption option =
    builder.Configuration.GetSection("DataReceiverOption").Get<DataReceiverMqttOption>();

// 第二参数是报文解析器：json -> { 地址 -> 值 }
builder.Services.AddMqttReceiver(option, ParsePayload, true, null);
builder.Services.AddHostedService<Worker>();

builder.Build().Run();

static Dictionary<string, ReceiverTempDataValue> ParsePayload(string json)
{
    // 这里换成实际的报文结构
    var payload = System.Text.Json.JsonSerializer.Deserialize<MyPayload>(json);
    if (payload?.Items == null) return null;

    return payload.Items.ToDictionary(
        t => t.Tag,
        t => new ReceiverTempDataValue(t.Value, DateTimeOffset.Now.ToUnixTimeMilliseconds()));
}

public class MyPayload
{
    public List<MyItem> Items { get; set; }
}

public class MyItem
{
    public string Tag { get; set; }
    public object Value { get; set; }
}
```

> `AddMqttReceiver` 与 `AddReceiver` 等价（后者是别名），都要求传入解析函数；使用 `Mqtt.Default` / `Mqtt.InPlant` 时不需要自己写解析。

---

## 三、连接配置

```json
{
  "DataReceiverOption": {
    "originHost": "192.168.1.30",
    "originPort": 1883,
    "originName": "collector",
    "originPwd": "123456",
    "connectTimeout": 10000,
    "reConnectPeriod": 10000,
    "reConnectTimes": 3,
    "receiverName": "L01_Mqtt",
    "productLineName": "L01",
    "nodeFile": "config/L01_nodes.json",
    "autoConnect": false,
    "dataInputTopics": [ "L01/device/+/data", "L01/line/#" ]
  }
}
```

| 配置 | 说明 |
| --- | --- |
| `originHost` / `originPort` | Broker 地址与端口（默认 1883；TLS 常用 8883） |
| `originName` / `originPwd` | 同时非空才启用账号密码 |
| `connectTimeout` | 连接超时（ms） |
| `dataInputTopics` | 订阅的主题列表，支持 MQTT 通配符 `+`、`#` |
| `receiverName` | 参与 ClientId 生成：`iml6yu.Receiver.{ReceiverName}.{时间戳}` |
| `productLineName` | 事件与通道的 Key |

**连接行为**：

- 启动时 `ConnectAsync` 建立连接，`ConnectedAsync` 事件里自动订阅 `dataInputTopics`；
- 断线后由 `StartWorkAsync` 的重连循环按 `reConnectPeriod` 重新连接（MQTTnet 自身未开启自动重连）；
- 停止时（token 取消）会先退订再断开。

---

## 四、点位配置（白名单 + 分组）

MQTT 的点位配置不是"读哪些寄存器"，而是**"认哪些地址、叫什么名字、归哪个组"**：

```json
[
  {
    "fullAddress": "PLC1.Speed",
    "address": "L01.Speed",
    "valueType": "Single",
    "groupName": "L01",
    "interval": 500,
    "descript": "报文里的 PLC1.Speed → 系统的 L01.Speed"
  },
  {
    "fullAddress": "PLC1.Temperature",
    "address": "L01.Temperature",
    "valueType": "Single",
    "groupName": "L01",
    "interval": 500
  },
  {
    "fullAddress": "PLC1.Running",
    "address": "L01.Running",
    "valueType": "Boolean",
    "groupName": "L01",
    "interval": 500
  }
]
```

规则：

| 情况 | 行为 |
| --- | --- |
| 已加载点位配置 | 只处理 `fullAddress` 命中的地址；未配置地址直接丢弃；`Address` 作为业务键进入缓存 |
| 未加载点位配置 | 报文里所有地址都接收，直接以报文字段名作为业务键 |
| `valueType` 不匹配 | 数据被丢弃，日志提示"数据类型不一致" |
| `groupName` | 只用于把一条报文拆成多批投递，**事件 Key 始终是 `productLineName`** |
| `interval` | 采集侧不轮询，仅保留字段兼容；实际节奏由设备发布频率决定 |

> 所以 MQTT 场景下 `address` 必须是"系统内统一业务键"，`fullAddress` 才是设备侧字段名；两者可以相同，也可以不同。

---

## 五、三种报文格式

### 5.1 Default（`iml6yu.DataReceive.Mqtt.Default`）

报文是本系统的 `DataReceiveContract`：

```json
{
  "id": 1732580000000,
  "key": "L01",
  "timestamp": 1732580000000,
  "datas": [
    { "address": "PLC1.Speed", "valueType": 13, "value": 12.5, "timestamp": 1732580000000 },
    { "address": "PLC1.Running", "valueType": 3, "value": true, "timestamp": 1732580000000 }
  ]
}
```

- `datas[].address` 对应点位配置的 `fullAddress`；
- `valueType` 是 `TypeCode` 整数，解析时以**点位配置的类型**为准；
- 同一报文里重复 `address` 只取第一条；
- `timestamp` 为毫秒时间戳，缺失时用报文级 `timestamp`。

### 5.2 InPlant（`iml6yu.DataReceive.Mqtt.InPlant`）

报文是中控风格的 `RTValue` 数组：

```json
{
  "RTValue": [
    { "Name": "PLC1.Speed", "Value": 12.5, "Timestamp": 1732580000 },
    { "Name": "PLC1.Running", "Value": true, "Timestamp": 1732580000 }
  ]
}
```

- `Name` 对应点位配置的 `fullAddress`；
- `Timestamp` 是**秒**级 Unix 时间戳（内部会 ×1000 转毫秒）；
- 同样只取第一次出现的 `Name`。

### 5.3 自定义

只要解析函数返回 `Dictionary<string, ReceiverTempDataValue>`，其余全部复用：

```csharp
static Dictionary<string, ReceiverTempDataValue> ParsePayload(string json)
{
    using var doc = JsonDocument.Parse(json);
    var root = doc.RootElement;

    var result = new Dictionary<string, ReceiverTempDataValue>();
    foreach (var item in root.GetProperty("data").EnumerateArray())
    {
        var tag = item.GetProperty("tag").GetString();
        var value = item.GetProperty("value").GetDouble();
        result[tag] = new ReceiverTempDataValue(value, DateTimeOffset.Now.ToUnixTimeMilliseconds());
    }
    return result;
}
```

- 返回 `null` 或空字典 → 本条报文丢弃；
- 解析抛异常 → 触发 `ErrorEvent`，本条报文丢弃，不影响下一条。

---

## 六、写入（发布）

MQTT 的"写"就是发布消息：

```csharp
// 方式一：直接发布任意对象到指定主题
await receiver.WriteAsync("L01/cmd/setSpeed", new { speed = 12.5 });

// 方式二：批量写入，DataWriteContract.Key 作为主题
await receiver.WriteAsync(new DataWriteContract
{
    Id = 1,
    Key = "L01/cmd/params",
    Datas =
    [
        new DataWriteContractItem { Address = "Speed", Value = 12.5, ValueType = TypeCode.Single },
        new DataWriteContractItem { Address = "Temperature", Value = 45f, ValueType = TypeCode.Single }
    ]
});
```

- 方式一发布的是 `data` 序列化后的 JSON；
- 方式二发布的是 `DataWriteContract` 序列化后的 JSON（`Key` 同时是主题）；
- `WriteOneAsync` 未实现，调用会返回 `NotImplemented`；
- 发布使用库内置默认 QoS（未开放配置）；需要 QoS1/2、Retained 消息时，建议直接使用 `receiver.Client`（`IMqttClient`）或继承扩展。

---

## 七、完整用例（Default 报文格式）

### 7.1 Program.cs

```csharp
using iml6yu.DataReceive.Mqtt.Configs;
using iml6yu.DataReceive.Mqtt.Default;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss.fff ");

DataReceiverMqttOption option =
    builder.Configuration.GetSection("DataReceiverOption").Get<DataReceiverMqttOption>();

// Mqtt.Default 没有单独的 AddReceiver 扩展，直接注册单例
builder.Services.AddSingleton(sp =>
    new DataReceiverMqttDefault(
        option,
        sp.GetRequiredService<ILogger<DataReceiverMqttDefault>>(),
        true,
        null));

builder.Services.AddHostedService<Worker>();

builder.Build().Run();
```

### 7.2 appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Warning"
    }
  },
  "DataReceiverOption": {
    "originHost": "192.168.1.30",
    "originPort": 1883,
    "originName": "collector",
    "originPwd": "123456",
    "connectTimeout": 10000,
    "reConnectPeriod": 10000,
    "reConnectTimes": 3,
    "receiverName": "L01_Mqtt",
    "productLineName": "L01",
    "nodeFile": "config/L01_nodes.json",
    "autoConnect": false,
    "dataInputTopics": [ "L01/line1/data" ]
  }
}
```

### 7.3 config/L01_nodes.json

```json
[
  {
    "fullAddress": "PLC1.Speed",
    "address": "L01.Speed",
    "valueType": "Single",
    "groupName": "L01",
    "interval": 500,
    "descript": "报文地址 PLC1.Speed"
  },
  {
    "fullAddress": "PLC1.Temperature",
    "address": "L01.Temperature",
    "valueType": "Single",
    "groupName": "L01",
    "interval": 500
  },
  {
    "fullAddress": "PLC1.Running",
    "address": "L01.Running",
    "valueType": "Boolean",
    "groupName": "L01",
    "interval": 500
  }
]
```

### 7.4 设备侧发布示例（任意 MQTT 客户端）

```json
{
  "id": 1732580000000,
  "key": "L01",
  "timestamp": 1732580000000,
  "datas": [
    { "address": "PLC1.Speed", "valueType": 13, "value": 12.5, "timestamp": 1732580000000 },
    { "address": "PLC1.Temperature", "valueType": 13, "value": 36.8, "timestamp": 1732580000000 },
    { "address": "PLC1.Running", "valueType": 3, "value": true, "timestamp": 1732580000000 }
  ]
}
```

### 7.5 Worker.cs

```csharp
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Mqtt.Default;

public class Worker : BackgroundService
{
    private readonly DataReceiverMqttDefault receiver;
    private readonly ILogger<Worker> logger;

    public Worker(DataReceiverMqttDefault receiver, ILogger<Worker> logger)
    {
        this.receiver = receiver;
        this.logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        receiver.ConnectionEvent += (_, e) =>
            logger.LogInformation("Broker 连接：{Connected} {Message}", e.IsConntion, e.Message);

        receiver.ErrorEvent += (_, e) => logger.LogError(e.Ex, "MQTT 错误：{Message}", e.Message);

        receiver.DataChangedEvent += (_, e) =>
            logger.LogInformation("变化 @{Time} [{Key}]：{Data}",
                DateTimeOffset.FromUnixTimeMilliseconds(e.Timestamp).ToString("HH:mm:ss.fff"),
                e.Key,
                string.Join(" | ", e.Datas.Select(d => $"{d.Address}={d.Value}")));

        // 每个收到并校验通过的批次都会触发
        receiver.DataIntervalEvent += (_, e) =>
        {
            // 不要在这里做耗时操作
        };

        _ = receiver.StartWorkAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await receiver.StopWorkAsync();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(5000, stoppingToken);

        // 读缓存（最近一次收到并校验通过的值）
        var all = await receiver.ReadAllAsync(stoppingToken);
        if (all.State)
            logger.LogInformation("缓存：{Data}",
                string.Join(",", all.Data.Select(d => $"{d.Address}={d.Value}")));

        // 发布指令（主题即 DataWriteContract.Key）
        var pub = await receiver.WriteAsync(new DataWriteContract
        {
            Id = 1,
            Key = "L01/line1/cmd",
            Datas =
            [
                new DataWriteContractItem
                {
                    Address = "SetSpeed", Value = 12.5f, ValueType = TypeCode.Single
                }
            ]
        });
        logger.LogInformation("发布指令：{State} {Message}", pub.State, pub.Message);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(15000, stoppingToken);
            logger.LogInformation("在线={Connected} 状态={State}", receiver.IsConnected, receiver.State);
        }
    }
}
```

---

## 八、注意事项（MQTT 对接清单）

1. **推送模型，不是轮询**：`StartWorkAsync` 只负责连接/保活/取消时断开；数据节奏由设备发布频率决定，配置里的 `interval` 不产生轮询。
2. **点位配置是白名单**：一旦加载了点位文件，未配置的地址全部丢弃；排查"收不到数据"时先看 `fullAddress` 是否和报文一致。
3. **`fullAddress` 与 `address` 别混**：前者是报文里的地址，后者是系统业务键；事件、缓存、订阅都用 `address`。
4. **事件 Key 是 `productLineName`**：`groupName` 只用于分批投递；如果业务需要按产线区分，请使用多个采集器实例或 `RefLineNames`。
5. **`dataInputTopics` 支持通配符**：`+` 匹配单层、`#` 匹配多层；但订阅层级越宽，报文量越大，解析与通道压力越高。
6. **ClientId 自动生成**：`iml6yu.Receiver.{receiverName}.{时间戳}`；如果 Broker 要求固定 ClientId 或需要持久会话（clean session = false），需要自行扩展。
7. **QoS 未开放配置**：默认订阅/发布行为由库内 MQTTnet 选项决定；需要 QoS1/2、Retained 时直接使用 `receiver.Client` 或继承 `DataReceiverMqtt`。
8. **默认报文格式的 `valueType` 是整数**：`TypeCode` 数值（`3`=Boolean、`13`=Single 等），但最终以点位配置的类型为准，报文里的类型只作参考。
9. **InPlant 的时间戳是秒**：内部会 ×1000；如果设备侧发的是毫秒，事件时间会变成"未来时间"。
10. **解析失败整条丢弃**：解析函数返回 `null`/空字典或抛异常，本条报文不会进入缓存，异常会走 `ErrorEvent`。
11. **重复地址只取第一条**：同一报文里相同 `address` 后续值会被忽略；如果设备会分片发布，请让每个分片地址唯一。
12. **写入方式**：`WriteAsync<T>(topic, data)` 或 `WriteAsync(DataWriteContract)`（`Key` 即主题）；`WriteOneAsync` 不支持。
13. **断线重连**：依赖 `StartWorkAsync` 的重连循环，`reConnectPeriod` 建议 5~10 秒；重连成功后 `ConnectedAsync` 会自动重新订阅。
14. **通道容量 5000**：事件处理过慢会丢最旧数据并打印预警；高频报文场景请把业务处理移到独立队列。
15. **Broker ACL**：账号需要同时具备订阅 `dataInputTopics` 和发布指令主题的权限，否则表现为"连上了但没数据/发不出去"。
16. **TLS 场景**：当前 `ConnectAsync` 使用 TCP + 账号密码，未开放证书配置；需要 TLS 时请扩展 `DataReceiverMqtt.ConnectAsync`。

---

## 九、三种宿主环境：WinForms / 控制台 / Windows 服务

### 9.1 WinForms

MQTT 调试器适合做"报文观察器"：连 Broker、看主题数据、手动发布指令。项目文件与 Modbus TCP 篇一致，包换成 `iml6yu.DataReceive.Mqtt` + `iml6yu.DataReceive.Mqtt.Default`。

**Program.cs**

```csharp
using iml6yu.DataReceive.Mqtt.Configs;
using iml6yu.DataReceive.Mqtt.Default;

internal static class Program
{
    public static IServiceProvider Services { get; private set; } = default!;

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        var option = builder.Configuration
            .GetSection("DataReceiverOption")
            .Get<DataReceiverMqttOption>()!;

        // Mqtt.Default 没有独立扩展方法，直接注册单例
        builder.Services.AddSingleton(sp => new DataReceiverMqttDefault(
            option,
            sp.GetRequiredService<ILogger<DataReceiverMqttDefault>>(),
            isAutoLoadNodeConfig: true,
            nodes: null));

        builder.Services.AddSingleton<MainForm>();

        using var host = builder.Build();
        Services = host.Services;
        host.Start();

        Application.Run(Services.GetRequiredService<MainForm>());

        host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    }
}
```

**MainForm.cs**

```csharp
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Mqtt.Default;

public sealed class MainForm : Form
{
    private readonly DataReceiverMqttDefault receiver;
    private readonly CancellationTokenSource cts = new();
    private readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly FlowLayoutPanel bar = new() { Dock = DockStyle.Top, Height = 40 };
    private readonly TextBox publisher = new() { Dock = DockStyle.Bottom, Height = 32, Text = "L01/line1/cmd" };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private readonly object gate = new();
    private string[] snapshot = [];

    public MainForm(DataReceiverMqttDefault receiver)
    {
        this.receiver = receiver;

        Text = "MQTT 采集调试器";
        Width = 900;
        Height = 520;

        var start = new Button { Text = "订阅", Width = 90 };
        var stop = new Button { Text = "停止", Width = 90, Enabled = false };
        var publish = new Button { Text = "发布测试指令", Width = 130 };
        bar.Controls.AddRange([start, stop, publish]);

        Controls.Add(list);
        Controls.Add(publisher);
        Controls.Add(bar);

        start.Click += (_, _) =>
        {
            _ = receiver.StartWorkAsync(cts.Token);
            start.Enabled = false;
            stop.Enabled = true;
            timer.Start();
        };

        stop.Click += async (_, _) =>
        {
            timer.Stop();
            cts.Cancel();
            await receiver.StopWorkAsync();
            start.Enabled = true;
            stop.Enabled = false;
        };

        publish.Click += async (_, _) =>
        {
            var result = await receiver.WriteAsync(publisher.Text, new { Speed = 12.5, At = DateTime.Now });
            MessageBox.Show($"发布结果：{result.State}\r\n{result.Message}");
        };

        timer.Tick += (_, _) => RefreshList();

        receiver.DataIntervalEvent += (_, e) =>
        {
            lock (gate)
                snapshot = e.Datas.Select(d => $"{d.Address} = {d.Value}").ToArray();
        };

        receiver.ErrorEvent += (_, e) => BeginInvoke(() => MessageBox.Show($"MQTT 异常：{e.Message}"));
    }

    private void RefreshList()
    {
        lock (gate)
        {
            list.BeginUpdate();
            list.Items.Clear();
            list.Items.AddRange(snapshot);
            list.EndUpdate();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        timer.Stop();
        cts.Cancel();
        receiver.StopWorkAsync().GetAwaiter().GetResult();
        cts.Dispose();
        base.OnFormClosing(e);
    }
}
```

**WinForms 要点**

- 报文是推送的，界面只做"快照 + 定时刷新"，不要在事件里直接刷新控件。
- 发布主题放在输入框里方便现场改；发布内容按业务约定构造。
- Broker 断线时 `ConnectionEvent` 会触发，界面提示即可，重连由类库负责。

### 9.2 控制台

```csharp
using iml6yu.DataReceive.Mqtt.Configs;
using iml6yu.DataReceive.Mqtt.Default;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(b => b
    .AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss.fff ")
    .SetMinimumLevel(LogLevel.Information));

var option = new DataReceiverMqttOption
{
    OriginHost = "192.168.1.30",
    OriginPort = 1883,
    OriginName = "collector",
    OriginPwd = "123456",
    ReceiverName = "L01_Mqtt",
    ProductLineName = "L01",
    NodeFile = Path.Combine(AppContext.BaseDirectory, "config", "L01_nodes.json"),
    DataInputTopics = ["L01/line1/data"]
};

using var receiver = new DataReceiverMqttDefault(
    option, loggerFactory.CreateLogger<DataReceiverMqttDefault>(), isAutoLoadNodeConfig: true);

receiver.ConnectionEvent += (_, e) => Console.WriteLine($"Broker：{e.IsConntion} {e.Message}");
receiver.ErrorEvent += (_, e) => Console.WriteLine($"错误：{e.Message}");
receiver.DataChangedEvent += (_, e) =>
    Console.WriteLine($"变化：{string.Join(",", e.Datas.Select(d => $"{d.Address}={d.Value}"))}");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

_ = receiver.StartWorkAsync(cts.Token);

try { await Task.Delay(Timeout.Infinite, cts.Token); }
catch (OperationCanceledException) { }

await receiver.StopWorkAsync();
Console.WriteLine("已停止");
```

> 如果使用自定义报文，把 `DataReceiverMqttDefault` 换成 `DataReceiverMqtt` + `SetDataParse(Parse)`（或用 `AddMqttReceiver` 注入）。

### 9.3 Windows 服务

```csharp
using iml6yu.DataReceive.Mqtt.Configs;
using iml6yu.DataReceive.Mqtt.Default;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(o => o.ServiceName = "L01.MqttCollector");
builder.Logging.AddEventLog(s =>
{
    s.SourceName = "L01.MqttCollector";
    s.LogName = "Application";
});

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

var option = builder.Configuration
    .GetSection("DataReceiverOption")
    .Get<DataReceiverMqttOption>()!;
option.NodeFile = Path.Combine(AppContext.BaseDirectory, option.NodeFile);

builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));

builder.Services.AddSingleton(sp => new DataReceiverMqttDefault(
    option,
    sp.GetRequiredService<ILogger<DataReceiverMqttDefault>>(),
    isAutoLoadNodeConfig: true,
    nodes: null));

builder.Services.AddHostedService<Worker>();     // Worker 复用第七节示例

builder.Build().Run();
```

**MQTT 服务化要点**

- 服务没有控制台，日志必须走 EventLog/文件。
- Broker 连接依赖网络与 ACL；服务账户变化后，Broker 侧按账号/IP 的白名单要同步调整。
- 主题和点位文件都随程序发布，用 `AppContext.BaseDirectory` 拼路径。
- 高频报文场景给 `HostOptions.ShutdownTimeout` 留足时间，避免停止时丢失未处理消息。

---

## 十、扩展自己的逻辑：继承与重写

MQTT 提供了三种扩展粒度，按需要选择：

| 粒度 | 方式 | 适用 |
| --- | --- | --- |
| 只换报文格式 | `SetDataParse(func)` / `AddMqttReceiver(option, func, ...)` | 报文结构固定、只做字段映射 |
| 换报文格式 + 改发布 | 继承 `DataReceiverMqtt`，重写 `JsonDataPrase` / `WriteAsync<T>` | 需要统一信封、签名、加时间戳 |
| 改路由/连接行为 | 继承 `DataReceiverMqtt`，重写 `MessageReceivedAsync` / `ConnectAsync` | 按主题分流、TLS、固定 ClientId |

```csharp
using System.Text.Json;
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.Mqtt;
using iml6yu.DataReceive.Mqtt.Configs;
using iml6yu.Result;

public sealed class DataReceiverMqttCustom : DataReceiverMqtt
{
    public DataReceiverMqttCustom(DataReceiverMqttOption option,
        ILogger<DataReceiverMqttCustom> logger,
        bool isAutoLoadNodeConfig = false,
        List<NodeItem> nodes = null)
        : base(option, logger, isAutoLoadNodeConfig, nodes) { }

    // 1) 自定义报文：{"ts": 毫秒, "values": { "TAG": 1.23 }}
    protected override Dictionary<string, ReceiverTempDataValue> JsonDataPrase(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var ts = doc.RootElement.GetProperty("ts").GetInt64();

            var result = new Dictionary<string, ReceiverTempDataValue>();
            foreach (var property in doc.RootElement.GetProperty("values").EnumerateObject())
            {
                object value = property.Value.ValueKind switch
                {
                    JsonValueKind.Number => property.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => property.Value.GetString()
                };
                result[property.Name] = new ReceiverTempDataValue(value, ts);
            }
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "自定义报文解析失败");
            return null;
        }
    }

    // 2) 统一发布信封：加时间戳和来源
    public override async Task<MessageResult> WriteAsync<T>(string topicname, T data)
    {
        var envelope = new
        {
            ts = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            source = Option.ReceiverName,
            payload = data
        };
        return await base.WriteAsync(topicname, envelope);
    }
}
```

**扩展注意事项**

- `JsonDataPrase` 是 `protected virtual`，返回 `null` 或空字典表示"丢弃本条"；异常请自己 catch，否则会冒泡到 MQTTnet 回调。
- 如需按主题做不同解析，重写 `MessageReceivedAsync`（`protected virtual`），根据 `arg.ApplicationMessage.Topic` 分流；不要忘记调用 `ReceiveDataToMessageChannelAsync` 投递。
- `ConnectAsync` 是 `public override`（仍可继续重写）：TLS、遗嘱消息、固定 ClientId、CleanSession 都在这里改。
- `WriteBatchAsync` 是 `protected override`，可以在这里统一把 `DataWriteContract` 包一层业务信封再发布。
- 基类构造时会调用 `CreateClient`（MQTTnet 客户端），派生类字段此时未初始化。

---

## 十一、小结

MQTT 采集的核心不是"读"，而是"约定"：约定主题、约定报文、约定地址到业务键的映射。把这三件事写进配置，剩下的连接、订阅、解析、缓存、变化事件、发布都可以交给类库。

至此五种协议全部讲完：Modbus TCP / RTU 解决"寄存器世界"，S7 解决"DB 块世界"，OPC UA 解决"标准化世界"，MQTT 解决"推送世界"。它们的共同点，是同一套 `NodeItem` 配置、同一套 `Read/Write/Event` API —— 这也是 `iml6yu.DataReceive` 最想提供的价值。


---

## 交流与反馈

|Wechat| QQ邮箱|Gmail（不常用）|
|----|----|----|
|![WeChat](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAFoAAABYCAYAAAB1YOAJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsQAAA7EAZUrDhsAACLISURBVHhe7d0HtC1FES7gMYs5oKiYEROYc84RTJgDRhRUxICKmDChqIgBVAQB9aogBkSCWTGjoogiiFkwA+Zr1v3qqzO1T++5s/c5Fy5vrfeW/1qzZk93z0xPT3VV/VU955xrEuj+h3Mc6wz0P/7xj07Ruc51rr5kMbS1bbTRRn3J/3/429/+1p373Ofuj1bGf//733XHw0AXnva0pxn0s7Q961nP6q8yjje+8Y2T+93vfpN//etfeXz66adPHvCAB0ze+973TnbdddfJk570pCyHn/70p5N73OMek6OPProvmUxe+cpX5vmLcNBBB+V5f/rTn/qS+fjnP/+Zbd/znvdMnvvc5472/7TTTptc4AIXGH3elbZNNtlkcuaZZ/ZXWpLGRLy10RPWZ1uEm93sZtmmbv7tb387jw3eZS5zmZnzP/GJT+Rx+/Cbb775ive4733vm21++MMf9iXz8Zvf/CbbPuhBD5pc/OIXn1zoQhfqa5ZxdgTP9rrXva6/0mRy3ihIxO/+V9e97GUv6x7xiEd0f//73/uScUTnure85S3dnnvu2ZfMx4te9KLuy1/+cnepS10qj6973et2xx9/fHf1q1+9O+SQQ7q1a9dmOdzpTnfqjjvuuKwr7L777t13v/vd7o9//GN35JFHdve85z27S1/60ln3yU9+srvwhS/c7bzzzt01r3nN7vKXv3x38MEHd7e97W27E044obvc5S6Xz/erX/2qu8997pNq8bKXvWz3zGc+s3vwgx/c3ete98r6U045JdvHTOvOd77zTdXFxhtv3H3+85/PNu04DaH9n//85+6ud71r7v/zn//0NYE4MfHXv/51+ia++c1v9qUrIzowPW8Rtthii2xDZQyxmvOvcIUrZJvXvOY1uX/rW9+a5VSR4wte8IKTu93tbvmbJNk//vGPz/01rnGNybWuda38/fvf/z7Pi0HP43vf+96T85///JNLXvKSk3gxWfbjH/842zz96U/P41vf+tZ5vFpc5zrXyfNe+9rX9iWNRLcoSf75z3/e7bXXXl08RL5JkmD/73//O6UhHn6u1DOqod/SMHjTe+yxR/fV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)|[646007589@qq.com](mailto:646007589@qq.com)|[iml6yu@gmail.com](mailto:iml6yu@gmail.com)|
