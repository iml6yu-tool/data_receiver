# iml6yu.DataReceive.SerialCommunicate

串口数据采集类库，基于 .NET SerialPort 实现串口数据的接收和发送。

## 功能特性

- 支持标准串口配置（波特率、数据位、奇偶校验、停止位等）
- 支持数据接收和发送
- 支持自定义数据解析器
- 支持连接状态监控
- 支持自动重连机制

## 安装

```bash
Install-Package iml6yu.DataReceive.SerialCommunicate
```

## 使用示例

### 基本配置

```csharp
using iml6yu.DataReceive.SerialCommunicate;
using iml6yu.DataReceive.SerialCommunicate.Configs;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

var option = new DataReceiverSerialCommunicateOption
{
    ReceiverName = "SerialCommunicateReceiver",
    ProductLineName = "Line1",
    PortName = "COM1",
    BaudRate = 9600,
    DataBits = 8,
    Parity = System.IO.Ports.Parity.None,
    StopBits = System.IO.Ports.StopBits.One,
    AutoConnect = true
};

services.AddSerialCommunicateReceiver(option, (data) => 
{
    // 自定义数据解析逻辑
    var result = new Dictionary<string, ReceiverTempDataValue>();
    result.Add("Data", new ReceiverTempDataValue(data, DateTimeOffset.Now.ToUnixTimeMilliseconds()));
    return result;
});
```

### 手动使用

```csharp
var receiver = new DataReceiverSerialCommunicate(option, logger);
await receiver.ConnectAsync();
await receiver.StartWorkAsync(cancellationToken);
```

## 配置参数

| 参数 | 类型 | 说明 | 默认值 |
|------|------|------|--------|
| PortName | string | 串口名称 | - |
| BaudRate | int | 波特率 | 9600 |
| DataBits | int | 数据位 | 8 |
| Parity | Parity | 奇偶校验 | None |
| StopBits | StopBits | 停止位 | One |
| Handshake | Handshake | 握手协议 | None |
| ReadTimeout | int | 读取超时(ms) | 500 |
| WriteTimeout | int | 写入超时(ms) | 500 |
| ReadBufferSize | int | 接收缓冲区大小 | 4096 |
| WriteBufferSize | int | 发送缓冲区大小 | 2048 |
| RtsEnable | bool | 是否启用RTS | false |
| DtrEnable | bool | 是否启用DTR | false |

## 事件

| 事件 | 说明 |
|------|------|
| ConnectionEvent | 连接状态变化事件 |
| ErrorEvent | 错误事件 |
| WarnEvent | 警告事件 |
| DataChangedEvent | 数据变化事件 |
| DataIntervalEvent | 定时数据事件 |
| DataSubscribeEvent | 订阅数据事件 |

## 地址格式
`{Encoding}.{SerialReadWriteType}.{Index1}.{Index2}`

- Encoding：编码方式，如`None`, `UTF8`、`ASCII`,`GBK`,`Unicode`,`Custom` 等

**`Custom`编码方式系统不支持解码配置，需要自定义解码类库**

### 例子

- Bit 类型数据：

`None.Bit.0.4`

|值|说明|
|----|----|
|None|表示当前数据不需要作任何编码，直接将将byte读取出来|
|Bit|表示当前数据bit位|
|0|表示当前数据的第0个byte|
|4|表示第0个byte数据的第4个bit|

- Byte
`None.Byte.1`或者 `None.Byte.1.0`

|值|说明|
|----|----|
|None|表示当前数据不需要作任何编码，直接将byte读取出来|
|Byte|表示当前数据类型是Byte|
|1|表示当前数据的起始byte|
|0|Byte类型的时候默认最后一个就是0，可以省略|

- Short Int Double Float 等数值类型：

`None.Short.0`或者 `None.Short.0.0`
`None.Int.0`或者 `None.Int.0.0`
`None.Double.0`或者 `None.Double.0.0`
`None.Float.0`或者 `None.Float.0.0`

|值|说明|
|----|----|
|None|表示当前数据不需要作任何编码|
|Short Int Double Float|表示当前数据类型 数据长度分别对应 2byte 4byte 8byte 4byte（IEEE 754 浮点标准）|
|1|表示当前数据的起始byte|
|0|默认最后一个就是0，可以省略|

- String 类型数据：

`UTF8.String.0.10`

|值|说明|
|----|----|
|UTF8|表示当前数据的编码解码方式|
|String|表示当前数据类型 |
|1|表示当前数据的起始byte|
|10|表示当前数据的终止byte|


## 许可证

MIT License