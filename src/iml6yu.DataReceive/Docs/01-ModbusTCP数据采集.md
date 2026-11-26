# Modbus TCP 数据采集实战：从地址类型到端序，一次讲透 iml6yu.DataReceive.ModbusMasterTCP

> 关键词：Modbus TCP、Modbus 地址、寄存器端序、ABCD/BADC/CDAB/DCBA、NModbus、.NET、数据采集

## 前言

Modbus TCP 是最"看起来简单、实际最容易出错"的工业协议：报文结构简单，但真正落到产线上，十次对接有八次卡在这三个问题上——

1. 设备手册写 `40001`，程序里读 `1` 还是 `0`？
2. 一个 32 位整数，为什么读出来是 `65536` 的倍数，或者是 `-1.7e38` 这种离谱的浮点数？
3. 写 `float` 的时候，明明写了 `12.5`，PLC 里看到的是乱码？

这三个问题本质上都是"地址语义 + 字节序"。本文基于 `iml6yu.DataReceive.ModbusMasterTCP`，把 Modbus 的地址模型、本项目支持的 34 种读取类型、四种端序的对应关系、批量合并规则、写入规则，以及一份可直接跑的完整用例讲清楚。

---

## 一、适用场景与业务环境

**典型设备**：Modbus TCP 网关（把 485 设备转以太网）、变频器、伺服驱动器、智能电表、称重仪表、扫码枪、温控器、远程 IO 模块。

**典型业务环境**：

| 场景 | 说明 |
| --- | --- |
| 产线数据采集 | 上位机 / 边缘网关按固定周期读取设备状态、工艺参数、报警字 |
| 设备联网改造 | 老设备通过 Modbus TCP 网关接入 MES / SCADA |
| 参数下发 | 上位机写入配方、设定值、启停命令 |
| 多设备汇总 | 一个采集服务同时管理几十台设备，按产线分组、按频率分时 |

**选择 TCP 而不是 RTU 的条件**：现场有以太网、设备或网关支持 502 端口、单次通信实时性要求不高（毫秒~百毫秒级）。

**选择本类库的理由**：地址配置化、分组分时轮询、自动重连、读缓存、变化事件、批量写入与写后校验，这些都不用自己再写一遍。

---

## 二、安装与最小接入

```bash
dotnet add package iml6yu.DataReceive.ModbusMasterTCP
```

```csharp
using iml6yu.DataReceive.ModbusMasterTCP;
using iml6yu.DataReceive.ModbusMasterTCP.Configs;

var builder = Host.CreateApplicationBuilder(args);

DataReceiverModbusTCPOption option =
    builder.Configuration.GetSection("DataReceiverOption").Get<DataReceiverModbusTCPOption>();

// 第二参数 true：自动加载点位；第三个参数传 null：从 option.NodeFile 读取
builder.Services.AddReceiver(option, true, null);

builder.Services.AddHostedService<Worker>();
builder.Build().Run();
```

也可以不落文件，直接在代码里传点位：

```csharp
var nodes = new List<NodeItem>
{
    new() { FullAddress = "1.HoldingRegisters2.0", Address = "L01.Speed", ValueType = TypeCode.Int32, GroupName = "L01", Interval = 300 },
    new() { FullAddress = "1.Coils.0",            Address = "L01.Running", ValueType = TypeCode.Boolean, GroupName = "L01", Interval = 300 }
};
builder.Services.AddReceiver(option, true, nodes);
```

> `isAutoLoadNodeConfig = false` 时不会加载任何点位，需要自己调用 `LoadConfig(nodes)` 或 `LoadConfigAsync(file)`，适合点位来自数据库/远程配置的场景。

---

## 三、连接配置

```json
{
  "DataReceiverOption": {
    "originHost": "192.168.1.10",
    "originPort": 502,
    "reConnectPeriod": 10000,
    "reConnectTimes": 3,
    "connectTimeout": 60000,
    "receiverName": "L01_ModbusTCP",
    "productLineName": "L01",
    "nodeFile": "config/L01_nodes.json",
    "autoConnect": false,
    "heartBeat": {
      "address": "L01.Heartbeat",
      "valueType": "UInt16"
    }
  }
}
```

| 配置 | 说明 |
| --- | --- |
| `originHost` / `originPort` | 设备或网关 IP / 端口，Modbus TCP 默认 502 |
| `reConnectPeriod` | 断线重连间隔（ms），默认 30000 |
| `connectTimeout` | 连接超时（ms），默认 60000 |
| `heartBeat` | 心跳点位（取缓存值判断是否刷新），配合 `HeartBeatHealthy()` 使用 |
| `productLineName` | 事件与消息通道的 Key |
| `autoConnect` | 构造时立即异步连接；一般保持 false，由 `StartWorkAsync` 的重连循环负责 |

