# iml6yu.DataService.ModbusRTU

iml6yu.DataService.ModbusRTU is a .NET library for slave of Modbus RTU data.

## 用法

```csharp
    ILogger logger;

	DataServiceModbusOption option = new DataServiceModbusOption
	{
		ComName = "COM1", // 串口名称
		BaudRate = 9600, // 波特率
		DataBits = 8, // 数据位
		StopBits = StopBits.One, // 停止位
		Parity = Parity.None, // 校验位
		SlaveId = 1, // 从站ID
	};

	var  dataService = new DataService.ModbusRTU.DataServiceModbusRTU(option, logger);
	//启动Slave服务
	dataService.StartServicer(token);
```

## 依赖
```xml
	<PackageReference Include="System.IO.Ports" Version="8.0.0" />
	<PackageReference Include="NModbus.Serial" Version="3.0.81" />
```

## 配置
```json
 {
      "ServiceName": "DCSExampleTCP",

      //Start: 这部分属于服务监听配置，根基实际情况配置ComName（串口RTU）或IPAddress（TCP）
      "IPAddress": null, // null表示使用本机IP地址 Ip.Any  如果配置ComName表示的是串口通信
      "Port": 502,
      //End: 这部分属于服务监听配置，根基实际情况配置ComName（串口RTU）或IPAddress（TCP）

      "Slaves": [
        {
          "Id": 1,
          "Heart": {
            "HeartType": "Number",
            "HeartAddress": "1.ReadInputRegisters.61",
            "HeartInterval": 5
          },
          //默认值 
          "DefaultValues": [
            {
              "Address": "1.Coils.0",
              "DefaultValue": false
            },
            {
              "Address": "1.Inputs.0",
              "DefaultValue": false
            },
            {
              "Address": "1.HoldingRegisters.0",
              "DefaultValue": 0
            },
            {
              "Address": "1.HoldingRegisters2.0",
              "DefaultValue": 0
            },
            {
              "Address": "1.HoldingRegisters4.0",
              "DefaultValue": 0
            },
            {
              "Address": "1.HoldingRegisters2ByteSwap.0",
              "DefaultValue": 0
            },
            {
              "Address": "1.HoldingRegisters4ByteSwap.0",
              "DefaultValue": 0
            } 
          ]
        }
      ]
    }
```

## 源码

- DataServiceModbusOption  (一个数据服务的配置)
	+ 源码
```csharp
 public class DataServiceModbusOption 
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        public required string ServiceName { get; set; }

        /// <summary>
        /// 从站列表
        /// </summary>
        public List<DataServiceModbusSlaveOption> Slaves { get; set; }

        #region TCP
        public string? IPAddress { get; set; }
        public int? Port { get; set; }
        #endregion

        #region RTU
        /// <summary>
        /// 端口号
        /// </summary>
        public string? ComName { get; set; }
        /// <summary>
        /// 波特率
        /// </summary>
        public int? BaudRate { get; set; }
        /// <summary>
        /// 校验位
        /// </summary>
        public Parity? Parity { get; set; }
        /// <summary>
        /// 数据位
        /// </summary>
        public int? DataBits { get; set; }
        /// <summary>
        /// 停止位
        /// </summary>
        public StopBits? StopBits { get; set; }
        #endregion
    }
```

- DataServiceModbusSlaveOption (每一个slave的store配置)
    + 源码
```charp
    public class DataServiceModbusSlaveOption
    {
        public byte Id { get; set; }
        public HeartOption Heart { get; set; }
        /// <summary>
        /// 默认值
        /// </summary>
        public List<DataServiceStoreDefaultObjectItem> DefaultValues { get; set; }
    }
```

- HeartOption （心跳配置）
    + 源码
```charp
    public class HeartOption
    {
        /// <summary>
        /// 心跳类型
        /// </summary>
        public HeartType HeartType { get; set; }
        /// <summary>
        /// 心跳地址
        /// </summary>
        public string HeartAddress { get; set; }

        /// <summary>
        /// 心跳数据更新间隔 秒
        /// </summary>
        public int HeartInterval { get; set; }
    }
```

 

- ModbusReadWriteType （读写类型）
    + 源码  

