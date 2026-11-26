# Modbus RTU 数据采集实战：串口参数、单循环调度与 485 现场避坑

> 关键词：Modbus RTU、RS485、串口采集、NModbus.Serial、波特率、校验位、.NET、数据采集

## 前言

如果说 Modbus TCP 的坑在"字节序"，那 Modbus RTU 的坑就在"物理层和调度"：

- 串口参数（波特率/数据位/校验位/停止位）差一位，报文全是乱码；
- 485 总线是半双工，**不能并发**，多线程读取只会互相踩报文；
- 一条总线上挂多个站号，轮询节奏怎么定？
- USB 转 485 的 COM 号会漂移，服务重启就变了。

本文基于 `iml6yu.DataReceive.ModbusMasterRTU`，讲清楚串口参数配置、地址模型（与 TCP 完全一致）、单循环调度策略，以及一份可直接跑的完整用例。

---

## 一、适用场景与业务环境

**典型设备**：老式变频器、温控表、称重仪表、流量计、电表、PLC（通过 485 扩展口）、扫码器等。

**典型业务环境**：

| 场景 | 说明 |
| --- | --- |
| 设备只有 485/232 | 现场没有以太网口，只能用串口或 USB 转 485 |
| 一台工控机带多条总线 | 每路 485 一个 `DataReceiverModbusRTU` 实例，互不影响 |
| 成本敏感型改造 | 不增加网关，直接串口采集 |
| 短距离、强干扰 | 屏蔽双绞线 + 单点接地 + 终端电阻 |

**什么时候不要用 RTU**：现场已经有串口服务器（RS485 转以太网）时，优先用 `ModbusMasterTCP`，让 TCP 层负责连接管理，串口服务器负责物理层；RTU 版本适合"工控机直接带 485 口"或"USB 转 485"的场景。

---

## 二、安装与最小接入

```bash
dotnet add package iml6yu.DataReceive.ModbusMasterRTU
```

```csharp
using iml6yu.DataReceive.ModbusMasterRTU;
using iml6yu.DataReceive.ModbusMasterRTU.Configs;

var builder = Host.CreateApplicationBuilder(args);

DataReceiverModbusRTUOption option =
    builder.Configuration.GetSection("DataReceiverOption").Get<DataReceiverModbusRTUOption>();

builder.Services.AddReceiver(option, true, null);
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
```

> `DataReceiverModbusRTU` 在构造时就会 `new SerialPort(...)` 并 `Open()`。串口号不存在、被占用、参数非法，都会在 **DI 解析阶段**直接抛异常，表现为服务启动失败。这是有意为之：串口都打不开，采集没有意义。

---

## 三、串口与连接配置

```json
{
  "DataReceiverOption": {
    "comPort": "COM3",
    "baudRate": 9600,
    "parity": 0,
    "dataBits": 8,
    "stopBit": 1,
    "originHost": null,
    "originPort": 0,
    "reConnectPeriod": 10000,
    "reConnectTimes": 3,
    "connectTimeout": 60000,
    "receiverName": "L01_ModbusRTU",
    "productLineName": "L01",
    "nodeFile": "config/L01_nodes.json",
    "autoConnect": false
  }
}
```

| 配置 | 默认值 | 说明 |
| --- | --- | --- |
| `comPort` | 无 | 串口号，如 `COM3`、`/dev/ttyUSB0` |
| `baudRate` | 9600 | 波特率，必须与设备一致 |
| `parity` | 0（None） | 校验位：`0=None`、`1=Odd`、`2=Even`、`3=Mark`、`4=Space` |
| `dataBits` | 8 | 数据位，常用 8；部分老设备是 7 |
| `stopBit` | 1（One） | 停止位：`0=None`、`1=One`、`2=Two`、`3=OnePointFive` |
| `reConnectPeriod` | 30000 | 断线检测与重连间隔（ms） |
| `connectTimeout` | 60000 | 连接超时（ms） |

> JSON 里 `parity` / `stopBit` 可以写数字（推荐，和 `Parity` / `StopBits` 枚举值一致），也可以写枚举名，配置绑定两种都支持。

**最常见的三个参数问题**：