> `ConnectAsync` 失败时会主动断开并重建 `TcpClient`/`IModbusMaster`，所以网关重启后不需要重启服务。

---

## 四、点位配置与地址类型（重点）

### 4.1 地址格式

```text
slaveAddress.ReadType.Bit
```

| 段 | 类型 | 说明 |
| --- | --- | --- |
| `slaveAddress` | `byte` | 从站号 / 单元号，0~255。直连设备通常为 1；网关后面挂多个 485 设备时用于路由 |
| `ReadType` | 枚举 | 读取类型，见下表，**大小写敏感**（与枚举名一致） |
| `Bit` | `ushort` | 起始地址，**0 基**（不是 4xxxx 的 1 基） |

示例：

```text
1.Coils.0                      站号 1，线圈 0
1.HoldingRegisters.100         站号 1，保持寄存器 100（16 位）
2.HoldingRegistersFloat.200    站号 2，保持寄存器 200 起的 2 个寄存器，按 float 解析
1.ReadInputRegisters2.0        站号 1，输入寄存器 0 起的 2 个寄存器，按 32 位整数解析
```

> 设备手册中 `40001`、`30001` 是 1 基编号，需要减 1：`40001 → 0`，`40100 → 99`。
> 分隔符支持英文句点 `.` 和中文句号 `。`，但强烈建议统一用 `.`。

### 4.2 读取类型全表

按"位数 / 寄存器数量"分四类，端序在 4.3 节单独解释。

**1 位（线圈 / 离散输入）**

| ReadType | 长度 | 说明 |
| --- | --- | --- |
| `Coils` | 1 bit | 可读可写，开关量输出，如继电器、启停 |
| `Inputs` | 1 bit | 只读，开关量输入，如传感器到位信号 |

**16 位（单寄存器）**

| ReadType | 长度 | 说明 |
| --- | --- | --- |
| `HoldingRegisters` | 16 bit / 1 个寄存器 | 保持寄存器，可读写 |
| `ReadInputRegisters` | 16 bit / 1 个寄存器 | 输入寄存器，只读 |

**32 位（2 个寄存器）**

| ReadType（保持寄存器） | ReadType（输入寄存器） | 字节布局 | 说明 |
| --- | --- | --- | --- |
| `HoldingRegisters2` | `ReadInputRegisters2` | ABCD（大端） | 32 位整数/无符号，高字在前 |
| `HoldingRegisters2ByteSwap` | `ReadInputRegisters2ByteSwap` | BADC | 高字在前，字内字节交换 |
| `HoldingRegistersLittleEndian2` | `ReadInputRegistersLittleEndian2` | DCBA | 低字在前，字内小端（完整小端） |
| `HoldingRegistersLittleEndian2ByteSwap` | `ReadInputRegistersLittleEndian2ByteSwap` | CDAB | 低字在前，字内大端 |
| `HoldingRegistersFloat` | `ReadInputRegistersFloat` | ABCD | 32 位浮点，大端 |
| `HoldingRegistersFloatByteSwap` | `ReadInputRegistersFloatByteSwap` | BADC | 32 位浮点，字内字节交换 |
| `HoldingRegistersFloatLittleEndian` | `ReadInputRegistersFloatLittleEndian` | DCBA | 32 位浮点，完整小端 |
| `HoldingRegistersFloatLittleEndianByteSwap` | `ReadInputRegistersFloatLittleEndianByteSwap` | CDAB | 32 位浮点，低字在前、字内大端 |

**64 位（4 个寄存器）**

| ReadType（保持寄存器） | ReadType（输入寄存器） | 字节布局 | 说明 |
| --- | --- | --- | --- |
| `HoldingRegisters4` | `ReadInputRegisters4` | ABCD EFGH | 64 位整数，大端 |
| `HoldingRegisters4ByteSwap` | `ReadInputRegisters4ByteSwap` | BADC FEHG | 高字在前，字内字节交换 |
| `HoldingRegistersDouble` | `ReadInputRegistersDouble` | ABCD EFGH | 64 位浮点，大端 |
| `HoldingRegistersDoubleByteSwap` | `ReadInputRegistersDoubleByteSwap` | BADC FEHG | 64 位浮点，字内字节交换 |
| `HoldingRegistersLittleEndian4` | `ReadInputRegistersLittleEndian4` | HGFE DCBA | 64 位整数，完整小端 |
| `HoldingRegistersLittleEndian4ByteSwap` | `ReadInputRegistersLittleEndian4ByteSwap` | FEHG CDAB | 低字在前，字内大端 |
| `HoldingRegistersDoubleLittleEndian` | `ReadInputRegistersDoubleLittleEndian` | HGFE DCBA | 64 位浮点，完整小端 |
| `HoldingRegistersDoubleLittleEndianByteSwap` | `ReadInputRegistersDoubleLittleEndianByteSwap` | FEHG CDAB | 64 位浮点，低字在前、字内大端 |