```csharp
       public enum ModbusReadWriteType
    {
        /// <summary>
        /// 用于读取和控制远程设备的开关状态，通常用于控制继电器等开关设备, Reads from 1 to 2000 contiguous coils status.
        /// </summary>
        Coils,
        /// <summary>
        /// 用于读取远程设备的输入状态，通常用于读取传感器等输入设备的状态, Reads from 1 to 2000 contiguous discrete input status.
        /// </summary>
        Inputs,
        /// <summary>
        /// 用于存储和读取远程设备的数据，通常用于存储控制参数、设备状态等信息, Reads contiguous block of holding registers.
        /// </summary>
        HoldingRegisters,
        /// <summary>
        /// 32bit （默认大端 ABCD） 读取2个short 用于存储和读取远程设备的数据，通常用于存储控制参数、设备状态等信息, Reads contiguous block of holding registers.
        /// </summary>
        HoldingRegisters2,
        /// <summary>
        /// 32bit （默认大端 BADC） 读取2个short 用于存储和读取远程设备的数据，通常用于存储控制参数、设备状态等信息, Reads contiguous block of holding registers.
        /// </summary>
        HoldingRegisters2ByteSwap,
        /// <summary>
        /// 64bit （默认大端 ABCD EFGH） 读取4个short 用于存储和读取远程设备的数据，通常用于存储控制参数、设备状态等信息, Reads contiguous block of holding registers.
        /// </summary>
        HoldingRegisters4,
        /// <summary>
        /// 64bit （默认大端 BADC FEHG） 读取4个short 用于存储和读取远程设备的数据，通常用于存储控制参数、设备状态等信息, Reads contiguous block of holding registers.
        /// </summary>
        HoldingRegisters4ByteSwap,
        /// <summary>
        /// 32bit 小端(DCBA) 读取2个short 用于存储和读取远程设备的数据，通常用于存储控制参数、设备状态等信息, Reads contiguous block of holding registers.
        /// </summary>
        HoldingRegistersLittleEndian2,
        /// <summary>
        /// 32bit 小端(CDAB) 读取2个short 用于存储和读取远程设备的数据，通常用于存储控制参数、设备状态等信息, Reads contiguous block of holding registers.
        /// </summary>
        HoldingRegistersLittleEndian2ByteSwap,
        /// <summary>
        /// 64bit  小端(HGFE DCBA) 读取4个short 用于存储和读取远程设备的数据，通常用于存储控制参数、设备状态等信息, Reads contiguous block of holding registers.
        /// </summary>
        HoldingRegistersLittleEndian4,
        /// <summary>
        /// 64bit  小端(FEHG CDAB) 读取4个short 用于存储和读取远程设备的数据，通常用于存储控制参数、设备状态等信息, Reads contiguous block of holding registers.
        /// </summary>
        HoldingRegistersLittleEndian4ByteSwap,
        /// <summary>
        /// 用于存储远程设备的输入数据，通常用于存储传感器等输入设备的数据, Reads contiguous block of input registers.
        /// </summary>
        ReadInputRegisters,
        /// <summary>
        ///32bit(ABCD) 读取2个short  用于存储远程设备的输入数据，通常用于存储传感器等输入设备的数据, Reads contiguous block of input registers.
        /// </summary>
        ReadInputRegisters2,
        /// <summary>
        ///32bit(BADC) 读取2个short  用于存储远程设备的输入数据，通常用于存储传感器等输入设备的数据, Reads contiguous block of input registers.
        /// </summary> 
        ReadInputRegisters2ByteSwap,
        /// <summary>
        /// 64bit(ABCD EFGH) 读取4个short 用于存储远程设备的输入数据，通常用于存储传感器等输入设备的数据, Reads contiguous block of input registers.
        /// </summary>
        ReadInputRegisters4,
        /// <summary>
        /// 64bit(BADC FEHG) 读取4个short 用于存储远程设备的输入数据，通常用于存储传感器等输入设备的数据, Reads contiguous block of input registers.
        /// </summary>
        ReadInputRegisters4ByteSwap,
        /// <summary>
        ///32bit(DCBA) 小端 读取2个short  用于存储远程设备的输入数据，通常用于存储传感器等输入设备的数据, Reads contiguous block of input registers.
        /// </summary>
        ReadInputRegistersLittleEndian2,
        /// <summary>
        ///32bit(CDAB) 小端 读取2个short  用于存储远程设备的输入数据，通常用于存储传感器等输入设备的数据, Reads contiguous block of input registers.
        /// </summary>
        ReadInputRegistersLittleEndian2ByteSwap,
        /// <summary>
        /// 64bit(HGFE DCBA) 小端 读取4个short 用于存储远程设备的输入数据，通常用于存储传感器等输入设备的数据, Reads contiguous block of input registers.
        /// </summary>
        ReadInputRegistersLittleEndian4,
        /// <summary>
        /// 64bit(GHEF CDAB) 小端 读取4个short 用于存储远程设备的输入数据，通常用于存储传感器等输入设备的数据, Reads contiguous block of input registers.
        /// </summary>
        ReadInputRegistersLittleEndian4ByteSwap

    }
```

- DataServiceStoreDefaultObjectItem (默认值配置)
    + 源码

```csharp
     public class DataServiceStoreDefaultDataItem<TValue> where TValue : notnull
    {
        /// <summary>
        /// 地址
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 默认值
        /// </summary>
        public required TValue DefaultValue { get; set; }
    }

    public class DataServiceStoreDefaultObjectItem : DataServiceStoreDefaultDataItem<object>
    {

    }
    public class DataServiceStoreDefaultBoolItem : DataServiceStoreDefaultDataItem<bool>
    {

    }
    public class DataServiceStoreDefaultUShortItem : DataServiceStoreDefaultDataItem<ushort>
    {

    }
```