1. 设备是 `8N1`，程序写成 `8E1` → 全部超时；
2. 设备是 `9600`，程序写 `19200` → 全是乱码或无响应；
3. USB 转 485 线质量差 / 未接终端电阻 → 偶发 CRC 错误，距离越长越明显。

---

## 四、点位配置与地址类型

### 4.1 地址格式（与 TCP 完全相同）

```text
slaveAddress.ReadType.Bit
```

- `slaveAddress`：485 总线上的站号，`0~255`，同一总线不能重复；
- `ReadType`：读取类型，见 4.2；
- `Bit`：起始地址，**0 基**（手册 `40001` → `0`）。

```text
1.HoldingRegisters.0
1.HoldingRegisters2.10
2.ReadInputRegistersFloat.100
3.Coils.5
```

### 4.2 读取类型（完整）

**1 位**

| ReadType | 说明 |
| --- | --- |
| `Coils` | 线圈，可读可写（继电器、启停） |
| `Inputs` | 离散输入，只读（到位、限位） |

**16 位（1 个寄存器）**

| ReadType | 说明 |
| --- | --- |
| `HoldingRegisters` | 保持寄存器，可读写 |
| `ReadInputRegisters` | 输入寄存器，只读 |

**32 位（2 个寄存器）**

| 保持寄存器类型 | 输入寄存器类型 | 布局 |
| --- | --- | --- |
| `HoldingRegisters2` | `ReadInputRegisters2` | ABCD（大端，高字在前） |
| `HoldingRegisters2ByteSwap` | `ReadInputRegisters2ByteSwap` | BADC（高字在前，字内交换） |
| `HoldingRegistersLittleEndian2` | `ReadInputRegistersLittleEndian2` | DCBA（低字在前，字内小端） |
| `HoldingRegistersLittleEndian2ByteSwap` | `ReadInputRegistersLittleEndian2ByteSwap` | CDAB（低字在前，字内大端） |
| `HoldingRegistersFloat` | `ReadInputRegistersFloat` | ABCD（float） |
| `HoldingRegistersFloatByteSwap` | `ReadInputRegistersFloatByteSwap` | BADC（float） |
| `HoldingRegistersFloatLittleEndian` | `ReadInputRegistersFloatLittleEndian` | DCBA（float） |
| `HoldingRegistersFloatLittleEndianByteSwap` | `ReadInputRegistersFloatLittleEndianByteSwap` | CDAB（float） |

**64 位（4 个寄存器）**

| 保持寄存器类型 | 输入寄存器类型 | 布局 |
| --- | --- | --- |
| `HoldingRegisters4` | `ReadInputRegisters4` | ABCD EFGH（大端） |
| `HoldingRegisters4ByteSwap` | `ReadInputRegisters4ByteSwap` | BADC FEHG |
| `HoldingRegistersDouble` | `ReadInputRegistersDouble` | ABCD EFGH（double） |
| `HoldingRegistersDoubleByteSwap` | `ReadInputRegistersDoubleByteSwap` | BADC FEHG（double） |
| `HoldingRegistersLittleEndian4` | `ReadInputRegistersLittleEndian4` | HGFE DCBA（完整小端） |
| `HoldingRegistersLittleEndian4ByteSwap` | `ReadInputRegistersLittleEndian4ByteSwap` | FEHG CDAB |
| `HoldingRegistersDoubleLittleEndian` | `ReadInputRegistersDoubleLittleEndian` | HGFE DCBA（double，完整小端） |
| `HoldingRegistersDoubleLittleEndianByteSwap` | `ReadInputRegistersDoubleLittleEndianByteSwap` | FEHG CDAB（double） |

### 4.3 ValueType 匹配规则

| 读取类型族 | 寄存器数 | 可配 `valueType` |
| --- | --- | --- |
| `Coils` / `Inputs` | 1 bit | `Boolean` |
| `HoldingRegisters` / `ReadInputRegisters` | 1 | `Int16` / `UInt16` |
| 32 位 `*2` / `*Float` | 2 | `Int32` / `UInt32` / `Single` |
| 64 位 `*4` / `*Double` | 4 | `Int64` / `UInt64` / `Double` |

32 位点位之间地址间隔 2，64 位间隔 4；地址重叠的节点会在加载配置时被判定为错误并跳过。

