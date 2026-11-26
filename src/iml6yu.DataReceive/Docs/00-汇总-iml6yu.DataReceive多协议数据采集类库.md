# iml6yu.DataReceive：一套代码打通 Modbus / 西门子 S7 / OPC UA / MQTT

> 关键词：工业数据采集、Modbus TCP、Modbus RTU、S7.Net、OPC UA、MQTT、上位机、边缘网关、.NET

## 前言

做过产线数据采集的朋友应该都经历过这样的循环：新项目接一台 Modbus 网关，写一套轮询；下个项目换成西门子 PLC，再写一套；再下一个项目现场只给 OPC UA，又得重新封装一遍。每个项目里真正花时间的往往不是业务，而是这些"老四样"：

- 连接、断线重连、超时；
- 分组、分时轮询，别把设备打爆；
- 点位配置化，别把地址写死在代码里；
- 读写缓存、变化推送、批量写入校验；
- 异常日志、状态可观测。

`iml6yu.DataReceive` 就是把这些年在产线项目里反复写的东西抽成了一套类库：统一的配置模型、统一的读写 API、统一的事件模型，换协议只是换一个 NuGet 包和一份点位配置。本文是系列总览，先讲清楚整体设计与协议选型，再给出最小可运行骨架，最后是各协议的详细文章索引。

---

## 一、这套类库解决什么问题

一句话：**把"采数据"这件事从业务代码里拿出来，做成可配置、可观测、可替换的组件。**

核心能力：

| 能力 | 说明 |
| --- | --- |
| 配置化点位 | `NodeItem` 描述 `FullAddress`（协议地址）、`Address`（业务键）、`ValueType`、`GroupName`、`Interval`、`Count` |
| 分组分时轮询 | 先按 `GroupName` 分组，组内再按 `Interval`（毫秒）分时，同一间隔的点位一次批量读取 |
| 统一缓存 | 每个点位缓存"值 + 时间戳"，`ReadAllAsync / ReadAsync / ReadsAsync` 直接读缓存，不打扰设备 |
| 主动读取 | `DirectReadAsync` 绕过缓存直接读设备，适合写后校验、按需查询 |
| 统一写入 | `WriteAsync`、`WriteAsync<T>(address, value)`、`WriteWithVerifyAsync`（写后回读比对，再写标志位） |
| 变化事件 | `DataChangedEvent`（值变化才触发）、`DataIntervalEvent`（每个采集周期都触发）、`DataSubscribeEvent`（按订阅 Key 过滤） |
| 连接与异常事件 | `ConnectionEvent`、`ErrorEvent`、`WarnEvent` |
| 心跳健康 | 配置 `HeartBeat` 点位后，`HeartBeatHealthy()` 返回 `Healthy / Degraded / Unavailable / UnKnown` |
| 自动重连 | `StartWorkAsync` 内部循环检测连接，未连接时按 `ReConnectPeriod` 主动重连 |
| 多目标框架 | `net6.0 / net7.0 / net8.0 / net9.0 / net10.0` |

---

## 二、包结构

整个仓库按"核心 + 协议实现"拆分，按需引用，不用为了 MQTT 把 OPC UA 的依赖也带进项目。

| NuGet 包 | 作用 | 关键依赖 |
| --- | --- | --- |
| `iml6yu.DataReceive.Core` | 接收器基类、配置模型、缓存、通道、事件、心跳 | `iml6yu.Data.Core` |
| `iml6yu.DataReceive.ModbusMaster` | Modbus 公共基类：地址解析、寄存器合并、端序转换、写入 | `NModbus` |
| `iml6yu.DataReceive.ModbusMasterTCP` | Modbus TCP 实现 | `NModbus` |
| `iml6yu.DataReceive.ModbusMasterRTU` | Modbus RTU 实现（串口） | `NModbus`、`NModbus.Serial`、`System.IO.Ports` |
| `iml6yu.DataReceive.PLCSiemens` | 西门子 S7 协议实现（内置裁剪版 S7.Net 协议栈） | 无第三方 PLC 依赖 |
| `iml6yu.DataReceive.OPCUA` | OPC UA 客户端与采集实现 | `OPCFoundation.NetStandard.Opc.Ua.Client` |
| `iml6yu.DataReceive.Mqtt` | MQTT 接收/发布基类，支持自定义报文解析 | `MQTTnet`（net6/7 用 4.x，net8+ 用 5.x） |
| `iml6yu.DataReceive.Mqtt.Default` | 默认报文格式（本系统的 `DataReceiveContract`） | `iml6yu.DataReceive.Mqtt` |
| `iml6yu.DataReceive.Mqtt.InPlant` | 中控 MQTT 报文格式（`RTValue`） | `iml6yu.DataReceive.Mqtt` |

