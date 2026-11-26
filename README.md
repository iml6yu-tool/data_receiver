# iml6yu.Data · 多协议工业数据采集与数据服务类库

[English](README.en.md) | 简体中文

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
![.NET](https://img.shields.io/badge/.NET-6.0%20%7C%207.0%20%7C%208.0%20%7C%209.0%20%7C%2010.0-512BD4)
[![GitHub stars](https://img.shields.io/github/stars/iml6yu-tool/data_receiver?style=social)](https://github.com/iml6yu-tool/data_receiver/stargazers)
[![GitHub issues](https://img.shields.io/github/issues/iml6yu-tool/data_receiver)](https://github.com/iml6yu-tool/data_receiver/issues)
[![Last commit](https://img.shields.io/github/last-commit/iml6yu-tool/data_receiver)](https://github.com/iml6yu-tool/data_receiver/commits/main)

> 基于 .NET 6/7/8/9/10 的统一数据采集接收器（`DataReceiver`）与数据服务（`DataService`）类库。
> 一套抽象、多种协议：**Modbus TCP / Modbus RTU / OPC UA / Siemens S7 / MQTT**。

---

## 目录

- [简介](#简介)
- [功能特性](#功能特性)
- [支持的协议](#支持的协议)
- [架构概览](#架构概览)
- [项目结构](#项目结构)
- [快速开始](#快速开始)
- [配置说明](#配置说明)
- [核心 API](#核心-api)
- [事件模型](#事件模型)
- [详细使用规则](#详细使用规则)
- [数据服务（DataService）](#数据服务dataservice)
- [示例项目](#示例项目)
- [构建](#构建)
- [常见问题](#常见问题)
- [贡献](#贡献)
- [许可证](#许可证)

---

## 简介

`data_receiver` 提供了一套统一的工业设备数据采集抽象：无论底层是 Modbus、OPC UA、Siemens S7 还是 MQTT，上层业务都通过同一个 `IDataReceiver` 接口完成**连接、轮询、缓存、读取、写入、订阅与事件回调**。

同时项目还包含 `DataService`，可将数据以 **Modbus TCP/RTU 从站**或 **OPC UA 服务端**的形式对外提供，用于设备联调、数据回写与模拟。

核心目标：

- **协议无关**：业务代码只依赖统一接口与数据契约。
- **开箱即用**：与 `Microsoft.Extensions.DependencyInjection`、`ILogger`、`IConfiguration` 无缝集成。
- **配置驱动**：连接参数与点位（Node）均通过 JSON 配置，可按需加载或重新加载。
- **稳定可靠**：内置自动重连、数据缓存、变化检测、心跳检测与有界消息通道。

---

## 功能特性

| 特性 | 说明 |
| --- | --- |
| 统一抽象 | `IDataReceiver` / `IDataReceiver<TClient, TOption>` 屏蔽协议差异 |
| 多协议支持 | Modbus TCP、Modbus RTU、OPC UA、Siemens S7、MQTT |
| 依赖注入 | 各协议包提供 `AddReceiver(...)` / `AddMqttReceiver(...)` 扩展方法 |
| 自动重连 | `StartWorkAsync` 持续检测连接状态并按 `ReConnectPeriod` 自动重连 |
| 数据缓存 | 以地址为 Key 缓存最新值与时间戳，支持 `ReadAllAsync` / `ReadAsync` / `ReadsAsync` |
| 变化检测 | 仅当值发生变化时触发 `DataChangedEvent`，避免重复上报 |
| 定时轮询 | 按点位 `Interval` 周期读取并触发 `DataIntervalEvent` |
| 数据订阅 | `Subscribe(key, addresses)` 按业务 Key 订阅变化数据，最多 20 组、每组 100 个地址 |
| 读写能力 | 支持批量写、单点写、写入后校验（`WriteWithVerifyAsync`） |
| 心跳检测 | 依据配置的心跳点位判断 `Healthy / Degraded / Unavailable` |
| 有界通道 | 内部使用容量 5000 的 `Channel`，满时丢弃最旧数据并告警 |
| 多目标框架 | `net6.0` / `net7.0` / `net8.0` / `net9.0` / `net10.0` |
| 数据服务 | Modbus TCP/RTU 从站、OPC UA 服务端，支持默认值与心跳写入 |

---

## 支持的协议

### 数据采集（DataReceive）

| NuGet 包 | 协议 | 底层依赖 | 说明 |
| --- | --- | --- | --- |
| `iml6yu.DataReceive.Core` | - | `iml6yu.License` | 接收器核心抽象、配置模型、缓存与事件 |
| `iml6yu.DataReceive.ModbusMaster` | Modbus | `NModbus` 3.0.81 | Modbus 公共读取模型与类型定义 |
| `iml6yu.DataReceive.ModbusMasterTCP` | Modbus TCP | `NModbus` | Modbus TCP 主站采集 |
| `iml6yu.DataReceive.ModbusMasterRTU` | Modbus RTU | `NModbus.Serial`、`System.IO.Ports` | Modbus RTU 串口采集 |
| `iml6yu.DataReceive.OPCUA` | OPC UA | `OPCFoundation.NetStandard.Opc.Ua.Client` 1.5.377.22 | OPC UA 客户端采集 |
| `iml6yu.DataReceive.PLCSiemens` | Siemens S7 | 内置 `S7.Net` 源码 | 西门子 S7 系列 PLC 采集 |
| `iml6yu.DataReceive.Mqtt` | MQTT | `MQTTnet` 5.x（net8+）/ 4.3.7（net6/7） | MQTT 订阅采集，需自定义数据解析器 |
| `iml6yu.DataReceive.Mqtt.Default` | MQTT | 同上 | 按 `DataReceiveContract` 默认 JSON 格式解析 |
| `iml6yu.DataReceive.Mqtt.InPlant` | MQTT | 同上 | 中控 InPlant 数据格式解析 |

### 数据服务（DataService）

| NuGet 包 | 服务类型 | 说明 |
| --- | --- | --- |
| `iml6yu.DataService.Core` | - | 服务核心接口 `IDataService` 与配置模型 |
| `iml6yu.DataService.Modbus` | Modbus | Modbus 从站公共实现与数据存储 |
| `iml6yu.DataService.ModbusTCP` | Modbus TCP 从站 | 将数据以 Modbus TCP 从站对外提供 |
| `iml6yu.DataService.ModbusRTU` | Modbus RTU 从站 | 将数据以 Modbus RTU 从站对外提供 |
| `iml6yu.DataService.OpcUa` | OPC UA 服务端 | 将数据以 OPC UA 节点形式对外提供 |

---

## 架构概览

```mermaid
flowchart LR
    subgraph 数据源
        M1[Modbus TCP 设备]
        M2[Modbus RTU 设备]
        O1[OPC UA Server]
        S1[Siemens PLC]
        Q1[MQTT Broker]
    end

    subgraph DataReceive[数据采集层]
        R1[DataReceiverModbusTCP]
        R2[DataReceiverModbusRTU]
        R3[DataReceiverOPCUA]
        R4[DataReceiverPlcS7]
        R5[DataReceiverMqtt]
        CORE[IDataReceiver / DataReceiver 基类]
    end

    subgraph Output[输出]
        E1[DataChangedEvent]
        E2[DataIntervalEvent]
        E3[DataSubscribeEvent]
        API[Read / Write / HeartBeat]
    end

    M1 --> R1
    M2 --> R2
    O1 --> R3
    S1 --> R4
    Q1 --> R5
    R1 --> CORE
    R2 --> CORE
    R3 --> CORE
    R4 --> CORE
    R5 --> CORE
    CORE --> E1
    CORE --> E2
    CORE --> E3
    CORE --> API
```

数据流：**采集 → 类型校验 → 缓存更新（变化检测）→ 有界通道 → 事件分发**。

---

## 项目结构

```
iml6yu.Data
├── doc/                             # 补充文档
├── src/
│   ├── iml6yu.Data.sln              # 解决方案
│   ├── Directory.Build.props        # 多目标框架与版本号
│   ├── iml6yu.DataCore/             # 数据契约与公共模型
│   │   └── iml6yu.Data.Core/
│   ├── iml6yu.DataReceive/          # 数据采集实现
│   │   ├── iml6yu.DataReceive.Core/           # 核心抽象
│   │   ├── iml6yu.DataReceive.ModbusMaster/   # Modbus 公共模型
│   │   ├── iml6yu.DataReceive.ModbusMasterTCP/
│   │   ├── iml6yu.DataReceive.ModbusMasterRTU/
│   │   ├── iml6yu.DataReceive.OPCUA/
│   │   ├── iml6yu.DataReceive.PLCSiemens/
│   │   ├── iml6yu.DataReceive.Mqtt/
│   │   ├── iml6yu.DataReceive.Mqtt.Default/
│   │   └── iml6yu.DataReceive.Mqtt.InPlant/
│   ├── iml6yu.DataService/          # 数据服务实现
│   │   ├── iml6yu.DataService.Core/
│   │   ├── iml6yu.DataService.Modbus/
│   │   ├── iml6yu.DataService.ModbusTCP/
│   │   ├── iml6yu.DataService.ModbusRTU/
│   │   └── iml6yu.DataService.OpcUa/
│   ├── iml6yu.Database/             # 数据库常量
│   ├── examples/                    # 可运行示例
│   └── test/                        # 测试与验证工程
├── LICENSE
└── README.md
```

版本号统一维护在 [Directory.Build.props](src/Directory.Build.props) 中，当前为 `2.0.2-beta261126001`。

---

## 快速开始

### 环境要求

- .NET SDK 10.0（类库多目标编译到 `net10.0`；仅运行示例时 .NET 8 SDK 即可）
- 可访问对应协议的设备或服务端

### 1. 安装 NuGet 包

以 Modbus TCP 为例：

```bash
dotnet add package iml6yu.DataReceive.ModbusMasterTCP
```

其他协议按需安装：

```bash
dotnet add package iml6yu.DataReceive.ModbusMasterRTU
dotnet add package iml6yu.DataReceive.OPCUA
dotnet add package iml6yu.DataReceive.PLCSiemens
dotnet add package iml6yu.DataReceive.Mqtt
```

> 如果 NuGet 上没有对应版本，可直接引用仓库中的源码项目。

### 2. 注册接收器（依赖注入）

```csharp
using iml6yu.DataReceive.ModbusMasterTCP;
using iml6yu.DataReceive.ModbusMasterTCP.Configs;

var builder = Host.CreateApplicationBuilder(args);

var option = builder.Configuration
    .GetSection("DataReceiverOption")
    .Get<DataReceiverModbusTCPOption>();

// isAutoLoadNodeConfig = true：从 option.NodeFile 加载点位配置
builder.Services.AddReceiver(option, isAutoLoadNodeConfig: true);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

### 3. 配置文件

`appsettings.json`：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "DataReceiverOption": {
    "originName": "",
    "originPwd": "",
    "originHost": "127.0.0.1",
    "originPort": 502,
    "reConnectPeriod": 10000,
    "reConnectTimes": 2,
    "connectTimeout": 60000,
    "receiverName": "warn_receiver",
    "nodeFile": "config/L01_P4.json",
    "autoConnect": false
  }
}
```

### 4. 点位配置（Node）

`config/L01_P4.json`：

```json
[
  {
    "fullAddress": "1.ReadInputRegisters2.0",
    "address": "CheckResults",
    "valueType": "Int32",
    "interval": 300,
    "groupName": "L01",
    "descript": ""
  }
]
```

### 5. 编写后台服务

```csharp
using iml6yu.DataReceive.ModbusMasterTCP;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly DataReceiverModbusTCP _receiver;

    public Worker(ILogger<Worker> logger, DataReceiverModbusTCP receiver)
    {
        _logger = logger;
        _receiver = receiver;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _receiver.ConnectionEvent += (_, e) =>
            _logger.LogInformation("连接状态：{Connected} {Message}", e.IsConntion, e.Message);

        _receiver.ErrorEvent += (_, e) =>
            _logger.LogError(e.Ex, "采集异常：{Message}", e.Message);

        _receiver.DataChangedEvent += (_, e) =>
            _logger.LogInformation("数据变化：{Key} {Time} {Datas}",
                e.Key,
                DateTimeOffset.FromUnixTimeMilliseconds(e.Timestamp).ToString("yyyy-MM-dd HH:mm:ss.fff"),
                string.Join(",", e.Datas.Select(d => $"{d.Address}={d.Value}")));

        await _receiver.StartWorkAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _receiver.StopWorkAsync();
        await base.StopAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
```

### 6. 主动读写

```csharp
// 读取缓存中的单个点位
var value = await _receiver.ReadAsync("CheckResults");

// 主动读取设备（不经过缓存）
var direct = await _receiver.DirectReadAsync(new[]
{
    new DataReadItem { Address = "1.Coils.0", ValueType = TypeCode.Boolean }
});

// 写入单个点位
await _receiver.WriteAsync("1.Coils.4", true);

// 批量写入并校验
await _receiver.WriteWithVerifyAsync(new DataWriteContract
{
    Key = "biz-key",
    Datas = new[]
    {
        new DataWriteContractItem { Address = "1.HoldingRegistersDouble.6", Value = 12.34, ValueType = TypeCode.Double }
    }
});

// 订阅变化
_receiver.DataSubscribeEvent += (key, e) => { /* key 为订阅时的分组标识 */ };
_receiver.Subscribe("dashboard", new[] { "CheckResults", "1.Coils.0" });

// 心跳
var health = await _receiver.HeartBeatHealthy(); // Healthy / Degraded / Unavailable / UnKnown
```

其他协议注册方式一致，仅需替换 `Option` 与 `AddReceiver` 所在命名空间：

| 协议 | Option 类型 | 注册说明 |
| --- | --- | --- |
| Modbus TCP | `DataReceiverModbusTCPOption` | `services.AddReceiver(option, ...)` |
| Modbus RTU | `DataReceiverModbusRTUOption` | 额外配置 `comPort` / `baudRate` / `parity` / `dataBits` / `stopBit` |
| OPC UA | `DataReceiverOPCUAOption` | 地址使用 OPC UA 节点 ID（如 `ns=2;s=Tag`） |
| Siemens S7 | `DataReceiverPlcS7Option` | 额外配置 `cpuType` / `rack` / `slot` |
| MQTT | `DataReceiverMqttOption` | 使用 `AddMqttReceiver(option, dataParse, ...)` 传入数据解析器 |

MQTT 数据解析器示例：

```csharp
builder.Services.AddMqttReceiver(
    builder.Configuration.GetSection("GathererMqttOption").Get<DataReceiverMqttOption>(),
    json =>
    {
        if (string.IsNullOrEmpty(json)) return null;

        var data = JsonSerializer.Deserialize<InPlantMqttEntity>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data?.RTValue == null) return null;

        return data.RTValue
            .Where(x => x.Value != null && !string.IsNullOrEmpty(x.Name))
            .GroupBy(x => x.Name)
            .ToDictionary(g => g.Key, g => new ReceiverTempDataValue(g.First().Value, g.First().Timestamp));
    },
    isAutoLoadNodeConfig: true);
```

更多 MQTT 配置示例见 [doc/readme.md](doc/readme.md)。

---

## 配置说明

### DataReceiverOption（所有协议的公共配置）

| 属性 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `ProductLineName` | `string` | - | 业务关联的产线名称 |
| `RefLineNames` | `string[]` | - | 需要联动复制的产线名称 |
| `OriginName` | `string` | - | 数据源用户名 |
| `OriginPwd` | `string` | - | 数据源密码 |
| `OriginHost` | `string` | - | 数据源 Host |
| `OriginPort` | `int?` | - | 数据源端口 |
| `ReConnectPeriod` | `int` | `30000` | 重连间隔（毫秒） |
| `ReConnectTimes` | `int` | `3` | 重连次数 |
| `ConnectTimeout` | `int` | `60000` | 连接超时（毫秒） |
| `ReceiverName` | `string` | - | 接收器名称 |
| `NodeFile` | `string` | - | 点位配置文件路径 |
| `HeartBeat` | `DataReadItem` | - | 心跳点位配置 |
| `AutoConnect` | `bool` | `false` | 是否在构造后自动连接 |

协议扩展配置：

- **Modbus RTU**：`ComPort`（串口号）、`BaudRate`（默认 `9600`）、`Parity`（默认 `None`）、`DataBits`（默认 `8`）、`StopBit`（默认 `One`）
- **Siemens S7**：`CpuType`（默认 `S71200`）、`Rack`（默认 `0`）、`Slot`（默认 `2`）
- **MQTT**：`DataInputTopics`（订阅的 Topic 列表）

### NodeItem（点位配置）

| 属性 | 说明 |
| --- | --- |
| `Address` | 业务地址（缓存与订阅的 Key） |
| `FullAddress` | 设备完整地址 |
| `ValueType` | 值类型，对应 `TypeCode` 枚举（JSON 中用字符串，如 `Int32`、`Boolean`） |
| `Descript` | 描述 |
| `GroupName` | 分组名称 |
| `Interval` | 定时读取间隔（毫秒，主动轮询专用） |
| `Count` | 读取长度，默认 `1` |

`ValueType` 支持：`Boolean`、`Char`、`SByte`、`Byte`、`Int16`、`UInt16`、`Int32`、`UInt32`、`Int64`、`UInt64`、`Single`、`Double`、`Decimal`、`DateTime`、`String`。

> 采集值与配置类型不匹配时，该条数据会被丢弃并记录警告日志。

### 地址格式

- **Modbus**：`<从站号>.<读写类型>.<起始地址>`，例如：
  - `1.ReadInputRegisters2.0`（读取 2 个寄存器的 32 位值）
  - `1.Coils.4`
  - `1.HoldingRegistersDouble.6`
- **Siemens S7**：以 PLC 地址表达，例如 `DB1.DTL126`、`DB102.DDT504`
- **OPC UA**：标准 NodeId，例如 `ns=2;s=Tag`

Modbus 读写类型包含大小端与字节交换变体（`HoldingRegisters2ByteSwap`、`HoldingRegistersFloatLittleEndian` 等），详见 [ModbusReadWriteType](src/iml6yu.DataReceive/iml6yu.DataReceive.ModbusMaster/DataTypes/Emuns.cs)。

---

## 核心 API

`IDataReceiver` 主要成员：

| 成员 | 说明 |
| --- | --- |
| `State` | 当前状态：`Reading` / `Ready` / `Working` / `Stoped` / `Error` / `Uninitialized` |
| `Name` | 接收器名称（取自 `ReceiverName`） |
| `IsConnected` | 是否已连接 |
| `LoadConfig(nodes)` / `LoadConfigAsync(file)` | 加载点位配置 |
| `ConnectAsync()` / `DisConnectAsync()` | 连接 / 断开 |
| `VerifyConnect()` | 校验当前连接状态 |
| `StartWorkAsync(token)` / `StopWorkAsync()` | 启动 / 停止采集与自动重连 |
| `ReadAllAsync()` | 读取缓存中的全部有效点位 |
| `ReadAsync(address)` / `ReadsAsync(addresses)` | 读取缓存中的指定点位 |
| `DirectReadAsync(items)` | 直接读取设备，不经过缓存 |
| `WriteAsync(contract)` / `WriteAsync(item)` / `WriteAsync<T>(address, value)` | 写入数据 |
| `WriteWithVerifyAsync(contract)` | 写入后读取校验（效率较低，但可确保准确性） |
| `Subscribe(key, addresses)` / `UnSubscrite(key)` | 订阅 / 取消订阅 |
| `HeartBeatHealthy()` | 心跳健康检查 |
| `SetDataParse(parser)` | 设置字符串数据解析器（MQTT 等场景） |

返回值统一使用 `iml6yu.Result` 中的 `MessageResult`、`DataResult<T>`、`CollectionResult<T>`，便于判断成功状态与错误信息。

如需按程序集与类型名动态创建接收器，可使用 `DataReceiverFactory.Create(...)`。

---

## 事件模型

| 事件 | 参数 | 触发时机 |
| --- | --- | --- |
| `ConnectionEvent` | `ConnectArgs` | 连接状态变化 |
| `ErrorEvent` | `ExceptionArgs` | 类库内部发生异常 |
| `WarnEvent` | `WarnArgs` | 类库内部发生警告 |
| `DataChangedEvent` | `DataReceiveContract` | 点位值发生变化 |
| `DataIntervalEvent` | `DataReceiveContract` | 按点位 `Interval` 定时读取 |
| `DataSubscribeEvent` | `DataReceiveContract` | 订阅的点位值发生变化，`sender` 为订阅 Key |

数据契约 `DataReceiveContract`：

| 属性 | 说明 |
| --- | --- |
| `Id` | 唯一 ID，可用于重传判定 |
| `Key` | 产线 / 业务分组名称 |
| `Timestamp` | 毫秒时间戳 |
| `Datas` | `DataReceiveContractItem` 列表（`Address`、`Value`、`ValueType`、`Timestamp`） |

---

## 详细使用规则

> 本节给出各协议的核心使用规则摘要。完整的项目骨架、WinForms / 控制台 / Windows 服务三种宿主写法、现场踩坑清单与二次开发示例，请查阅 `src/iml6yu.DataReceive/Docs` 下的系列文档：

| 文档 | 主要内容 |
| --- | --- |
| [00 汇总：一套代码打通 Modbus / S7 / OPC UA / MQTT](src/iml6yu.DataReceive/Docs/00-汇总-iml6yu.DataReceive多协议数据采集类库.md) | 架构、包结构、生命周期、协议选型、扩展开发 |
| [01 Modbus TCP 数据采集](src/iml6yu.DataReceive/Docs/01-ModbusTCP数据采集.md) | 地址格式、读取类型全表、四种端序、合并读取、写入 |
| [02 Modbus RTU 数据采集](src/iml6yu.DataReceive/Docs/02-ModbusRTU数据采集.md) | 串口参数、单循环调度、485 现场问题 |
| [03 西门子 S7 数据采集](src/iml6yu.DataReceive/Docs/03-西门子S7数据采集.md) | DB 块地址、扩展数据类型、PDU、Rack/Slot |
| [04 OPC UA 数据采集](src/iml6yu.DataReceive/Docs/04-OPCUA数据采集.md) | NodeId、URL、安全策略、批量读取 |
| [05 MQTT 数据采集](src/iml6yu.DataReceive/Docs/05-MQTT数据采集.md) | 主题、三种报文格式、发布写入 |

### 通用规则

| 规则 | 说明 |
| --- | --- |
| 停止方式 | `StartWorkAsync` 传入的 `CancellationToken` 是唯一可靠的停止手段；`StopWorkAsync()` 只负责断开与释放，不取消 token |
| 不要重复启动 | 重复调用 `StartWorkAsync` 会启动多套采集循环，导致设备请求量翻倍 |
| `Address` 唯一 | `address` 是缓存与事件的 Key，必须全局唯一；重复会导致配置加载失败 |
| `GroupName` 非空 | 分组字典以 `groupName` 为键，空值会导致配置加载失败 |
| 间隔下限 | `interval = 0` 视为 500ms；不要小于设备响应时间，否则请求堆积 |
| 类型匹配 | `valueType` 必须与协议读取类型一致，否则数据被丢弃并记录“数据已丢弃”警告 |
| 事件要快 | 事件在后台线程执行，耗时逻辑请转投自己的队列；通道容量 5000，满时丢最旧数据并告警 |
| 主动读取 | `DirectReadAsync` 会直连设备，不要放在高频循环中，避免与轮询争抢连接 |
| 健康判断 | 不要只看 `IsConnected` / `State`，应结合缓存时间戳与 `HeartBeatHealthy()` 综合判断 |

### Modbus TCP

- **地址格式**：`<从站号>.<ReadType>.<起始地址>`，例如 `1.Coils.0`、`1.HoldingRegisters.100`、`2.HoldingRegistersFloat.200`、`1.ReadInputRegisters2.0`。
- **地址基准**：起始地址为 **0 基**（手册中的 `40001` → `0`）；`ReadType` 大小写敏感，必须与枚举名完全一致。
- **读取类型**：1 位（`Coils` / `Inputs`）、16 位（`HoldingRegisters` / `ReadInputRegisters`）、32 位（`*2` / `*Float`）、64 位（`*4` / `*Double`）。
- **端序选择**：`ABCD`（大端）、`BADC`（字内交换）、`DCBA`（完整小端）、`CDAB`（低字在前）。建议用已知值逐种试读确认。
- **类型匹配**：1 位 → `Boolean`；16 位 → `Int16` / `UInt16`；32 位 → `Int32` / `UInt32` / `Single`；64 位 → `Int64` / `UInt64` / `Double`。
- **合并读取**：同一 `groupName` + `interval` 下按“站号 + 读取类型”排序，地址连续时合并为一次请求；地址重叠的点位会被跳过。32 位点位间隔 2，64 位间隔 4。
- **写入规则**：`Coils` 传 `bool`；`HoldingRegisters` 传 `ushort` 走单寄存器写；`*Float*` 必须传 `float`，`*Double*` 必须传 `double`；`Inputs` 与 `ReadInputRegisters*` 只读。
- **限制**：单次读取线圈上限 2000、寄存器上限 125；`count` 字段对 Modbus 无效。

详见 [01 Modbus TCP 数据采集](src/iml6yu.DataReceive/Docs/01-ModbusTCP数据采集.md)。

### Modbus RTU

- **地址格式**：与 TCP 完全一致：`<从站号>.<ReadType>.<起始地址>`，同样 0 基。
- **串口参数**：`comPort`、`baudRate`（默认 9600）、`parity`（默认 None）、`dataBits`（默认 8）、`stopBit`（默认 One），必须与设备逐项一致。
- **调度策略**：串行总线不能并发，RTU 为**单循环顺序执行**；等待时间取所有点位中最小且大于 0 的 `interval`，未配置时默认 500ms。
- **总线约束**：一条总线只能有一个主站实例；同一总线站号不能重复；长距离或高波特率需在两端接 120Ω 终端电阻。
- **写入规则**：与 TCP 相同。
- **选型提示**：若现场是“485 转以太网”的串口服务器，请改用 `ModbusMasterTCP` 并通过从站号路由，不要用 RTU 打开虚拟串口。

详见 [02 Modbus RTU 数据采集](src/iml6yu.DataReceive/Docs/02-ModbusRTU数据采集.md)。

### Siemens S7

- **地址体系**：`fullAddress` 为 S7 物理地址，`address` 为业务键。DB 区示例：`DB1.DBX0.0`（位）、`DB1.DBB2`（字节）、`DB1.DBW4`（字）、`DB1.DBD6`（双字）、`DB1.SRL10`（Real）、`DB1.LRL14`（LReal）、`DB1.DDT18`（日期时间）、`DB1.STR48`（字符串）。
- **非 DB 区**：`I0.0` / `IB0` / `IW0` / `ID0`、`Q0.0` / `QB0` / `QW0` / `QD0`、`M0.0` / `MB0` / `MW0` / `MD0`、`T1`、`C1`。
- **类型匹配**：`DBX` → `Boolean`；`DBB` → `Byte`；`DBW` → `Int16` / `UInt16`；`DBD` → `Int32` / `UInt32`；`SRL` → `Single`；`LRL` → `Double`。
- **`count` 的作用**：仅字符串 / 数组需要配置读取长度，普通数值点位保持默认 `1`。
- **轮询方式**：按 `groupName` + `interval` 分组，每个循环用 `ReadMultipleVarsAsync` 一次读取全组；建议 `interval >= 200ms`，点位多时按组拆分以避开 PDU 限制。
- **写入规则**：`Bit` 写 `bool`、`Real` 写 `float`、`LReal` 写 `double`、`Word` 写 `short` / `ushort`；严格写入时标志位最多 1 个。
- **现场要点**：S7-1200/1500 需开启 PUT/GET 通信；DB 需取消“优化的块访问”；`DBD` 不是浮点，浮点必须用 `SRL`。

详见 [03 西门子 S7 数据采集](src/iml6yu.DataReceive/Docs/03-西门子S7数据采集.md)。

### OPC UA

- **地址格式**：`fullAddress` 即 NodeId，推荐字符串标识（如 `ns=2;s=Demo.Line1.Speed`）；也支持 `ns=2;i=1234` 等数字标识。
- **连接地址**：`originHost` 填写完整 URL（如 `opc.tcp://192.168.1.10:4840`），不要再设置 `originPort`，否则会拼接出非法地址。
- **认证方式**：匿名，或同时配置 `originName` + `originPwd`；默认安全策略为 `None`，仅适用于内网。
- **读取方式**：按 `groupName` + `interval` 分组轮询批量 `Read`，一次请求节点数受服务端 `MaxNodesPerRead` 限制，单组建议不超过 500 个节点。
- **类型匹配**：读取结果按 `valueType` 校验，支持 `Boolean` / `Int16` / `UInt16` / `Int32` / `UInt32` / `Int64` / `UInt64` / `Single` / `Double` / `DateTime` / `String`。
- **写入规则**：`WriteWithVerifyAsync` 在 OPC UA 下不会额外回读校验，强一致场景请写入后自行 `DirectReadAsync` 验证。
- **现场要点**：当前实现为轮询而非订阅；事件时间戳是本地接收时间；默认自动接受不受信任证书，生产环境建议改为人工信任。

详见 [04 OPC UA 数据采集](src/iml6yu.DataReceive/Docs/04-OPCUA数据采集.md)。

### MQTT

- **推送模型**：`StartWorkAsync` 只负责连接、保活与取消时断开；数据节奏由设备发布频率决定，`interval` 不产生轮询。
- **点位白名单**：`fullAddress` 对应报文中的地址，`address` 是系统业务键；一旦加载点位配置，未配置的地址会被丢弃。
- **事件 Key**：始终是 `productLineName`；`groupName` 仅用于把一条报文拆成多批投递。
- **报文格式**：`Mqtt.Default` 接收 `DataReceiveContract` JSON；`Mqtt.InPlant` 接收中控 `RTValue` 数组（时间戳为秒）；也可通过 `AddMqttReceiver(option, dataParse, ...)` 自定义解析。
- **写入即发布**：`WriteAsync<T>(topic, data)` 发布序列化后的对象；`WriteAsync(DataWriteContract)` 以 `Key` 作为主题；`WriteOneAsync` 未实现。
- **现场要点**：同一报文重复地址只取第一条；QoS 与 ClientId 未开放配置，需要时使用 `receiver.Client` 或继承扩展；Broker ACL 需同时允许订阅与发布。

详见 [05 MQTT 数据采集](src/iml6yu.DataReceive/Docs/05-MQTT数据采集.md)。

---

## 数据服务（DataService）

`IDataService` 用于将数据以标准协议对外提供：

| 成员 | 说明 |
| --- | --- |
| `Name` / `IsRuning` | 服务名称 / 运行状态 |
| `StartServicer(token)` / `StopServicer()` | 启动 / 停止服务 |
| `WriteAsync(contract)` / `WriteAsync(item)` / `WriteAsync<T>(address, value)` | 写入服务端数据 |

### Modbus TCP 从站

```csharp
using iml6yu.DataService.Core.Configs;
using iml6yu.DataService.ModbusTCP;
using iml6yu.DataService.ModbusTCP.Configs;

var option = new DataServiceModbusOptionTCP
{
    IPAddress = "0.0.0.0",
    Port = 502,
    ServiceName = "ModbusSlave",
    Storages = new List<DataServiceStorageOption>
    {
        new()
        {
            Id = "1", // 从站号
            Heart = new HeartOption
            {
                HeartType = HeartType.Number,
                HeartAddress = "1.HoldingRegisters.0",
                HeartInterval = 1
            },
            DefaultValues = new List<DataServiceStorageDefaultObjectItem>
            {
                new() { Address = "1.HoldingRegisters.1", DefaultValue = 100, ValueType = TypeCode.Int32 }
            }
        }
    }
};

var service = new DataServiceModbusTCP(option);
service.StartServicer(CancellationToken.None);
```

地址格式与采集端一致：`<从站号>.<读写类型>.<起始地址>`。

### OPC UA 服务端

`appsettings.json`：

```json
{
  "option": {
    "IPAddress": "localhost",
    "Port": 4840,
    "ServiceName": "Test",
    "Storages": [
      {
        "Id": "DataServiceTest001",
        "Heart": {
          "HeartType": "Number",
          "HeartAddress": "ns=2;HeartNumber",
          "HeartInterval": 1000
        },
        "DefaultValues": [
          { "Address": "ns=2;TestBoolean1", "DefaultValue": true, "ValueType": "Boolean" },
          { "Address": "ns=2;TestInt1", "DefaultValue": 1, "ValueType": "Int32" },
          { "Address": "ns=2;TestDouble1", "DefaultValue": 1.0001234, "ValueType": "Double" },
          { "Address": "ns=2;TestString", "DefaultValue": "iml6yu", "ValueType": "String" }
        ]
      }
    ]
  }
}
```

心跳类型：

- `Number`：数字递增，到达最大值后归零
- `OddAndEven`：奇偶交替（0 / 1）
- `Time`：写入当前时间的分秒数字

---

## 示例项目

| 示例 | 说明 |
| --- | --- |
| [iml6yu.DataReceiverExample.S7](src/examples/iml6yu.DataReceiverExample) | 西门子 S7 采集（控制台 + `BackgroundService`） |
| [iml6yu.DataReceiverExample.ModbusTCP](src/examples/iml6yu.DataReceiverExample.ModbusTCP) | Modbus TCP 采集与读写 |
| [iml6yu.DataReceiverExample.ModbusRtu](src/examples/iml6yu.DataReceiverExample.ModbusRtu) | Modbus RTU 串口采集 |
| [iml6yu.DataReceiverExample.S7.Winform](src/examples/iml6yu.DataReceiverExample.S7.Winform) | WinForm 界面演示 |
| [iml6yu.DataServiceExample.ModbusTCP](src/examples/iml6yu.DataServiceExample.ModbusTCP) | Modbus TCP 从站服务（WinForm） |
| [iml6yu.DataServiceExample.OpcUa](src/examples/iml6yu.DataServiceExample.OpcUa) | OPC UA 服务端示例 |

运行示例：

```bash
dotnet run --project src/examples/iml6yu.DataReceiverExample.ModbusTCP
```

---

## 构建

```bash
dotnet restore src/iml6yu.Data.sln
dotnet build src/iml6yu.Data.sln -c Release
```

类库默认多目标框架编译（`net6.0` ~ `net10.0`），示例项目大多面向 `net8.0`（部分 WinForm 示例为 `net8.0-windows` 或 `net48`）。

---

## 常见问题

**Q：采集没有数据？**

检查点位 `Address` / `FullAddress` 是否与设备地址一致，`ValueType` 是否匹配；值类型不一致时数据会被丢弃并输出警告日志。

**Q：如何只接收变化的数据？**

监听 `DataChangedEvent`；需要按业务分组接收时使用 `Subscribe(key, addresses)` + `DataSubscribeEvent`。

**Q：如何定时上报？**

为点位配置 `Interval`，监听 `DataIntervalEvent`。该事件会按周期返回本次采集到的有效点位。

**Q：MQTT 采集收不到数据？**

确认 `DataInputTopics` 已配置、`dataParse` 解析结果以点位地址为 Key；默认 MQTT 包要求消息体为 `DataReceiveContract` JSON 格式。

---

## 贡献

欢迎通过 [Issue](https://github.com/iml6yu-tool/data_receiver/issues) 反馈问题或提出需求：

1. Fork 本仓库并新建分支：`feature/your-feature`
2. 保持代码风格与现有实现一致，补充必要注释
3. 提交 Pull Request，并说明改动内容与验证方式

---

## 许可证

本项目基于 [MIT License](LICENSE) 开源。

- GitHub：<https://github.com/iml6yu-tool/data_receiver>
- Gitee：<https://gitee.com/iml6yu/data_receiver>