### 4.4 RTU 的调度策略（与 TCP 最大的区别）

串行总线不能并发，所以 RTU 版本是**单循环顺序执行**：

```text
while (未取消)
{
    foreach (分组)
        foreach (间隔)
            foreach (站号)
                foreach (读取项)  → 串口发一帧、收一帧
    等待 delayTime
}
```

其中 `delayTime` = **所有点位中最小且大于 0 的 `interval`**，没有配置时默认 500ms。

这意味着：

- `interval` 在 RTU 里不是"每个分组的独立节奏"，而是用来算全局最小等待时间；
- 一轮轮询本身要花时间（帧间隔、设备响应），实际周期 ≈ 一轮耗时 + `delayTime`；
- 想要"快速点位高频、慢速点位低频"，需要拆成多个 `DataReceiverModbusRTU`（不同串口/不同总线）或接受统一节奏。

### 4.5 写入规则

与 TCP 一致：

- `Coils`：传 `bool`，走单线圈写；
- `HoldingRegisters`：传 `ushort` 走单寄存器写，其它数值按端序转多寄存器；
- `HoldingRegisters*Float*`：必须传 `float`；
- `HoldingRegisters*Double*`：必须传 `double`；
- `Inputs`、`ReadInputRegisters*` 只读，写入直接返回错误。

---

## 五、完整用例

### 5.1 Program.cs

```csharp
using iml6yu.DataReceive.ModbusMasterRTU;
using iml6yu.DataReceive.ModbusMasterRTU.Configs;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss.fff ");

DataReceiverModbusRTUOption option =
    builder.Configuration.GetSection("DataReceiverOption").Get<DataReceiverModbusRTUOption>();

// 串口在构造时打开，端口占用/不存在会在这里抛异常
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
    "comPort": "COM3",
    "baudRate": 9600,
    "parity": 0,
    "dataBits": 8,
    "stopBit": 1,
    "reConnectPeriod": 10000,
    "reConnectTimes": 3,
    "connectTimeout": 60000,
    "receiverName": "L01_ModbusRTU",
    "productLineName": "L01",
    "nodeFile": "config/L01_nodes.json",
    "autoConnect": false
  }
}
```

### 5.3 config/L01_nodes.json

```json
[
  {
    "fullAddress": "1.Coils.0",
    "address": "L01.Running",
    "valueType": "Boolean",
    "groupName": "L01",
    "interval": 500,
    "descript": "运行状态"
  },
  {
    "fullAddress": "1.HoldingRegisters.0",
    "address": "L01.SetSpeed",
    "valueType": "UInt16",
    "groupName": "L01",
    "interval": 500,
    "descript": "设定速度（可写）"
  },
  {
    "fullAddress": "1.HoldingRegisters2.2",
    "address": "L01.ActualSpeed",
    "valueType": "Int32",
    "groupName": "L01",
    "interval": 500,
    "descript": "实际速度（32 位，占用 2、3）"
  },
  {
    "fullAddress": "1.HoldingRegistersFloat.4",
    "address": "L01.Temperature",
    "valueType": "Single",
    "groupName": "L01",
    "interval": 500,
    "descript": "温度（float，占用 4、5）"
  },
  {
    "fullAddress": "1.HoldingRegisters4.6",
    "address": "L01.TotalCount",
    "valueType": "Int64",
    "groupName": "L01",
    "interval": 1000,
    "descript": "累计产量（64 位，占用 6、7、8、9）"
  },
  {
    "fullAddress": "2.ReadInputRegisters.0",
    "address": "L01.Weight",
    "valueType": "UInt16",
    "groupName": "L01",
    "interval": 500,
    "descript": "2 号站重量"
  }
]
```

### 5.4 Worker.cs