安装示例（按项目实际用到的协议安装即可）：

```bash
dotnet add package iml6yu.DataReceive.ModbusMasterTCP
dotnet add package iml6yu.DataReceive.ModbusMasterRTU
dotnet add package iml6yu.DataReceive.PLCSiemens
dotnet add package iml6yu.DataReceive.OPCUA
dotnet add package iml6yu.DataReceive.Mqtt
dotnet add package iml6yu.DataReceive.Mqtt.Default
dotnet add package iml6yu.DataReceive.Mqtt.InPlant
```

---

## 三、统一编程模型

### 3.1 数据流

```text
设备 / Broker
   │  协议层读取
   ▼
DataReceiver<TClient, TOption>
   │  ① 点位缓存 CacheDataDic（值 + 时间戳）
   │  ② 有界消息通道（容量 5000，满了丢最旧）
   │  ③ 后台消费：比对变化 → 触发事件
   ▼
业务代码：事件 / 缓存读取 / 主动读取 / 写入
```

这个设计的直接好处是：**设备读取频率和业务处理频率解耦**。采集循环只管读，业务在事件里处理；事件处理慢了，数据会进通道排队（最多 5000 条），不会阻塞设备读取，也不会把内存撑爆。

### 3.2 生命周期

| 步骤 | API | 说明 |
| --- | --- | --- |
| 1. 注册 | `services.AddReceiver(option, isAutoLoadNodeConfig, nodes)` | 单例注册；`option.NodeFile` 可为空，此时用 `nodes` 传点位 |
| 2. 加载点位 | 构造时自动加载，或手动 `LoadConfig(nodes)` / `LoadConfigAsync(file)` | 点位文件是 `List<NodeItem>` 的 JSON 数组 |
| 3. 启动 | `StartWorkAsync(cancellationToken)` | 启动采集循环与自动重连循环（MQTT 为保活） |
| 4. 使用 | 事件 / `ReadAllAsync` / `ReadAsync` / `DirectReadAsync` / `WriteAsync` | — |
| 5. 停止 | `StopWorkAsync()` + 取消传入的 `token` | 停止依赖外部 token 取消，`StopWorkAsync` 负责断开与释放 |

### 3.3 关键配置项（`DataReceiverOption`）

| 配置 | 默认值 | 说明 |
| --- | --- | --- |
| `ProductLineName` | 空 | 产线名，事件与通道的 `Key` |
| `RefLineNames` | null | 一个采集器同时向多条产线推送数据（复制多份事件） |
| `OriginHost` / `OriginPort` | 空 / 协议默认 | 设备地址；OPC UA 填完整 URL |
| `OriginName` / `OriginPwd` | 空 | 账号密码（OPC UA、MQTT 使用） |
| `ReConnectPeriod` | 30000 ms | 重连间隔 |
| `ReConnectTimes` | 3 | 重连次数配置项 |
| `ConnectTimeout` | 60000 ms | 连接超时 |
| `ReceiverName` | 空 | 采集器名称，日志与 MQTT ClientId 会用到 |
| `NodeFile` | 空 | 点位配置文件路径 |
| `HeartBeat` | null | 心跳点位（`DataReadItem`：地址 + 类型） |
| `AutoConnect` | false | 构造时是否立即异步连接 |

