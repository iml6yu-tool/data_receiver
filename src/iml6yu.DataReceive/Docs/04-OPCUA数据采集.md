# OPC UA 数据采集实战：NodeId、会话安全与 iml6yu.DataReceive.OPCUA

> 关键词：OPC UA、OPC UA Client、NodeId、ns=2、数据采集、MES、DCS、.NET

## 前言

OPC UA 是这几年工业互联的"通用语言"：跨厂商、类型自描述、自带安全模型。但真正写采集代码时，问题往往集中在几个很具体的地方：

- 服务地址到底是 `opc.tcp://ip:4840` 还是 `ip` + `port` 分开配？
- `NodeId` 里的 `ns=2;s=xxx` 和 `ns=2;i=1234` 有什么区别？命名空间索引会不会变？
- 匿名能连上，加账号密码反而连不上？
- 点位几百上千个，一次 `Read` 会不会被服务端拒绝？

本文基于 `iml6yu.DataReceive.OPCUA`（底层使用 OPC Foundation 官方 `OPCFoundation.NetStandard.Opc.Ua.Client`），把地址、连接、安全、批量读取和完整用例讲清楚。

---

## 一、适用场景与业务环境

**典型服务端**：KEPServerEX、UaGateway、西门子 S7-1500 内置 OPC UA、中控 DCS、汇川/台达网关、各类边缘网关、自研 OPC UA Server。

**典型业务环境**：

| 场景 | 说明 |
| --- | --- |
| 跨厂商集成 | 现场设备来自多个品牌，统一通过 OPC UA 暴露数据 |
| MES / SCADA 对接 | 上层系统只认 OPC UA，需要在边缘侧汇聚后转发 |
| 已有 UA Server | 现场已经部署 KEPServer 等，直接订阅/轮询其节点 |
| 标准化信息模型 | 需要读取节点的数据类型、工程单位等元信息 |

**和 Modbus / S7 的区别**：OPC UA 的点位是"节点"，类型由服务端定义，客户端不需要猜端序；代价是会话、证书、批量读取限制这些"IT 侧"的问题更多。

---

## 二、安装与最小接入

```bash
dotnet add package iml6yu.DataReceive.OPCUA
```

```csharp
using iml6yu.DataReceive.OPCUA;
using iml6yu.DataReceive.OPCUA.Configs;

var builder = Host.CreateApplicationBuilder(args);

DataReceiverOPCUAOption option =
    builder.Configuration.GetSection("DataReceiverOption").Get<DataReceiverOPCUAOption>();

builder.Services.AddReceiver(option, true, null);
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
```

---

## 三、连接配置

```json
{
  "DataReceiverOption": {
    "originHost": "opc.tcp://192.168.1.20:4840",
    "originName": "",
    "originPwd": "",
    "reConnectPeriod": 10000,
    "reConnectTimes": 3,
    "connectTimeout": 60000,
    "receiverName": "OPC01",
    "productLineName": "L01",
    "nodeFile": "config/OPC01_nodes.json",
    "autoConnect": false
  }
}
```

| 配置 | 说明 |
| --- | --- |
| `originHost` | **推荐直接写完整 URL**：`opc.tcp://ip:4840` |
| `originPort` | 可选；如果填了端口，内部会拼成 `"{originHost}:{originPort}"` |
| `originName` / `originPwd` | 同时非空时使用用户名/密码身份，否则匿名 |
| `connectTimeout` | 端点发现与连接超时（ms） |
| `reConnectPeriod` | 断线重连间隔（ms） |

> **不要同时写带端口的 URL 和 `originPort`**，否则会拼出 `opc.tcp://ip:4840:4840` 这种非法地址。

**安全相关（当前实现内置策略）**：

- `UseSecurity = false`：使用 None 安全策略；
- `AutoAcceptUntrustedCertificates = true`：自动接受服务端证书；
- 客户端证书保存在 `CurrentUser\My`；
- 会话超时默认 60 秒。