```csharp
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.ModbusMasterRTU;

public class Worker : BackgroundService
{
    private readonly DataReceiverModbusRTU receiver;
    private readonly ILogger<Worker> logger;

    public Worker(DataReceiverModbusRTU receiver, ILogger<Worker> logger)
    {
        this.receiver = receiver;
        this.logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        receiver.ConnectionEvent += (_, e) =>
            logger.LogInformation("串口连接：{Connected} {Message}", e.IsConntion, e.Message);

        receiver.ErrorEvent += (_, e) =>
            logger.LogError(e.Ex, "串口异常：{Message}", e.Message);

        receiver.DataChangedEvent += (_, e) =>
            logger.LogInformation("变化 @{Time}：{Data}",
                DateTimeOffset.FromUnixTimeMilliseconds(e.Timestamp).ToString("HH:mm:ss.fff"),
                string.Join(" | ", e.Datas.Select(d => $"{d.Address}={d.Value}")));

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
        await Task.Delay(3000, stoppingToken);

        var all = await receiver.ReadAllAsync(stoppingToken);
        if (all.State)
            logger.LogInformation("当前缓存：{Data}",
                string.Join(",", all.Data.Select(d => $"{d.Address}={d.Value}")));

        // 写设定速度（单寄存器）
        var write = await receiver.WriteAsync("1.HoldingRegisters.0", (ushort)1200);
        logger.LogInformation("写设定速度：{State} {Message}", write.State, write.Message);

        // 批量写 + 校验 + 标志位
        var strict = await receiver.WriteWithVerifyAsync(new DataWriteContract
        {
            Id = 2,
            Key = "L01",
            Datas =
            [
                new DataWriteContractItem
                {
                    Address = "1.HoldingRegisters.0", Value = (ushort)1200, ValueType = TypeCode.UInt16
                },
                new DataWriteContractItem
                {
                    Address = "1.Coils.10", Value = true, ValueType = TypeCode.Boolean, IsFlag = true
                }
            ]
        });
        logger.LogInformation("严格写入：{State} {Message}", strict.State, strict.Message);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(15000, stoppingToken);
            logger.LogInformation("在线={Connected} 状态={State}", receiver.IsConnected, receiver.State);
        }
    }
}
```

---

## 六、注意事项（485 现场清单）

1. **串口参数必须逐项对齐**：波特率、数据位、校验位、停止位，任何一项不一致都表现为超时或 CRC 错误。先用手册参数，再用串口调试助手验证。
2. **一条总线只能有一个"主站实例"**：不要用两个 `DataReceiverModbusRTU` 同时打开同一个 `COM` 口，会互相抢报文、抛"端口被占用"。
3. **RTU 是单循环**：`interval` 只参与计算最小等待时间，所有点位按同一节奏轮询；一轮耗时超过最短间隔时，实际周期会被拉长。
4. **485 是半双工**：不要指望并发；如果点位多、实时性要求高，拆成多条总线（多串口/多 USB 转 485）。
5. **站号不能重复**：同一总线两个设备相同站号，会随机读到错误数据或超时。
6. **必须接终端电阻**：长距离（>50m）或高波特率时，总线两端各 120Ω，屏蔽层单点接地，否则偶发 CRC 错误。
7. **USB 转 485 的 COM 号会漂移**：换 USB 口后可能变成 `COM4`。生产环境建议在设备管理器里固定端口号，或把 `comPort` 做成可配置项。
8. **地址仍要 0 基、按寄存器数量留间隔**：与 TCP 完全一致；重叠点位会被跳过。
9. **单次读取数量上限**：寄存器 125、线圈 2000；超过会被从站拒绝（异常码 03）。
10. **单个点位读失败不会中断整轮**：库内部对每次读取做了 `try/catch` 并记录日志，坏点不会导致整轮停止，但会持续刷错误日志，建议监控 `ErrorEvent`/日志关键字。
11. **`count` 对 Modbus 无效**：长度由 `ReadType` 决定。
12. **停止有延迟**：取消后循环最多再等一个 `delayTime` 才退出，属正常现象。
13. **写入前确认从站允许写**：部分仪表对保持寄存器有写保护，需要先在设备面板解锁。
14. **串口服务器场景**：如果现场是"485 转以太网"，请改用 `ModbusMasterTCP`，通过 `slaveAddress` 路由到具体设备，不要用 RTU 版本去打开虚拟串口（稳定性差很多）。

---

## 七、三种宿主环境：WinForms / 控制台 / Windows 服务

### 7.1 WinForms

WinForms 适合做"现场调试器"：选串口、看数据、手动写参数。项目文件与 Modbus TCP 篇一致，只需把包换成 `iml6yu.DataReceive.ModbusMasterRTU`。