### 3.4 点位模型（`NodeItem`）

```json
{
  "fullAddress": "1.HoldingRegisters2.0",
  "address": "L01.Speed",
  "valueType": "Int32",
  "groupName": "L01",
  "interval": 300,
  "count": 1,
  "descript": "1号线速度"
}
```

| 字段 | 说明 |
| --- | --- |
| `fullAddress` | 协议地址，各协议格式不同（Modbus：`站号.类型.地址`；S7：`DB1.DBW0`；OPC UA：`ns=2;s=Tag`） |
| `address` | 业务键，事件与缓存都用它，**必须全局唯一** |
| `valueType` | `TypeCode` 字符串（`Boolean / Int16 / UInt16 / Int32 / UInt32 / Int64 / UInt64 / Single / Double / DateTime / String` 等） |
| `groupName` | 分组名，决定轮询分组与事件 `Key` |
| `interval` | 读取间隔（ms），0 视为 500ms |
| `count` | 读取长度，Modbus 由读取类型决定（此字段无效），S7 字符串/数组使用 |
| `descript` | 备注 |

### 3.5 事件与读取

```csharp
receiver.ConnectionEvent += (sender, e) => { /* e.IsConntion, e.Message */ };
receiver.ErrorEvent      += (sender, e) => { /* e.Ex, e.Message */ };
receiver.DataChangedEvent   += (sender, e) => { /* 只有值变化才触发，e.Datas */ };
receiver.DataIntervalEvent  += (sender, e) => { /* 每个采集周期都触发 */ };
receiver.DataSubscribeEvent += (sender, e) => { /* sender 是订阅 Key */ };
```

| API | 是否访问设备 | 适用场景 |
| --- | --- | --- |
| `ReadAllAsync()` | 否，读缓存 | 页面初始化、全量快照 |
| `ReadAsync(address)` | 否，读缓存 | 单点查询 |
| `ReadsAsync(addresses)` | 否，读缓存 | 批量查询 |
| `DirectReadAsync(addresses)` | 是，直连设备 | 校验、临时取数；频繁调用会影响采集循环 |
| `WriteAsync(contract)` | 是 | 批量写入，不校验 |
| `WriteWithVerifyAsync(contract)` | 是 | 写入 → 回读比对 → 最后写 `IsFlag` 标志位 |
| `Subscribe(key, addresses)` | 否 | 按业务分组订阅，最多 20 个 Key、每个 Key 100 个地址 |

### 3.6 写入模型

```csharp
await receiver.WriteWithVerifyAsync(new DataWriteContract
{
    Id = 1,
    Key = "L01",
    Datas =
    [
        new DataWriteContractItem { Address = "L01.SpeedSet", Value = 12.5, ValueType = TypeCode.Single },
        new DataWriteContractItem { Address = "L01.WriteFlag", Value = true, ValueType = TypeCode.Boolean, IsFlag = true }
    ]
});
```

`IsFlag = true` 的点位在所有普通点位写入并回读校验成功后才会写入，适合"先写参数、最后置标志位"这类设备交互约定。`Sort` 控制写入顺序。

---

## 四、协议选型对照

| 协议 | 典型场景 | 业务环境 | 优势 | 限制 |
| --- | --- | --- | --- | --- |
| Modbus TCP | 网关、仪表、变频器、称重、扫码器 | 以太网可达，设备支持 502 端口 | 简单、通用、生态最好 | 无类型语义，端序靠配置；并发取决于网关 |
| Modbus RTU | 老设备、485 总线、串口服务器 | 现场只有 RS485/RS232，或串口服务器 | 成本低、抗干扰好 | 串行总线不能并发，一条总线共用一个轮询节奏 |
| 西门子 S7 | S7-1200 / 1500 / 300 / 400 产线 | 西门子 PLC 产线、设备层直采 | 不用装 OPC 服务器，直接读 DB | 强依赖 PLC 地址与 PDU 大小；地址类型要匹配 |
| OPC UA | 中控、DCS、MES、跨厂商集成 | 已有 OPC UA Server，或需要标准化信息模型 | 类型自描述、跨平台、安全模型完整 | 点位多时批量读受 `MaxNodesPerRead` 限制；安全配置需按现场调整 |
| MQTT | 设备/平台主动上报、跨网段、弱网 | 已经有 Broker，数据由设备侧推送 | 推送模式、无需轮询、天然解耦 | 数据格式需要约定；断线补发依赖设备侧 QoS/缓存 |