### 4.3 四种端序怎么选

寄存器只有 16 位，一个 32 位值必须拆成两个寄存器，拆分顺序就是"字序"；每个寄存器内部还有两个字节，排列顺序就是"字节序"。两者组合出四种：

| 布局 | 高/低字 | 字内字节 | 十六进制字节序（从低地址到高地址） |
| --- | --- | --- | --- |
| ABCD | 高字在前 | 大端 | `AB CD` / `AB CD EF GH` |
| BADC | 高字在前 | 交换 | `BA DC` / `BA DC FE HG` |
| DCBA | 低字在前 | 小端 | `DC BA` / `HG FE DC BA` |
| CDAB | 低字在前 | 大端 | `CD AB` / `FE HG CD AB` |

**现场判断方法**：读一个已知值（例如设备里固定是 `1` 或 `100`），四种类型各读一次，哪个对上用哪个。常见设备的默认习惯：

- 西门子、施耐德多数为 ABCD（大端）；
- 部分国产仪表、欧姆龙为 CDAB 或 BADC；
- 组态软件里标注 "Float Reverse" / "Word Swap" 的，通常就是 BADC 或 CDAB。

### 4.4 ValueType 必须与读取类型匹配

| 读取类型族 | 寄存器数 | 可配 `valueType` | 配错的结果 |
| --- | --- | --- | --- |
| `Coils` / `Inputs` | 1 bit | `Boolean` | — |
| `HoldingRegisters` / `ReadInputRegisters` | 1 | `Int16` / `UInt16` | 配 `Boolean` 会被类型校验丢弃，日志提示"数据类型不一致" |
| 所有 32 位 `*2` / `*Float` | 2 | `Int32` / `UInt32` / `Single` | 配 `Int16`/`UInt16`/`Double` 会得到 `0` |
| 所有 64 位 `*4` / `*Double` | 4 | `Int64` / `UInt64` / `Double` | 配 `Single`/`Int32` 会得到 `0` |

> 提醒：`HoldingRegisters` 是 16 位，不是 1 位；README 早期版本这里写成了 `1bit`，属于笔误，以本文表格为准。

### 4.5 批量合并读取规则

同一个 `groupName` + 同一个 `interval` 下，点位会按 `站号 + 读取类型` 排序，然后尽量合并：

- 地址**正好**等于上一个地址 + 寄存器数量（1/2/4）时，合并进同一次请求；
- 中间有空洞时，从空洞处拆成两次请求；
- 地址重叠（小于上一个地址 + 寄存器数量）会记录错误并**跳过该点位**：

```text
1.HoldingRegisters2.0   → 占用 0、1
1.HoldingRegisters2.1   → 与上一个重叠，配置错误，被跳过
```

所以配置多寄存器点位时，地址间隔要按寄存器数量递增：`0、2、4、6...`（32 位），`0、4、8、12...`（64 位）。

另外，Modbus 协议本身对单次读取数量有上限（线圈 2000 个、寄存器 125 个），一个间隔内点位过多时请拆分组，避免整包失败。

---

## 五、完整用例

### 5.1 项目结构

```text
L01Collector/
├─ Program.cs
├─ Worker.cs
├─ appsettings.json
└─ config/
   └─ L01_nodes.json
```

### 5.2 Program.cs

```csharp
using iml6yu.DataReceive.ModbusMasterTCP;
using iml6yu.DataReceive.ModbusMasterTCP.Configs;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss.fff ");

DataReceiverModbusTCPOption option =
    builder.Configuration.GetSection("DataReceiverOption").Get<DataReceiverModbusTCPOption>();

builder.Services.AddReceiver(option, true, null);
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
```