**Program.cs**

```csharp
using iml6yu.DataReceive.ModbusMasterRTU;
using iml6yu.DataReceive.ModbusMasterRTU.Configs;

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
            .Get<DataReceiverModbusRTUOption>()!;

        try
        {
            // 注意：串口在构造时打开，COM 被占用会在这里抛异常
            builder.Services.AddReceiver(option, true, null);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"串口打开失败：{ex.Message}", "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

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
using iml6yu.DataReceive.ModbusMasterRTU;

public sealed class MainForm : Form
{
    private readonly DataReceiverModbusRTU receiver;
    private readonly CancellationTokenSource cts = new();
    private readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly FlowLayoutPanel bar = new() { Dock = DockStyle.Top, Height = 40 };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private readonly object gate = new();
    private string[] snapshot = [];

    public MainForm(DataReceiverModbusRTU receiver)
    {
        this.receiver = receiver;

        Text = "Modbus RTU 采集调试器";
        Width = 900;
        Height = 500;

        var start = new Button { Text = "开始", Width = 90 };
        var stop = new Button { Text = "停止", Width = 90, Enabled = false };
        var write = new Button { Text = "写设定速度 1200", Width = 160 };
        bar.Controls.AddRange([start, stop, write]);

        Controls.Add(list);
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
            var result = await receiver.WriteAsync("1.HoldingRegisters.0", (ushort)1200);
            MessageBox.Show($"写入结果：{result.State}\r\n{result.Message}");
        };

        timer.Tick += (_, _) => RefreshList();

        receiver.DataIntervalEvent += (_, e) =>
        {
            lock (gate)
                snapshot = e.Datas.Select(d => $"{d.Address} = {d.Value}").ToArray();
        };

        receiver.ErrorEvent += (_, e) =>
            BeginInvoke(() => MessageBox.Show($"串口异常：{e.Message}"));
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

**RTU 在 WinForms 里的额外注意点**

- 调试器本身会占用 COM 口，串口调试助手和上位机不能同时打开同一个口。
- 提供"刷新串口列表"时用 `SerialPort.GetPortNames()`，但不要在采集运行中切换端口，必须先停止再重建实例。
- 串口异常（拔线、USB 松动）会走 `ErrorEvent`，界面提示即可，不要退出程序。

### 7.2 控制台

```csharp
using System.IO.Ports;
using iml6yu.DataReceive.ModbusMasterRTU;
using iml6yu.DataReceive.ModbusMasterRTU.Configs;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(b => b
    .AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss.fff ")
    .SetMinimumLevel(LogLevel.Information));

var option = new DataReceiverModbusRTUOption
{
    ComPort = "COM3",
    BaudRate = 9600,
    Parity = Parity.None,
    DataBits = 8,
    StopBit = StopBits.One,
    ReceiverName = "L01_ModbusRTU",
    ProductLineName = "L01",
    NodeFile = Path.Combine(AppContext.BaseDirectory, "config", "L01_nodes.json")
};

DataReceiverModbusRTU receiver;
try
{
    receiver = new DataReceiverModbusRTU(
        option, loggerFactory.CreateLogger<DataReceiverModbusRTU>(), isAutoLoadNodeConfig: true);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"串口打开失败：{ex.Message}");
    return;
}

using (receiver)
{
    receiver.DataChangedEvent += (_, e) =>
        Console.WriteLine($"变化：{string.Join(",", e.Datas.Select(d => $"{d.Address}={d.Value}"))}");
    receiver.ErrorEvent += (_, e) => Console.WriteLine($"错误：{e.Message}");

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    _ = receiver.StartWorkAsync(cts.Token);

    try { await Task.Delay(Timeout.Infinite, cts.Token); }
    catch (OperationCanceledException) { }

    await receiver.StopWorkAsync();
    Console.WriteLine("已停止");
}
```

### 7.3 Windows 服务

服务化和 TCP 完全一样，差异只在"串口权限"和"COM 号稳定性"。

```csharp
using iml6yu.DataReceive.ModbusMasterRTU;
using iml6yu.DataReceive.ModbusMasterRTU.Configs;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(o => o.ServiceName = "L01.ModbusRTUCollector");
builder.Logging.AddEventLog(s =>
{
    s.SourceName = "L01.ModbusRTUCollector";
    s.LogName = "Application";
});

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