> 选型建议：能直连 PLC 就不要多加一层 OPC Server；点位少、实时性要求高，Modbus TCP 最省事；跨厂商、要求标准化，选 OPC UA；数据源本身就是消息中间件，用 MQTT。

---

## 五、最小可运行骨架

以 Modbus TCP 为例，其它协议只需要换 `Option`、`AddReceiver` 与点位地址格式。

**Program.cs**

```csharp
using iml6yu.DataReceive.ModbusMasterTCP;
using iml6yu.DataReceive.ModbusMasterTCP.Configs;

var builder = Host.CreateApplicationBuilder(args);

// 从 appsettings.json 读取连接配置
DataReceiverModbusTCPOption option =
    builder.Configuration.GetSection("DataReceiverOption").Get<DataReceiverModbusTCPOption>();

// 第三个参数传 null：从 option.NodeFile 读取点位 JSON
builder.Services.AddReceiver(option, true, null);
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
```

**appsettings.json**

```json
{
  "DataReceiverOption": {
    "originHost": "127.0.0.1",
    "originPort": 502,
    "reConnectPeriod": 10000,
    "reConnectTimes": 2,
    "connectTimeout": 60000,
    "receiverName": "L01_ModbusTCP",
    "nodeFile": "config/nodes.json",
    "productLineName": "L01",
    "autoConnect": false
  }
}
```

**config/nodes.json**

```json
[
  {
    "fullAddress": "1.HoldingRegisters2.0",
    "address": "L01.Speed",
    "valueType": "Int32",
    "groupName": "L01",
    "interval": 300,
    "descript": "线速度"
  },
  {
    "fullAddress": "1.Coils.0",
    "address": "L01.Running",
    "valueType": "Boolean",
    "groupName": "L01",
    "interval": 300,
    "descript": "运行状态"
  }
]
```

**Worker.cs**

```csharp
using iml6yu.DataReceive.ModbusMasterTCP;

public class Worker : BackgroundService
{
    private readonly DataReceiverModbusTCP receiver;
    private readonly ILogger<Worker> logger;

    public Worker(DataReceiverModbusTCP receiver, ILogger<Worker> logger)
    {
        this.receiver = receiver;
        this.logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        receiver.ConnectionEvent += (_, e) =>
            logger.LogInformation("连接状态：{State} {Message}", e.IsConntion, e.Message);
        receiver.ErrorEvent += (_, e) => logger.LogError(e.Ex, "采集异常：{Message}", e.Message);
        receiver.DataChangedEvent += (_, e) =>
            logger.LogInformation("数据变化 {Time}：{Data}",
                DateTimeOffset.FromUnixTimeMilliseconds(e.Timestamp).ToString("HH:mm:ss.fff"),
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
        // 每 5 秒读一次缓存，验证数据是否在刷新
        while (!stoppingToken.IsCancellationRequested)
        {
            var all = await receiver.ReadAllAsync(stoppingToken);
            if (all.State)
                logger.LogInformation("缓存点位 {Count} 个，连接={Connected}", all.Data.Count, receiver.IsConnected);

            await Task.Delay(5000, stoppingToken);
        }
    }
}
```

---

## 六、通用注意事项