> 这套默认值适合内网、调试、快速接入。生产环境如果要求签名/加密，需要基于 `OpcUaClient` 扩展安全策略与证书信任链，不能直接用于公网。

---

## 四、点位配置（NodeId）

### 4.1 地址格式

`fullAddress` 就是 OPC UA 的 `NodeId` 字符串，`address` 是业务键：

```json
{
  "fullAddress": "ns=2;s=Demo.Line1.Speed",
  "address": "L01.Speed",
  "valueType": "Single",
  "groupName": "L01",
  "interval": 500,
  "descript": "1 号线速度"
}
```

常见 `NodeId` 写法：

| 写法 | 说明 |
| --- | --- |
| `ns=2;s=Demo.Tag` | 字符串标识符，最常用，**推荐** |
| `ns=2;i=1234` | 数字标识符，由服务端分配 |
| `ns=2;g=...` | GUID 标识符 |
| `ns=2;b=...` | 不透明字节串 |

> `ns=2` 里的 2 是**命名空间索引**，不是命名空间 URI。索引由服务端启动时分配，服务端重装/重配后可能变化；能用 `s=` 字符串标识就尽量用字符串，至少业务上更稳定、可读。

### 4.2 点位加载校验

`LoadConfig` 会对每个 `fullAddress` 执行 `NodeId.Parse`：

- 格式非法（如 `invalid node id`）→ 加载配置返回失败，日志有记录；
- 格式合法但服务端不存在 → 加载能成功，运行时读取返回 null/质量码异常，该点位数据被丢弃并记录告警。

### 4.3 分组与批量读取

- 按 `groupName` 分组，组内按 `interval` 分组；
- 每个组合一个循环，一次 `Read` 请求读取该组全部节点；
- 组与组、间隔与间隔之间并行执行；
- 一次请求的节点数受服务端 `MaxNodesPerRead` 限制（常见 1000，部分服务端更小），点位多时请拆 `groupName`；
- 时间戳使用**本地接收时间**，不是服务端的 SourceTimestamp。

### 4.4 ValueType 匹配

读取结果会按配置的 `valueType` 做类型校验/转换，支持 `Boolean / Int16 / UInt16 / Int32 / UInt32 / Int64 / UInt64 / Single / Double / DateTime / String` 等常见 `TypeCode`。类型不匹配会返回失败或丢弃数据，日志中有明确提示。

> 配置前先用 UaExpert 之类的工具确认节点的实际数据类型，避免把 `Int32` 配成 `Double`。

### 4.5 写入

- `WriteAsync<T>(address, value)`：写单个节点；
- `WriteAsync(DataWriteContractItem)`：写单个节点；
- `WriteAsync(DataWriteContract)`：批量写，先写普通点位，再写 `IsFlag` 点位；
- `WriteWithVerifyAsync`：OPC UA 实现**不会额外做回读比对**，需要强一致时请写入后自行 `DirectReadAsync` 校验。

写入值需要与服务端节点数据类型一致，类型不符时服务端返回 `BadTypeMismatch`，接口返回失败。

---

## 五、完整用例

### 5.1 Program.cs

```csharp
using iml6yu.DataReceive.OPCUA;
using iml6yu.DataReceive.OPCUA.Configs;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss.fff ");

DataReceiverOPCUAOption option =
    builder.Configuration.GetSection("DataReceiverOption").Get<DataReceiverOPCUAOption>();

builder.Services.AddReceiver(option, true, null);
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
```