### 5.3 appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Warning"
    }
  },
  "DataReceiverOption": {
    "originHost": "192.168.1.10",
    "originPort": 502,
    "reConnectPeriod": 10000,
    "reConnectTimes": 3,
    "connectTimeout": 60000,
    "receiverName": "L01_ModbusTCP",
    "productLineName": "L01",
    "nodeFile": "config/L01_nodes.json",
    "autoConnect": false,
    "heartBeat": {
      "address": "L01.Heartbeat",
      "valueType": "UInt16"
    }
  }
}
```

### 5.4 config/L01_nodes.json

```json
[
  {
    "fullAddress": "1.Coils.0",
    "address": "L01.Running",
    "valueType": "Boolean",
    "groupName": "L01",
    "interval": 200,
    "descript": "运行状态"
  },
  {
    "fullAddress": "1.Coils.1",
    "address": "L01.Alarm",
    "valueType": "Boolean",
    "groupName": "L01",
    "interval": 200,
    "descript": "总报警"
  },
  {
    "fullAddress": "1.ReadInputRegisters2.0",
    "address": "L01.Speed",
    "valueType": "Int32",
    "groupName": "L01",
    "interval": 300,
    "descript": "线速度（32 位整数，设备为大端 ABCD）"
  },
  {
    "fullAddress": "1.ReadInputRegistersFloat.2",
    "address": "L01.Temperature",
    "valueType": "Single",
    "groupName": "L01",
    "interval": 300,
    "descript": "温度（32 位浮点）"
  },
  {
    "fullAddress": "1.ReadInputRegisters4.4",
    "address": "L01.TotalWeight",
    "valueType": "Int64",
    "groupName": "L01",
    "interval": 1000,
    "descript": "累计重量（64 位整数，占用 4、5、6、7）"
  },
  {
    "fullAddress": "1.HoldingRegisters.20",
    "address": "L01.SetSpeed",
    "valueType": "UInt16",
    "groupName": "L01",
    "interval": 1000,
    "descript": "设定速度（可写）"
  },
  {
    "fullAddress": "1.HoldingRegistersFloat.22",
    "address": "L01.SetTemperature",
    "valueType": "Single",
    "groupName": "L01",
    "interval": 1000,
    "descript": "设定温度（可写，float）"
  },
  {
    "fullAddress": "1.HoldingRegisters.24",
    "address": "L01.Heartbeat",
    "valueType": "UInt16",
    "groupName": "L01",
    "interval": 1000,
    "descript": "设备心跳计数（16 位，配 UInt16）"
  }
]
```

> `heartBeat` 配置里填的是 `address`（业务键），不是 `fullAddress`；`HeartBeatHealthy()` 读的是缓存，所以心跳点位本身必须被轮询刷新。

### 5.5 Worker.cs（事件 + 缓存读取 + 主动读取 + 写入）

```csharp
using iml6yu.Data.Core.Models;
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
            logger.LogInformation("连接事件：{Connected} {Message}", e.IsConntion, e.Message);

        receiver.ErrorEvent += (_, e) =>
            logger.LogError(e.Ex, "采集异常：{Message}", e.Message);

        receiver.WarnEvent += (_, e) =>
            logger.LogWarning("采集警告：{Message}", e.Message);

        // 只有值发生变化才会触发
        receiver.DataChangedEvent += (_, e) =>
            logger.LogInformation("变化 @{Time}：{Data}",
                DateTimeOffset.FromUnixTimeMilliseconds(e.Timestamp).ToString("HH:mm:ss.fff"),
                string.Join(" | ", e.Datas.Select(d => $"{d.Address}={d.Value}")));

        // 每个采集周期都会触发（适合做实时看板刷新）
        receiver.DataIntervalEvent += (_, e) =>
        {
            // 事件在后台线程执行，不要在这里做耗时操作
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
        await Task.Delay(2000, stoppingToken);

        // 1) 读缓存：不打扰设备
        var all = await receiver.ReadAllAsync(stoppingToken);
        if (all.State)
            logger.LogInformation("缓存点位：{Count} 个", all.Data.Count);

        var one = await receiver.ReadAsync("L01.Speed", stoppingToken);
        if (one.State)
            logger.LogInformation("L01.Speed = {Value}", one.Data.Value);

        // 2) 主动读取：绕过缓存，直接访问设备
        var direct = await receiver.DirectReadAsync(
        [
            new DataReadItem { Address = "1.ReadInputRegisters2.0", ValueType = TypeCode.Int32 },
            new DataReadItem { Address = "1.ReadInputRegistersFloat.2", ValueType = TypeCode.Single }
        ], stoppingToken);
        if (direct.State)
            logger.LogInformation("主动读取：{Data}",
                string.Join(",", direct.Data.Datas.Select(d => $"{d.Address}={d.Value}")));

        // 3) 订阅：只接收关心的点位变化
        receiver.Subscribe("看板", ["L01.Speed", "L01.Temperature"]);
        receiver.DataSubscribeEvent += (key, e) =>
            logger.LogInformation("订阅[{Key}]：{Data}", key,
                string.Join(",", e.Datas.Select(d => $"{d.Address}={d.Value}")));

        // 4) 写入：单点
        var writeOne = await receiver.WriteAsync("1.HoldingRegisters.20", (ushort)800);
        logger.LogInformation("写单寄存器：{Result}", writeOne.State);

        // 5) 写入：批量 + 写后回读校验 + 最后写标志位
        var batch = await receiver.WriteWithVerifyAsync(new DataWriteContract
        {
            Id = 1,
            Key = "L01",
            Datas =
            [
                new DataWriteContractItem
                {
                    Address = "1.HoldingRegistersFloat.22", Value = 45.5f, ValueType = TypeCode.Single
                },
                new DataWriteContractItem
                {
                    Address = "1.HoldingRegisters.20", Value = (ushort)800, ValueType = TypeCode.UInt16
                },
                new DataWriteContractItem
                {
                    Address = "1.Coils.10", Value = true, ValueType = TypeCode.Boolean, IsFlag = true
                }
            ]
        });
        logger.LogInformation("批量写入并校验：{State} {Message}", batch.State, batch.Message);

        // 6) 心跳健康
        var health = await receiver.HeartBeatHealthy();
        logger.LogInformation("心跳状态：{Health}", health);

        // 7) 常驻：每 10 秒打印一次连接与缓存情况
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(10000, stoppingToken);
            logger.LogInformation("连接={Connected} 状态={State}", receiver.IsConnected, receiver.State);
        }
    }
}
```

### 5.6 运行效果

```text
14:02:11.203 连接事件：True success
14:02:11.405 变化 @14:02:11.405：L01.Running=True | L01.Alarm=False
14:02:11.506 变化 @14:02:11.506：L01.Speed=1520 | L01.Temperature=36.8
14:02:12.512 缓存点位：9 个
14:02:12.514 L01.Speed = 1520
14:02:12.702 批量写入并校验：True
14:02:12.703 心跳状态：Healthy
```

---

## 六、注意事项（现场踩坑清单）

1. **地址 0 基**：NModbus 使用协议地址（PDU 地址），手册里的 `40001/30001` 要减 1。
2. **站号是 `byte`**：`0~255`，超出会配置错误；网关场景下站号决定报文落到哪个 485 设备。
3. **`ReadType` 跟枚举名完全一致**：`HoldingRegisters`、`HoldingRegistersFloat`、`ReadInputRegisters2ByteSwap`，拼错会在加载配置时被丢弃并记录日志。
4. **多寄存器类型要按寄存器数量留间隔**：32 位占 2 个、64 位占 4 个，重叠点位会被跳过。
5. **端序只能靠试**：先用已知值验证 ABCD/BADC/DCBA/CDAB，再批量配置；配错时浮点数通常表现为极小值或极大值。
6. **`valueType` 必须匹配**：32 位只能配 `Int32/UInt32/Single`，64 位只能配 `Int64/UInt64/Double`，否则读出来是 0；16 位不要配 `Boolean`。
7. **`Inputs` 和 `ReadInputRegisters*` 只读**：写入会返回 `read-only, cannot write`。
8. **`TcpClient.Connected` 不代表链路可用**：拔网线后它可能仍为 `true`。判断在线请结合 `HeartBeatHealthy()` 和缓存时间戳，而不是只看 `IsConnected`。
9. **TCP 是分组并行读取**：不同 `groupName`、不同 `interval` 的循环并行执行（默认并行度受线程池/CPU 影响），它们共用同一个 `IModbusMaster`。网关并发能力有限时，建议减少分组数量，或把高频点位放在同一个分组同一个间隔。
10. **单次读取数量上限**：线圈 2000、寄存器 125，超过会被设备拒绝；点位多时拆分 `groupName`。
11. **`count` 字段对 Modbus 无效**：读取长度由 `ReadType` 决定。
12. **`address` 全局唯一、`groupName` 不能为空**：否则配置加载失败，表现为"服务起来了但没有数据"。
13. **写入值类型要精确**：`Coils` 传 `bool`；`HoldingRegisters` 传 `ushort` 时走单寄存器写；`HoldingRegistersFloat` 必须传 `float`，传 `int` 会被 `Convert.ChangeType` 转换失败并返回参数错误。
14. **写后校验会多一轮读**：`WriteWithVerifyAsync` 会先写普通点位、回读比对、再写 `IsFlag` 点位，整体耗时是普通写的 2~3 倍，不要放在高频循环里。
15. **设备侧写入权限**：部分网关默认禁止写保持寄存器，需要在网关配置里打开；表现为写入超时或异常码。

---

## 七、三种宿主环境：WinForms / 控制台 / Windows 服务

### 7.1 WinForms

**项目文件（关键项）**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
  </ItemGroup>
  <ItemGroup>
    <!-- 配置文件和点位文件必须随程序输出，否则发布后读不到 -->
    <None Update="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
    <None Update="config\**\*.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

**Program.cs**

```csharp
using iml6yu.DataReceive.ModbusMasterTCP;
using iml6yu.DataReceive.ModbusMasterTCP.Configs;

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
            .Get<DataReceiverModbusTCPOption>()!;

        builder.Services.AddReceiver(option, true, null);
        builder.Services.AddSingleton<MainForm>();

        using var host = builder.Build();
        Services = host.Services;
        host.Start();                                  // 只启动容器，不阻塞

        Application.Run(Services.GetRequiredService<MainForm>());

        host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    }
}
```

**MainForm.cs（不依赖设计器，可直接编译）**

```csharp
using iml6yu.DataReceive.ModbusMasterTCP;