1. **`StartWorkAsync` 的 token 是唯一可靠的停止手段**：内部循环靠 `token.IsCancellationRequested` 退出；`StopWorkAsync()` 只做断开与释放，不取消 token。BackgroundService 里务必把 `stoppingToken` 传进去。
2. **不要重复调用 `StartWorkAsync`**：每调用一次就会多启动一套采集循环，现场表现为设备请求量翻倍。
3. **`address` 必须全局唯一**：缓存和事件都以 `address` 为键，重复会导致配置加载失败（日志里会出现 `An item with the same key has already been added`）。
4. **`groupName` 不要为空**：分组字典以 `groupName` 为键，空值会导致配置加载失败。
5. **`interval = 0` 等价 500ms**：最小间隔不要小于设备响应时间，否则请求会堆积。
6. **`valueType` 必须和协议读取类型匹配**：例如 Modbus 32 位类型只能配 `Int32 / UInt32 / Single`，配成 `Boolean` 会被类型校验丢弃（日志有 `数据已丢弃` 警告）。
7. **事件处理要快**：事件在后台消费线程上执行，长耗时逻辑请转移到自己的队列；通道容量 5000，满了会丢最旧的数据并打印预警日志。
8. **`DirectReadAsync` 不要放在高频循环里**：它直接读设备，会和轮询争抢连接。
9. **`Option.AutoConnect = true` 时会在构造函数里异步连接**；`isAutoLoadNodeConfig = true` 且 `nodes == null` 时会在构造函数里**同步**读取点位文件，文件大时注意启动耗时。
10. **健康判断不要只看 `State`**：正常取消后任务结束状态仍可能显示 `Working`，建议以 `IsConnected` + 缓存数据时间戳 + `HeartBeatHealthy()` 三者结合判断。

---

## 七、三种宿主环境怎么落地

同一套采集代码，可以跑在控制台、WinForms、Windows 服务里，区别只在"谁管理生命周期"和"日志/异常往哪去"。

| 宿主 | 典型用途 | 生命周期 | 需要额外注意 |
| --- | --- | --- | --- |
| 控制台 / Worker | 服务端采集、现场调试、容器化 | `Main` 阻塞或 `BackgroundService` | Ctrl+C 取消、控制台日志、退出码 |
| WinForms | 现场调试工具、简单上位机、看板 | UI 主线程 + 后台采集 | 事件跨线程、配置随程序发布、窗体关闭时释放 |
| Windows 服务 | 7×24 产线、无人值守 | SCM 管理，随系统启动 | 工作目录、事件日志、串口/证书权限、优雅停止 |

三个通用原则：

1. **`StartWorkAsync` 的 token 就是生命周期**：控制台用 `Ctrl+C`、WinForms 用 `FormClosing`、服务用 `Host` 的 `stoppingToken`。
2. **停止顺序固定**：先 `Cancel()`，再 `await StopWorkAsync()`，最后 `Dispose()`（或交给容器释放）。
3. **日志要落到宿主能看到的地方**：控制台用 Console，服务用 EventLog/文件，WinForms 用日志文件或界面日志框。

各协议的完整宿主代码见每篇文章的"三种宿主环境"小节（可直接复制改造）：

- [Modbus TCP 的 WinForms / 控制台 / 服务写法](01-ModbusTCP数据采集.md)
- [Modbus RTU 的 WinForms / 控制台 / 服务写法](02-ModbusRTU数据采集.md)
- [S7 的 WinForms / 控制台 / 服务写法](03-西门子S7数据采集.md)
- [OPC UA 的 WinForms / 控制台 / 服务写法](04-OPCUA数据采集.md)
- [MQTT 的 WinForms / 控制台 / 服务写法](05-MQTT数据采集.md)

---

## 八、扩展与二次开发：继承哪些、重写哪些

### 8.1 先看基类的扩展点

`DataReceiver<TClient, TOption>` 把流程拆成了"抽象方法 + 虚方法"两层：