### 5.2 appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Warning"
    }
  },
  "DataReceiverOption": {
    "originHost": "opc.tcp://192.168.1.20:4840",
    "originName": "",
    "originPwd": "",
    "reConnectPeriod": 10000,
    "reConnectTimes": 3,
    "connectTimeout": 60000,
    "receiverName": "OPC01",
    "productLineName": "L01",
    "nodeFile": "config/OPC01_nodes.json",
    "autoConnect": false
  }
}
```

### 5.3 config/OPC01_nodes.json

```json
[
  {
    "fullAddress": "ns=2;s=Demo.Line1.Running",
    "address": "L01.Running",
    "valueType": "Boolean",
    "groupName": "L01",
    "interval": 500,
    "descript": "运行状态"
  },
  {
    "fullAddress": "ns=2;s=Demo.Line1.Speed",
    "address": "L01.Speed",
    "valueType": "Single",
    "groupName": "L01",
    "interval": 500,
    "descript": "线速度"
  },
  {
    "fullAddress": "ns=2;s=Demo.Line1.Temperature",
    "address": "L01.Temperature",
    "valueType": "Double",
    "groupName": "L01",
    "interval": 500,
    "descript": "温度"
  },
  {
    "fullAddress": "ns=2;s=Demo.Line1.TotalCount",
    "address": "L01.TotalCount",
    "valueType": "Int64",
    "groupName": "L01",
    "interval": 1000,
    "descript": "累计产量"
  },
  {
    "fullAddress": "ns=2;s=Demo.Line1.ProductCode",
    "address": "L01.ProductCode",
    "valueType": "String",
    "groupName": "L01",
    "interval": 1000,
    "descript": "产品编码"
  },
  {
    "fullAddress": "ns=2;s=Demo.Line1.SetSpeed",
    "address": "L01.SetSpeed",
    "valueType": "Single",
    "groupName": "L01",
    "interval": 1000,
    "descript": "速度设定（可写）"
  },
  {
    "fullAddress": "ns=2;s=Demo.Line1.WriteFlag",
    "address": "L01.WriteFlag",
    "valueType": "Boolean",
    "groupName": "L01",
    "interval": 1000,
    "descript": "写入标志（可写）"
  }
]
```

### 5.4 Worker.cs

```csharp
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.OPCUA;

public class Worker : BackgroundService
{
    private readonly DataReceiverOPCUA receiver;
    private readonly ILogger<Worker> logger;

