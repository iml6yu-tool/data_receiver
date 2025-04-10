# iml6yu.DataReceive.ModbusMasterTCP

## 配置文件 appsetting.json

```json
"XXXXXX": {
    "originName": "",//源登录账号
    "originPwd": "",//源密码
    "originHost": "192.168.0.224",
    "originPort": 502,
    "reConnectPeriod": 10000, //重连间隔 单位ms
    "reConnectTimes": 2, //重连次数
    "connectTimeout": 60000, //连接超时时间 单位ms
    "receiverName": "warn_receiver", //
    "nodeFile": "ConfigFiles/NodeConfigs/test.json",
    "autoConnect": false 
  },
```

## 点位配置文件
### FullAddress配置格式说明
**格式** slaveAddress.ReadType.Bit
**说明**
- slaveAddress：站点ID，0~255
- ReadType：读取类型 一共有以下几种
	+ Coils *<span style="color:blue">用于读取和控制远程设备的开关状态，通常用于控制继电器等开关设备,Reads from 1 to 2000 contiguous coils status.</span>"*,**读取到的数据类型都是bool**
	+ HoldingRegisters *<span style="color:blue">用于存储和读取远程设备的数据，通常用于存储控制参数、设备状态等信息,Reads contiguous block of holding registers.</span>"*,**读取到的数据类型都是ushort**
	+ HoldingRegisters2 大端32位 Int32
    + HoldingRegisters2 大端64位 Int64
    + HoldingRegistersLittleEndian2 小端32位 Int32
    + HoldingRegistersLittleEndian4 小端64位 Int64
    + Inputs *<span style="color:blue">用于读取远程设备的输入状态，通常用于读取传感器等输入设备的状态, Reads from 1 to 2000 contiguous discrete input status.</span>"*,**读取到的数据类型都是bool**
	+ ReadInputRegisters *<span style="color:blue">用于存储远程设备的输入数据，通常用于存储传感器等输入设备的数据,Reads contiguous block of input registers.</span>"*,**读取到的数据类型都是ushort**
    + ReadInputRegisters2 大端32位 Int32
    + ReadInputRegisters2 大端64位 Int64
    + ReadInputRegistersLittleEndian2 小端32位 Int32
    + ReadInputRegistersLittleEndian4 小端64位 Int64
- Bit:点位，数字

**例子**
- 1.Coils.0
- 1.Coils.1
- 1.Coils.2

> 这样就是读取 SlaveAddress是1的状态线圈从0开始3个长度