| 成员 | 类型 | 作用 | 什么时候重写 |
| --- | --- | --- | --- |
| `IsConnected` | 抽象属性 | 连接状态 | 新协议必须实现 |
| `ConnectAsync` / `DisConnectAsync` | 抽象方法 | 建连/断连 | 新协议必须实现 |
| `CreateClient` | 抽象方法 | 创建底层客户端 | 新协议必须实现；改证书/超时也可重写 |
| `WhileDoAsync` | 抽象方法 | 采集主循环 | 新协议必须实现；改调度策略可重写 |
| `DirectReadAsync` | 抽象方法 | 绕过缓存直读 | 新协议必须实现 |
| `WriteBatchAsync` | 抽象方法 | 批量写入 | 新协议必须实现；加审计/重试可重写 |
| `WriteOneAsync` / `WriteAsync<T>` | 抽象方法 | 单点写入 | 新协议必须实现；改写入协议可重写 |
| `LoadConfig` | 虚方法 | 解析点位、建缓存 | 动态点位、点位二次加工 |
| `UpdateCatch` | 虚方法 | 更新缓存并产出变化集 | 自定义"变化"判定、值加工 |
| `VerifyValue` / `VerifyValueEqual` | 虚方法 | 类型校验/写后比对 | 自定义换算、兼容特殊类型 |
| `ReceiveDataToMessageChannelAsync` | 虚方法 | 数据投递到通道 | 数据旁路（同时写 Kafka/DB/日志） |
| `WriteStrictlyAsync` | 虚方法 | 严格写入（写后回读） | 自定义校验流程（如允许 2 个标志位） |
| `InitReceiveDatasAction` | 虚方法 | 通道消费与事件分发 | 自定义事件分发策略 |
| `Dispose` | 虚方法 | 释放资源 | 额外资源清理 |

**不可重写**（没有 `virtual`）的成员：`ReadAllAsync`、`ReadAsync`、`ReadsAsync`、`Subscribe`、`VerifyConnect`、`HeartBeatHealthy`、`WriteAsync(DataWriteContract)`、`WriteWithVerifyAsync`、`WriteAsync(DataWriteContractItem)`、`State`、`Name`。要改变这些流程，只能在它们内部调用的虚方法上做文章，或者在业务层包一层。

### 8.2 新增一个协议：最小骨架

只要继承 `DataReceiver<TClient, TOption>` 并实现 8 个抽象成员，就能复用缓存、通道、事件、心跳、重连、订阅等全部能力。