    public Worker(DataReceiverOPCUA receiver, ILogger<Worker> logger)
    {
        this.receiver = receiver;
        this.logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        receiver.ConnectionEvent += (_, e) =>
            logger.LogInformation("OPC UA 连接：{Connected} {Message}", e.IsConntion, e.Message);

        receiver.ErrorEvent += (_, e) => logger.LogError(e.Ex, "OPC UA 异常：{Message}", e.Message);

        receiver.DataChangedEvent += (_, e) =>
            logger.LogInformation("变化 @{Time}：{Data}",
                DateTimeOffset.FromUnixTimeMilliseconds(e.Timestamp).ToString("HH:mm:ss.fff"),
                string.Join(" | ", e.Datas.Select(d => $"{d.Address}={d.Value}")));

        receiver.Subscribe("看板", ["L01.Speed", "L01.Temperature"]);
        receiver.DataSubscribeEvent += (key, e) =>
            logger.LogInformation("订阅[{Key}]：{Data}", key,
                string.Join(",", e.Datas.Select(d => $"{d.Address}={d.Value}")));

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

        var all = await receiver.ReadAllAsync(stoppingToken);
        if (all.State)
            logger.LogInformation("缓存点位：{Count}", all.Data.Count);

        // 主动读取（绕过缓存，直接访问服务端）
        var direct = await receiver.DirectReadAsync(
        [
            new DataReadItem { Address = "ns=2;s=Demo.Line1.Speed", ValueType = TypeCode.Single },
            new DataReadItem { Address = "ns=2;s=Demo.Line1.Temperature", ValueType = TypeCode.Double }
        ], stoppingToken);
        if (direct.State)
            logger.LogInformation("主动读取：{Data}",
                string.Join(",", direct.Data.Datas.Select(d => $"{d.Address}={d.Value}")));

        // 单点写
        var one = await receiver.WriteAsync("ns=2;s=Demo.Line1.SetSpeed", 12.5f);
        logger.LogInformation("写设定速度：{State} {Message}", one.State, one.Message);

        // 批量写：先写参数，再写标志位
        var batch = await receiver.WriteAsync(new DataWriteContract
        {
            Id = 100,
            Key = "L01",
            Datas =
            [
                new DataWriteContractItem
                {
                    Address = "ns=2;s=Demo.Line1.SetSpeed", Value = 12.5f, ValueType = TypeCode.Single
                },
                new DataWriteContractItem
                {
                    Address = "ns=2;s=Demo.Line1.WriteFlag", Value = true, ValueType = TypeCode.Boolean, IsFlag = true
                }
            ]
        });
        logger.LogInformation("批量写入：{State} {Message}", batch.State, batch.Message);

        // 需要强一致时：写后主动读回比对
        var readBack = await receiver.DirectReadAsync(
        [
            new DataReadItem { Address = "ns=2;s=Demo.Line1.SetSpeed", ValueType = TypeCode.Single }
        ], stoppingToken);
        if (readBack.State)
            logger.LogInformation("写后回读：{Value}", readBack.Data.Datas.FirstOrDefault()?.Value);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(15000, stoppingToken);
            logger.LogInformation("在线={Connected} 状态={State}", receiver.IsConnected, receiver.State);
        }
    }
}
```

---

## 六、注意事项（对接清单）

1. **地址写完整 URL**：`opc.tcp://ip:4840`；不要同时设置 `originPort`，否则会拼接出非法地址。
2. **优先用字符串 NodeId**：`ns=2;s=xxx` 比 `ns=2;i=1234` 更稳定、可读；命名空间索引可能随服务端配置变化。
3. **端口与防火墙**：4840 是默认端口，但现场经常改成 4841/4850；服务端与采集端双向放行。
4. **匿名 / 账号密码**：部分服务端禁用匿名，需要配置 `originName` + `originPwd`；两者必须同时非空才生效。
5. **安全策略默认是 None**：只能用于内网；需要签名/加密时要扩展 `OpcUaClient` 并处理证书信任。
6. **证书自动接受**：默认 `AutoAcceptUntrustedCertificates = true`，生产环境建议改为人工信任，避免中间人风险。
7. **批量读取有上限**：受服务端 `MaxNodesPerRead` 限制，点位多时按 `groupName` 拆分；单组节点数建议控制在 500 以内。
8. **不检查质量码**：服务端返回 `Bad` 质量时值为 null，库会按类型校验失败丢弃并记日志；请关注日志中的"类型不一致"告警。
9. **轮询而非订阅**：当前实现是定时批量 `Read`，不是 `Subscription/MonitoredItem`；对变化频率高、点位少的场景，轮询间隔可以设小一些（200~500ms）。
10. **严格写入不回读**：`WriteWithVerifyAsync` 在 OPC UA 下等同上批量写；强一致场景请自行 `DirectReadAsync` 校验。
11. **会话超时**：默认 60 秒；网络抖动时服务端可能先断开会话，重连间隔建议 5~10 秒。
12. **时间戳语义**：事件里的 `Timestamp` 是本地接收时间；需要服务端时间戳时请直接使用底层 `Client.ReadNodesAsync` 返回的 `DataValue.SourceTimestamp`。
13. **同一个 `address` 全局唯一**：缓存以业务键为索引，重复会加载失败。
14. **节点类型先用 UaExpert 验证**：把 `Int32` 配成 `Double`、把枚举配成 `Int32` 都会导致数据被丢弃。

---

## 七、三种宿主环境：WinForms / 控制台 / Windows 服务

### 7.1 WinForms

OPC UA 的调试器和 WinForms 很搭：连服务端、浏览节点、看质量码。项目文件与 Modbus TCP 篇一致，包换成 `iml6yu.DataReceive.OPCUA`。

**Program.cs**