public sealed class MainForm : Form
{
    private readonly DataReceiverModbusTCP receiver;
    private readonly CancellationTokenSource cts = new();
    private readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly FlowLayoutPanel bar = new() { Dock = DockStyle.Top, Height = 40 };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private readonly object gate = new();
    private string[] snapshot = [];

    public MainForm(DataReceiverModbusTCP receiver)
    {
        this.receiver = receiver;

        Text = "Modbus TCP 采集调试器";
        Width = 900;
        Height = 500;

        var start = new Button { Text = "开始", Width = 90 };
        var stop = new Button { Text = "停止", Width = 90, Enabled = false };
        var write = new Button { Text = "写设定速度 800", Width = 150 };
        bar.Controls.AddRange([start, stop, write]);

        Controls.Add(list);
        Controls.Add(bar);

        start.Click += (_, _) =>
        {
            _ = receiver.StartWorkAsync(cts.Token);    // 不要 await，否则界面卡死
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
            var result = await receiver.WriteAsync("1.HoldingRegisters.20", (ushort)800);
            MessageBox.Show($"写入结果：{result.State}\r\n{result.Message}");
        };

        timer.Tick += (_, _) => RefreshList();

        // 事件在后台线程触发：只存快照，绝不直接操作控件
        receiver.DataIntervalEvent += (_, e) =>
        {
            lock (gate)
                snapshot = e.Datas.Select(d => $"{d.Address} = {d.Value}").ToArray();
        };

        receiver.ErrorEvent += (_, e) =>
            BeginInvoke(() => MessageBox.Show($"采集错误：{e.Message}"));
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
        receiver.StopWorkAsync().GetAwaiter().GetResult();   // 关闭时允许同步等待
        cts.Dispose();
        base.OnFormClosing(e);
    }
}
```

**WinForms 要点**

- 采集事件在后台线程触发，必须"存快照 + UI 线程刷新"（`Timer` 或 `Invoke`），不要跨线程直接改控件。
- 只在 `FormClosing` 里取消 + 停止；不要在不取消 token 的情况下只调 `StopWorkAsync`。
- `appsettings.json` 和点位文件要设置 `CopyToOutputDirectory`，否则发布目录里没有。
- 演示代码的 `CancellationTokenSource` 只支持一次启停；需要反复启停时，每次 `StartAsync` 新建一个 CTS。

### 7.2 控制台

```csharp
using iml6yu.DataReceive.ModbusMasterTCP;
using iml6yu.DataReceive.ModbusMasterTCP.Configs;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(b => b
    .AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss.fff ")
    .SetMinimumLevel(LogLevel.Information));