```csharp
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.Result;

// 1. 底层客户端（示例）
public sealed class MyClient : IDisposable
{
    public bool Connected { get; private set; }
    public Task OpenAsync() { Connected = true; return Task.CompletedTask; }
    public Task CloseAsync() { Connected = false; return Task.CompletedTask; }
    public Task<object> ReadAsync(string address) => Task.FromResult<object>(DateTime.Now.Second);
    public Task WriteAsync(string address, object value) => Task.CompletedTask;
    public void Dispose() { }
}

// 2. 自定义配置（继承 DataReceiverOption，可继续加字段）
public sealed class DataReceiverMyProtocolOption : DataReceiverOption
{
    public int PollInterval { get; set; } = 500;
}

// 3. 采集器
public sealed class DataReceiverMyProtocol : DataReceiver<MyClient, DataReceiverMyProtocolOption>
{
    private Dictionary<string, List<NodeItem>> groups = new();

    public DataReceiverMyProtocol(DataReceiverMyProtocolOption option, ILogger<DataReceiverMyProtocol> logger,
        bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null)
        : base(option, logger, isAutoLoadNodeConfig, nodes) { }

    public override bool IsConnected => Client?.Connected == true;

    protected override MyClient CreateClient(DataReceiverMyProtocolOption option) => new();

    public override async Task<MessageResult> ConnectAsync()
    {
        if (IsConnected) return MessageResult.Success();
        try { await Client.OpenAsync(); return MessageResult.Success(); }
        catch (Exception ex) { return MessageResult.Failed(ResultType.Failed, ex.Message, ex); }
    }

    public override async Task<MessageResult> DisConnectAsync()
    {
        try { await Client.CloseAsync(); return MessageResult.Success(); }
        catch (Exception ex) { return MessageResult.Failed(ResultType.Failed, ex.Message, ex); }
    }

    public override MessageResult LoadConfig(List<NodeItem> nodes)
    {
        var result = base.LoadConfig(nodes);   // 建立 ConfigNodes / CacheDataDic
        if (!result.State) return result;
        groups = ConfigNodes;
        return MessageResult.Success();
    }

    protected override Task WhileDoAsync(CancellationToken token) => Task.Run(async () =>
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (VerifyConnect())
                {
                    foreach (var group in groups)
                    {
                        var temp = new Dictionary<string, ReceiverTempDataValue>();
                        foreach (var node in group.Value)
                        {
                            var value = await Client.ReadAsync(node.FullAddress);
                            temp[node.Address] = new ReceiverTempDataValue(value, GetTimestamp());
                        }
                        await ReceiveDataToMessageChannelAsync(Option.ProductLineName, temp);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "采集异常：{Message}", ex.Message);
            }
            await Task.Delay(Option.PollInterval, token);
        }
    }, token);

    protected override async Task<MessageResult> WriteBatchAsync(DataWriteContract data)
    {
        foreach (var item in data.Datas)
            await Client.WriteAsync(item.Address, item.Value);
        return MessageResult.Success();
    }

    public override async Task<MessageResult> WriteOneAsync(DataWriteContractItem data)
        => await WriteAsync(data.Address, data.Value);

    public override async Task<MessageResult> WriteAsync<T>(string address, T data)
    {
        try { await Client.WriteAsync(address, data); return MessageResult.Success(); }
        catch (Exception ex) { return MessageResult.Failed(ResultType.DeviceWriteError, ex.Message, ex); }
    }

    public override async Task<DataResult<DataReceiveContract>> DirectReadAsync(
        IEnumerable<DataReadItem> addressArray, CancellationToken cancellationToken = default)
    {
        try
        {
            var contract = new DataReceiveContract(GetTimestamp()) { Key = Option.ProductLineName };
            foreach (var item in addressArray)
            {
                contract.Datas.Add(new DataReceiveContractItem
                {
                    Address = item.Address,
                    Timestamp = contract.Timestamp,
                    Value = await Client.ReadAsync(item.Address),
                    ValueType = (int)item.ValueType
                });
            }
            return DataResult<DataReceiveContract>.Success(contract);
        }
        catch (Exception ex)
        {
            return DataResult<DataReceiveContract>.Failed(ResultType.Failed, ex.Message, ex);
        }
    }
}
```

### 8.3 自定义配置与依赖注入

```csharp
public sealed class DataReceiverMyProtocolOption : DataReceiverOption
{
    public string Endpoint { get; set; }
    public int PollInterval { get; set; } = 500;
}

public static class MyProtocolReceiverExtensions
{
    public static IServiceCollection AddMyReceiver(this IServiceCollection services,
        DataReceiverMyProtocolOption option, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null)
    {
        services.AddSingleton(sp => new DataReceiverMyProtocol(
            option,
            sp.GetRequiredService<ILogger<DataReceiverMyProtocol>>(),
            isAutoLoadNodeConfig,
            nodes));
        return services;
    }
}
```

### 8.4 扩展时的三个硬约束

1. **基类构造函数会立刻调用 `CreateClient`**：派生类自己 `new` 出来的字段在 `CreateClient` 里还没赋值，不要把"需要在 CreateClient 中使用的状态"放在派生类字段里。
2. **`Option`、`Client`、`ConfigNodes`、`CacheDataDic`、`MessageChannel`、`Logger` 都是 `protected`/`public`**，可以直接用；但 `CacheDataDic` 的键必须与点位 `address` 一致。
3. **事件在后台线程触发**：无论宿主是 WinForms 还是服务，重写 `InitReceiveDatasAction` 或订阅事件时都要考虑线程安全。

---

## 九、系列文章索引