```csharp
using iml6yu.DataReceive.OPCUA;
using iml6yu.DataReceive.OPCUA.Configs;

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
            .Get<DataReceiverOPCUAOption>()!;

        builder.Services.AddReceiver(option, true, null);
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
using iml6yu.DataReceive.OPCUA;

public sealed class MainForm : Form
{
    private readonly DataReceiverOPCUA receiver;
    private readonly CancellationTokenSource cts = new();
    private readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly FlowLayoutPanel bar = new() { Dock = DockStyle.Top, Height = 40 };
    private readonly Label stateLabel = new() { Dock = DockStyle.Bottom, Height = 24, Text = "未连接" };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private readonly object gate = new();
    private string[] snapshot = [];

    public MainForm(DataReceiverOPCUA receiver)
    {
        this.receiver = receiver;

        Text = "OPC UA 采集调试器";
        Width = 900;
        Height = 520;

        var start = new Button { Text = "开始", Width = 90 };
        var stop = new Button { Text = "停止", Width = 90, Enabled = false };
        var write = new Button { Text = "写 SetSpeed=12.5", Width = 160 };
        bar.Controls.AddRange([start, stop, write]);

        Controls.Add(list);
        Controls.Add(stateLabel);
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

        write.Click += async (_, _) =>
        {
            var result = await receiver.WriteAsync("ns=2;s=Demo.Line1.SetSpeed", 12.5f);
            MessageBox.Show($"写入结果：{result.State}\r\n{result.Message}");
        };

        timer.Tick += (_, _) => RefreshUi();

        receiver.DataIntervalEvent += (_, e) =>
        {
            lock (gate)
                snapshot = e.Datas.Select(d => $"{d.Address} = {d.Value}").ToArray();
        };

        receiver.ConnectionEvent += (_, e) => BeginInvoke(() => stateLabel.Text = e.IsConntion ? "已连接" : $"未连接：{e.Message}");
        receiver.ErrorEvent += (_, e) => BeginInvoke(() => MessageBox.Show($"OPC UA 异常：{e.Message}"));
    }

    private void RefreshUi()
    {
        lock (gate)
        {
            list.BeginUpdate();
            list.Items.Clear();
            list.Items.AddRange(snapshot);
            list.EndUpdate();
        }

        stateLabel.Text = receiver.IsConnected ? "已连接" : "连接已断开（自动重连中）";
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

**WinForms 的证书提示**

- 客户端证书生成在**当前用户**的 `CurrentUser\My` 证书库；用哪个 Windows 账户跑调试器，证书就在哪个账户下。
- 如果服务端要求信任客户端证书，先在调试器账户下完成一次连接并信任，再做服务化（服务账户不同，证书不同，需要重新信任）。

### 7.2 控制台

```csharp
using iml6yu.DataReceive.OPCUA;
using iml6yu.DataReceive.OPCUA.Configs;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(b => b
    .AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss.fff ")
    .SetMinimumLevel(LogLevel.Information));

var option = new DataReceiverOPCUAOption
{
    OriginHost = "opc.tcp://192.168.1.20:4840",   // 完整 URL，不要再配 OriginPort
    ReceiverName = "OPC01",
    ProductLineName = "L01",
    NodeFile = Path.Combine(AppContext.BaseDirectory, "config", "OPC01_nodes.json")
};

using var receiver = new DataReceiverOPCUA(
    option, loggerFactory.CreateLogger<DataReceiverOPCUA>(), isAutoLoadNodeConfig: true);

receiver.ConnectionEvent += (_, e) => Console.WriteLine($"连接：{e.IsConntion} {e.Message}");
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

### 7.3 Windows 服务