var option = new DataReceiverModbusTCPOption
{
    OriginHost = "192.168.1.10",
    OriginPort = 502,
    ReceiverName = "L01_ModbusTCP",
    ProductLineName = "L01",
    NodeFile = Path.Combine(AppContext.BaseDirectory, "config", "L01_nodes.json")
};

using var receiver = new DataReceiverModbusTCP(
    option,
    loggerFactory.CreateLogger<DataReceiverModbusTCP>(),
    isAutoLoadNodeConfig: true);

receiver.ConnectionEvent += (_, e) => Console.WriteLine($"连接：{e.IsConntion} {e.Message}");
receiver.ErrorEvent += (_, e) => Console.WriteLine($"错误：{e.Message}");
receiver.DataChangedEvent += (_, e) =>
    Console.WriteLine($"变化：{string.Join(",", e.Datas.Select(d => $"{d.Address}={d.Value}"))}");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;      // 阻止进程直接退出，走优雅停止
    cts.Cancel();
};

_ = receiver.StartWorkAsync(cts.Token);

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    // 正常退出
}

await receiver.StopWorkAsync();
Console.WriteLine("已停止");
```

> 控制台项目需要额外引用 `Microsoft.Extensions.Logging.Console`（`AddSimpleConsole` 所在包）。
> 不引用 Hosting 也能用：直接 `new` 采集器 + `LoggerFactory` 即可。

### 7.3 Windows 服务

**项目文件**

```xml
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="8.0.0" />
```

**Program.cs**

```csharp
using iml6yu.DataReceive.ModbusMasterTCP;
using iml6yu.DataReceive.ModbusMasterTCP.Configs;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(o => o.ServiceName = "L01.ModbusTCPCollector");
builder.Logging.AddEventLog(settings =>
{
    settings.SourceName = "L01.ModbusTCPCollector";
    settings.LogName = "Application";
});