| 协议 | 文章 | 主要内容 |
| --- | --- | --- |
| 汇总 | 本文 | 架构、通用能力、选型、骨架 |
| Modbus TCP | [Modbus TCP 数据采集实战](01-ModbusTCP数据采集.md) | 地址类型全表、端序、合并读取、写入 |
| Modbus RTU | [Modbus RTU 数据采集实战](02-ModbusRTU数据采集.md) | 串口参数、单循环调度、485 现场问题 |
| 西门子 S7 | [西门子 S7 数据采集实战](03-西门子S7数据采集.md) | DB 地址、扩展类型、PDU、Rack/Slot |
| OPC UA | [OPC UA 数据采集实战](04-OPCUA数据采集.md) | NodeId、URL、安全、批量读 |
| MQTT | [MQTT 数据采集实战](05-MQTT数据采集.md) | 主题、三种报文格式、发布写入 |

---

## 小结

`iml6yu.DataReceive` 的价值不在于"又多了一个 Modbus 封装"，而在于把五种协议收敛成同一套配置、同一套读写与事件模型：换协议时业务代码几乎不用改，点位从代码里搬到 JSON，采集的稳定性交给组件。下一篇从使用最广的 Modbus TCP 开始，把地址类型和端序这两个最容易踩坑的点讲透。


---

## 交流与反馈

|Wechat| QQ邮箱|Gmail（不常用）|
|----|----|----|
|![WeChat](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAFoAAABYCAYAAAB1YOAJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsQAAA7EAZUrDhsAACLISURBVHhe7d0HtC1FES7gMYs5oKiYEROYc84RTJgDRhRUxICKmDChqIgBVAQB9aogBkSCWTGjoogiiFkwA+Zr1v3qqzO1T++5s/c5Fy5vrfeW/1qzZk93z0xPT3VV/VU955xrEuj+h3Mc6wz0P/7xj07Ruc51rr5kMbS1bbTRRn3J/3/429/+1p373Ofuj1bGf//733XHw0AXnva0pxn0s7Q961nP6q8yjje+8Y2T+93vfpN//etfeXz66adPHvCAB0ze+973TnbdddfJk570pCyHn/70p5N73OMek6OPProvmUxe+cpX5vmLcNBBB+V5f/rTn/qS+fjnP/+Zbd/znvdMnvvc5472/7TTTptc4AIXGH3elbZNNtlkcuaZZ/ZXWpLGRLy10RPWZ1uEm93sZtmmbv7tb387jw3eZS5zmZnzP/GJT+Rx+/Cbb775ive4733vm21++MMf9iXz8Zvf/CbbPuhBD5pc/OIXn1zoQhfqa5ZxdgTP9rrXva6/0mRy3ihIxO/+V9e97GUv6x7xiEd0f//73/uScUTnure85S3dnnvu2ZfMx4te9KLuy1/+cnepS10qj6973et2xx9/fHf1q1+9O+SQQ7q1a9dmOdzpTnfqjjvuuKwr7L777t13v/vd7o9//GN35JFHdve85z27S1/60ln3yU9+srvwhS/c7bzzzt01r3nN7vKXv3x38MEHd7e97W27E044obvc5S6Xz/erX/2qu8997pNq8bKXvWz3zGc+s3vwgx/c3ete98r6U045JdvHTOvOd77zTdXFxhtv3H3+85/PNu04DaH9n//85+6ud71r7v/zn//0NYE4MfHXv/51+ia++c1v9qUrIzowPW8Rtthii2xDZQyxmvOvcIUrZJvXvOY1uX/rW9+a5VSR4wte8IKTu93tbvmbJNk//vGPz/01rnGNybWuda38/fvf/z7Pi0HP43vf+96T85///JNLXvKSk3gxWfbjH/842zz96U/P41vf+tZ5vFpc5zrXyfNe+9rX9iWNRLcoSf75z3/e7bXXXl08RL5JkmD/73//O6UhHn6u1DOqod/SMHjTe+yxR/fV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)|[646007589@qq.com](mailto:646007589@qq.com)|[iml6yu@gmail.com](mailto:iml6yu@gmail.com)|