```csharp
using iml6yu.DataReceive.OPCUA;
using iml6yu.DataReceive.OPCUA.Configs;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(o => o.ServiceName = "L01.OpcUaCollector");
builder.Logging.AddEventLog(s =>
{
    s.SourceName = "L01.OpcUaCollector";
    s.LogName = "Application";
});

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

var option = builder.Configuration
    .GetSection("DataReceiverOption")
    .Get<DataReceiverOPCUAOption>()!;
option.NodeFile = Path.Combine(AppContext.BaseDirectory, option.NodeFile);

builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));

builder.Services.AddReceiver(option, true, null);
builder.Services.AddHostedService<Worker>();     // Worker 复用第五节示例

builder.Build().Run();
```

**OPC UA 服务化要点**

1. **证书账户变了**：服务默认以 `LocalSystem` 运行，证书会生成在 SYSTEM 的 `CurrentUser\My`。之前用个人账户信任过的证书不生效，需要重新信任（或在服务端配置为不校验）。
2. **服务账户要能出网**：DPAPI/证书存储、防火墙规则按服务账户生效；端口 4840 双向放行。
3. **不要用交互式账户跑服务**：注销后会话结束，依赖用户会话的证书/映射盘会失效。
4. **日志走 EventLog/文件**：服务里看不到控制台输出。
5. **停止超时**：会话来不急关闭时，Host 会强制结束；`ShutdownTimeout` 建议 30 秒。

---

## 八、扩展自己的逻辑：继承与重写

| 需求 | 做法 |
| --- | --- |
| 开启安全策略/调整会话超时 | 重写 `CreateClient`，设置 `OpcUaClient` 属性 |
| 写入后强制回读校验 | 重写 `WriteStrictlyAsync` |
| 动态点位/点位过滤 | 重写 `LoadConfig` |
| 自定义批量读取（按节点大小分组） | 重写 `WhileDoAsync`（`readNodes` 为 `protected`） |
| 数据旁路 | 重写 `ReceiveDataToMessageChannelAsync` |

示例：开启安全 + 强一致写入。

```csharp
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.OPCUA;
using iml6yu.DataReceive.OPCUA.Configs;
using iml6yu.Result;

public sealed class DataReceiverOPCUACustom : DataReceiverOPCUA
{
    public DataReceiverOPCUACustom(DataReceiverOPCUAOption option,
        ILogger<DataReceiverOPCUACustom> logger,
        bool isAutoLoadNodeConfig = false,
        List<NodeItem> nodes = null)
        : base(option, logger, isAutoLoadNodeConfig, nodes) { }

    // 1) 调整客户端安全与会话参数
    protected override OpcUaClient CreateClient(DataReceiverOPCUAOption option)
    {
        var client = base.CreateClient(option);
        client.UseSecurity = true;
        client.SessionTimeout = 120000;
        return client;
    }

    // 2) 写入 → 回读 → 比对 → 写标志位
    protected override async Task<MessageResult> WriteStrictlyAsync(DataWriteContract data)
    {
        if (!IsConnected)
            return MessageResult.Failed(ResultType.DeviceWriteError, "OPC UA 未连接");

        var values = data.Datas.Where(t => !t.IsFlag).ToList();

        foreach (var item in values)
        {
            var write = await WriteOneAsync(item);
            if (!write.State) return write;
        }

        var read = await DirectReadAsync(values.Select(t => (DataReadItem)t).ToArray());
        if (!read.State)
            return MessageResult.Failed(ResultType.DeviceWriteError, "写后回读失败，请人工确认", null);

        foreach (var actual in read.Data.Datas)
        {
            var expected = values.First(t => t.Address == actual.Address);
            if (!VerifyValueEqual(actual.Value, expected.Value, (int)expected.ValueType))
                return MessageResult.Failed(ResultType.DeviceWriteError,
                    $"{actual.Address} 写入校验失败，当前值 {actual.Value}，预期 {expected.Value}");
        }

        foreach (var flag in data.Datas.Where(t => t.IsFlag))
        {
            var write = await WriteOneAsync(flag);
            if (!write.State) return write;
        }

        return MessageResult.Success();
    }
}
```