// 服务的工作目录是 C:\Windows\System32，相对路径必须基于程序目录
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

var option = builder.Configuration
    .GetSection("DataReceiverOption")
    .Get<DataReceiverModbusTCPOption>()!;
option.NodeFile = Path.Combine(AppContext.BaseDirectory, option.NodeFile);

// 多设备时可适当加大停止超时，默认 30 秒
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));

builder.Services.AddReceiver(option, true, null);
builder.Services.AddHostedService<Worker>();     // Worker 直接复用第五节示例

builder.Build().Run();
```

**发布与安装**

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o C:\Services\L01Collector

sc.exe create L01.ModbusTCPCollector binPath= "C:\Services\L01Collector\L01Collector.exe" start= auto DisplayName= "L01 ModbusTCP Collector"
sc.exe description L01.ModbusTCPCollector "L01 产线 Modbus TCP 数据采集"
sc.exe failure L01.ModbusTCPCollector reset= 86400 actions= restart/5000/restart/5000/restart/5000
sc.exe start L01.ModbusTCPCollector
```

**Windows 服务要点**

- PowerShell 里 `sc` 是 `Set-Content` 的别名，必须写 `sc.exe`。
- 服务没有控制台，`AddSimpleConsole` 的日志看不到，必须加 `AddEventLog` 或文件日志。
- 把 `appsettings.json`、`config\**\*.json` 一起发布，并在代码里用 `AppContext.BaseDirectory` 拼绝对路径。
- 卸载：`sc.exe stop L01.ModbusTCPCollector` → `sc.exe delete L01.ModbusTCPCollector`。

---

## 八、扩展自己的逻辑：继承与重写

Modbus TCP 的常用扩展场景和对应方式：

| 需求 | 做法 |
| --- | --- |
| 增加配置项（如超时、重试次数） | 继承 `DataReceiverModbusTCPOption` |
| 动态点位（数据库/接口下发） | 继承 `DataReceiverModbusTCP`，重写 `LoadConfig` |
| 自定义调度（运行时高频、停机低频） | 重写 `WhileDoAsync`，使用 `protected readNodes` 与 `ReadModbusNodeItem` |
| 写入审计/重试/限流 | 重写 `WriteBatchAsync` 或 `WriteStrictlyAsync` |
| 数据旁路（同时发 Kafka/入库） | 重写 `ReceiveDataToMessageChannelAsync` |
| 特殊值换算/类型兼容 | 重写 `VerifyValue` / `VerifyValueEqual` |
| 自定义缓存合并逻辑 | 重写 `UpdateCatch` |

示例：动态点位 + 运行时变节奏 + 写入审计。

```csharp
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.ModbusMasterTCP;
using iml6yu.DataReceive.ModbusMasterTCP.Configs;
using iml6yu.Result;

public sealed class DataReceiverModbusTCPCustom : DataReceiverModbusTCP
{
    public DataReceiverModbusTCPCustom(DataReceiverModbusTCPOption option,
        ILogger<DataReceiverModbusTCPCustom> logger,
        bool isAutoLoadNodeConfig = false,
        List<NodeItem> nodes = null)
        : base(option, logger, isAutoLoadNodeConfig, nodes) { }

    // 1) 动态点位：先注入系统点位，再交给基类
    public override MessageResult LoadConfig(List<NodeItem> nodes)
    {
        var merged = new List<NodeItem>(nodes ?? []);
        merged.Add(new NodeItem
        {
            FullAddress = "1.HoldingRegisters.200",
            Address = "L01.CollectorHeartbeat",
            ValueType = TypeCode.UInt16,
            GroupName = "L01",
            Interval = 1000,
            Descript = "采集器心跳"
        });
        return base.LoadConfig(merged);
    }

    // 2) 自定义调度：运行中 100ms 一轮，停机 3s 一轮
    protected override Task WhileDoAsync(CancellationToken token) => Task.Run(async () =>
    {
        while (!token.IsCancellationRequested)
        {
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
                            }
                        }
                    }

                    if (temp.Count > 0)
                        await ReceiveDataToMessageChannelAsync(Option.ProductLineName, temp);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "自定义采集异常：{Message}", ex.Message);
            }

            var running = (await ReadAsync("L01.Running", token)).Data?.Value is true;
            await Task.Delay(running ? 100 : 3000, token);
        }
    }, token);

    // 3) 写入审计：前后各记一条日志，失败可在这里重试
    protected override async Task<MessageResult> WriteBatchAsync(DataWriteContract data)
    {
        Logger.LogInformation("写入开始：{Count} 条", data.Datas.Count());
        var result = await base.WriteBatchAsync(data);
        Logger.LogInformation("写入结束：{State} {Message}", result.State, result.Message);
        return result;
    }
}
```