var option = builder.Configuration
    .GetSection("DataReceiverOption")
    .Get<DataReceiverModbusRTUOption>()!;
option.NodeFile = Path.Combine(AppContext.BaseDirectory, option.NodeFile);

builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));

builder.Services.AddReceiver(option, true, null);
builder.Services.AddHostedService<Worker>();     // Worker 复用第五节的实现

builder.Build().Run();
```

**RTU 服务化要点**

1. **COM 号必须固定**：在设备管理器里给 USB 转 485 指定固定端口号，否则重启/换口后 `COM3` 变 `COM5`，服务起不来。
2. **USB 驱动要全机安装**：CH340/CP210x/FTDI 驱动装成"所有用户"，不要只在当前用户会话安装。
3. **服务账户**：默认 `LocalSystem` 可以访问串口；若改用域账户，需要把账户加入 `Users` 组并确认对 COM 设备有访问权限。
4. **不要在服务里做交互式操作**：消息框、托盘图标都不会显示。
5. **发布时带上配置**：`appsettings.json`、`config\**\*.json` 一起发布；`System.IO.Ports` 由 RTU 包带入，无需额外引用。
6. **停止时释放串口**：`StopAsync` 里 `await receiver.StopWorkAsync()`，Host 停止完后串口才真正释放。

安装命令同 TCP 篇，把服务名换成 `L01.ModbusRTUCollector` 即可。

---

## 八、扩展自己的逻辑：继承与重写

RTU 最典型的扩展需求是"**写入时暂停读取**"，避免串口半双工冲突；其次是动态点位与自定义调度。

| 需求 | 做法 |
| --- | --- |
| 写入期间暂停读取 | 用 `SemaphoreSlim` 做总线锁，重写 `WhileDoAsync` 与 `WriteBatchAsync` |
| 动态点位 | 重写 `LoadConfig` |
| 按业务优先级分批读取 | 重写 `WhileDoAsync`，自行遍历 `readNodes` |
| 写入审计/重试 | 重写 `WriteBatchAsync` / `WriteStrictlyAsync` |
| 数据旁路 | 重写 `ReceiveDataToMessageChannelAsync` |

```csharp
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.ModbusMasterRTU;
using iml6yu.DataReceive.ModbusMasterRTU.Configs;
using iml6yu.Result;

public sealed class DataReceiverModbusRTUCustom : DataReceiverModbusRTU
{
    // 一条 485 总线一把锁：读和写互斥
    private readonly SemaphoreSlim busLock = new(1, 1);

    public DataReceiverModbusRTUCustom(DataReceiverModbusRTUOption option,
        ILogger<DataReceiverModbusRTUCustom> logger,
        bool isAutoLoadNodeConfig = false,
        List<NodeItem> nodes = null)
        : base(option, logger, isAutoLoadNodeConfig, nodes) { }

    // 写入时占锁，写完释放
    protected override async Task<MessageResult> WriteBatchAsync(DataWriteContract data)
    {
        await busLock.WaitAsync();
        try
        {
            return await base.WriteBatchAsync(data);
        }
        finally
        {
            busLock.Release();
        }
    }