**扩展注意事项**

- `CreateClient` 在基类构造函数里被调用，派生类字段尚未初始化，别在这里依赖派生类状态。
- `OpcUaClient` 是 `sealed`，但 `UseSecurity`、`SessionTimeout`、`UserIdentity`、`OpcUaName` 都可以在 `CreateClient` 里设置。
- 需要自定义证书信任逻辑时，可以在 `CreateClient` 之后直接操作 `Client.Session`，或自行实现 `OpcUaClient` 的替代实现。
- `DirectReadAsync` 是 `abstract`，如果自定义了 NodeId 映射，记得同步重写它，否则直读与轮询的地址语义会不一致。

---

## 九、小结

OPC UA 把"协议细节"藏起来了，代价是把"IT 细节"暴露出来：URL、证书、会话、批量上限。把这些配置化、把类型校验交给类库、把强一致写入用回读补上，OPC UA 采集就只剩下运维层面的证书和网络问题了。

下一篇是 MQTT：它不是"读设备"，而是"等设备推"，整个编程模型和前面三种协议都不一样。


---

## 交流与反馈

|Wechat| QQ邮箱|Gmail（不常用）|
|----|----|----|
|![WeChat](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAFoAAABYCAYAAAB1YOAJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsQAAA7EAZUrDhsAACLISURBVHhe7d0HtC1FES7gMYs5oKiYEROYc84RTJgDRhRUxICKmDChqIgBVAQB9aogBkSCWTGjoogiiFkwA+Zr1v3qqzO1T++5s/c5Fy5vrfeW/1qzZk93z0xPT3VV/VU955xrEuj+h3Mc6wz0P/7xj07Ruc51rr5kMbS1bbTRRn3J/3/429/+1p373Ofuj1bGf//733XHw0AXnva0pxn0s7Q961nP6q8yjje+8Y2T+93vfpN//etfeXz66adPHvCAB0ze+973TnbdddfJk570pCyHn/70p5N73OMek6OPProvmUxe+cpX5vmLcNBBB+V5f/rTn/qS+fjnP/+Zbd/znvdMnvvc5472/7TTTptc4AIXGH3elbZNNtlkcuaZZ/ZXWpLGRLy10RPWZ1uEm93sZtmmbv7tb387jw3eZS5zmZnzP/GJT+Rx+/Cbb775ive4733vm21++MMf9iXz8Zvf/CbbPuhBD5pc/OIXn1zoQhfqa5ZxdgTP9rrXva6/0mRy3ihIxO/+V9e97GUv6x7xiEd0f//73/uScUTnure85S3dnnvu2ZfMx4te9KLuy1/+cnepS10qj6973et2xx9/fHf1q1+9O+SQQ7q1a9dmOdzpTnfqjjvuuKwr7L777t13v/vd7o9//GN35JFHdve85z27S1/60ln3yU9+srvwhS/c7bzzzt01r3nN7vKXv3x38MEHd7e97W27E044obvc5S6Xz/erX/2qu8997pNq8bKXvWz3zGc+s3vwgx/c3ete98r6U045JdvHTOvOd77zTdXFxhtv3H3+85/PNu04DaH9n//85+6ud71r7v/zn//0NYE4MfHXv/51+ia++c1v9qUrIzowPW8Rtthii2xDZQyxmvOvcIUrZJvXvOY1uX/rW9+a5VSR4wte8IKTu93tbvmbJNk//vGPz/01rnGNybWuda38/fvf/z7Pi0HP43vf+96T85///JNLXvKSk3gxWfbjH/842zz96U/P41vf+tZ5vFpc5zrXyfNe+9rX9iWNRLcoSf75z3/e7bXXXl08RL5JkmD/73//O6UhHn6u1DOqod/SMHjTe+yxR/fV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)|[646007589@qq.com](mailto:646007589@qq.com)|[iml6yu@gmail.com](mailto:iml6yu@gmail.com)|