注册自定义类型（替换默认的 `AddReceiver`）：

```csharp
builder.Services.AddSingleton(sp => new DataReceiverModbusTCPCustom(
    option,
    sp.GetRequiredService<ILogger<DataReceiverModbusTCPCustom>>(),
    true,
    null));
```

**扩展注意事项**

- 基类构造函数里会调用 `CreateClient`，此时派生类字段还没初始化，不要在 `CreateClient` 里依赖派生类状态。
- `ReadModbusNodeItem`、`readNodes` 是 `protected`，可以直接复用；`AddReceiveValue` 是 `private`，自定义解析时需要自己写入 `tempDatas`。
- `WriteAsync(DataWriteContract)`、`WriteWithVerifyAsync` 不是虚方法，扩展写入请重写 `WriteBatchAsync` / `WriteStrictlyAsync` / `WriteOneAsync` / `WriteAsync<T>`。
- `ReadAllAsync` 等缓存读取方法也不是虚方法，自定义缓存策略请重写 `UpdateCatch` 或在业务层聚合。

---

## 九、小结

Modbus TCP 的难点从来不在协议本身，而在"地址怎么算、字节怎么排、点位怎么合并"。把这三件事用配置固化下来，采集代码就只剩下启动、订阅事件和写参数了。

下一篇讲 Modbus RTU：同样的地址模型，但串行总线决定了它不能并发，调度策略完全不同。


---

## 交流与反馈

|Wechat| QQ邮箱|Gmail（不常用）|
|----|----|----|
|![WeChat](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAFoAAABYCAYAAAB1YOAJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsQAAA7EAZUrDhsAACLISURBVHhe7d0HtC1FES7gMYs5oKiYEROYc84RTJgDRhRUxICKmDChqIgBVAQB9aogBkSCWTGjoogiiFkwA+Zr1v3qqzO1T++5s/c5Fy5vrfeW/1qzZk93z0xPT3VV/VU955xrEuj+h3Mc6wz0P/7xj07Ruc51rr5kMbS1bbTRRn3J/3/429/+1p373Ofuj1bGf//733XHw0AXnva0pxn0s7Q961nP6q8yjje+8Y2T+93vfpN//etfeXz66adPHvCAB0ze+973TnbdddfJk570pCyHn/70p5N73OMek6OPProvmUxe+cpX5vmLcNBBB+V5f/rTn/qS+fjnP/+Zbd/znvdMnvvc5472/7TTTptc4AIXGH3elbZNNtlkcuaZZ/ZXWpLGRLy10RPWZ1uEm93sZtmmbv7tb387jw3eZS5zmZnzP/GJT+Rx+/Cbb775ive4733vm21++MMf9iXz8Zvf/CbbPuhBD5pc/OIXn1zoQhfqa5ZxdgTP9rrXva6/0mRy3ihIxO/+V9e97GUv6x7xiEd0f//73/uScUTnure85S3dnnvu2ZfMx4te9KLuy1/+cnepS10qj6973et2xx9/fHf1q1+9O+SQQ7q1a9dmOdzpTnfqjjvuuKwr7L777t13v/vd7o9//GN35JFHdve85z27S1/60ln3yU9+srvwhS/c7bzzzt01r3nN7vKXv3x38MEHd7e97W27E044obvc5S6Xz/erX/2qu8997pNq8bKXvWz3zGc+s3vwgx/c3ete98r6U045JdvHTOvOd77zTdXFxhtv3H3+85/PNu04DaH9n//85+6ud71r7v/zn//0NYE4MfHXv/51+ia++c1v9qUrIzowPW8Rtthii2xDZQyxmvOvcIUrZJvXvOY1uX/rW9+a5VSR4wte8IKTu93tbvmbJNk//vGPz/01rnGNybWuda38/fvf/z7Pi0HP43vf+96T85///JNLXvKSk3gxWfbjH/842zz96U/P41vf+tZ5vFpc5zrXyfNe+9rX9iWNRLcoSf75z3/e7bXXXl08RL5JkmD/73//O6UhHn6u1DOqod/SMHjTe+yxR/fV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)|[646007589@qq.com](mailto:646007589@qq.com)|[iml6yu@gmail.com](mailto:iml6yu@gmail.com)|