    // 读取时尝试拿锁，拿不到说明正在写，跳过这一轮
    protected override Task WhileDoAsync(CancellationToken token) => Task.Run(async () =>
    {
        var delay = TimeSpan.FromMilliseconds(500);

        while (!token.IsCancellationRequested)
        {
            if (!await busLock.WaitAsync(0, token))
            {
                await Task.Delay(200, token);
                continue;
            }

            try
            {
                if (VerifyConnect())
                {
                    var temp = new Dictionary<string, ReceiverTempDataValue>();
                    foreach (var group in readNodes)              // protected 字段
                    {
                        foreach (var item in group.Value)
                        {
                            foreach (var readConfig in item.Value)
                            {
                                foreach (var node in readConfig.ReadItems)
                                    temp = ReadModbusNodeItem(readConfig, node, temp);

                                if (temp.Count > 0)
                                {
                                    await ReceiveDataToMessageChannelAsync(Option.ProductLineName, temp);
                                    temp = new Dictionary<string, ReceiverTempDataValue>();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "RTU 读取异常：{Message}", ex.Message);
            }
            finally
            {
                busLock.Release();
            }

            await Task.Delay(delay, token);
        }
    }, token);

    public override void Dispose()
    {
        base.Dispose();
        busLock.Dispose();
    }
}
```

**扩展注意事项**

- 重写 `WhileDoAsync` 后，基类原本的"最小间隔"策略不再生效，需要自己决定节奏。
- `readNodes` 是 `protected`，结构为 `分组 → 间隔 → 站号配置 → 读取项`；直接复用可以避免重新解析点位。
- `Dispose` 里一定要 `busLock.Dispose()`，否则服务重启后句柄泄漏。
- 如果要支持"多串口同时采集"，不要在一个实例里开多个串口，直接创建多个 `DataReceiverModbusRTU`（或自定义子类）实例，各自独立 token 与生命周期。

---

## 九、小结

Modbus RTU 的关键不是代码，而是"物理层正确 + 单循环调度 + 站号/地址不冲突"。把串口参数固化到配置、把轮询节奏交给类库、把坏点隔离交给日志和告警，485 采集就能稳定跑起来。

下一篇换一个完全不同的协议栈：西门子 S7。它没有"寄存器地址"的概念，取而代之的是 DB 块和 `DBX/DBB/DBW/DBD` 这套地址体系。


---

## 交流与反馈

|Wechat| QQ邮箱|Gmail（不常用）|
|----|----|----|
|![WeChat](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAFoAAABYCAYAAAB1YOAJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsQAAA7EAZUrDhsAACLISURBVHhe7d0HtC1FES7gMYs5oKiYEROYc84RTJgDRhRUxICKmDChqIgBVAQB9aogBkSCWTGjoogiiFkwA+Zr1v3qqzO1T++5s/c5Fy5vrfeW/1qzZk93z0xPT3VV/VU955xrEuj+h3Mc6wz0P/7xj07Ruc51rr5kMbS1bbTRRn3J/3/429/+1p373Ofuj1bGf//733XHw0AXnva0pxn0s7Q961nP6q8yjje+8Y2T+93vfpN//etfeXz66adPHvCAB0ze+973TnbdddfJk570pCyHn/70p5N73OMek6OPProvmUxe+cpX5vmLcNBBB+V5f/rTn/qS+fjnP/+Zbd/znvdMnvvc5472/7TTTptc4AIXGH3elbZNNtlkcuaZZ/ZXWpLGRLy10RPWZ1uEm93sZtmmbv7tb387jw3eZS5zmZnzP/GJT+Rx+/Cbb775ive4733vm21++MMf9iXz8Zvf/CbbPuhBD5pc/OIXn1zoQhfqa5ZxdgTP9rrXva6/0mRy3ihIxO/+V9e97GUv6x7xiEd0f//73/uScUTnure85S3dnnvu2ZfMx4te9KLuy1/+cnepS10qj6973et2xx9/fHf1q1+9O+SQQ7q1a9dmOdzpTnfqjjvuuKwr7L777t13v/vd7o9//GN35JFHdve85z27S1/60ln3yU9+srvwhS/c7bzzzt01r3nN7vKXv3x38MEHd7e97W27E044obvc5S6Xz/erX/2qu8997pNq8bKXvWz3zGc+s3vwgx/c3ete98r6U045JdvHTOvOd77zTdXFxhtv3H3+85/PNu04DaH9n//85+6ud71r7v/zn//0NYE4MfHXv/51+ia++c1v9qUrIzowPW8Rtthii2xDZQyxmvOvcIUrZJvXvOY1uX/rW9+a5VSR4wte8IKTu93tbvmbJNk//vGPz/01rnGNybWuda38/fvf/z7Pi0HP43vf+96T85///JNLXvKSk3gxWfbjH/842zz96U/P41vf+tZ5vFpc5zrXyfNe+9rX9iWNRLcoSf75z3/e7bXXXl08RL5JkmD/73//O6UhHn6u1DOqod/SMHjTe+yxR/fV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)|[646007589@qq.com](mailto:646007589@qq.com)|[iml6yu@gmail.com](mailto:iml6yu@gmail.com)|
