# iml6yu.DataService.ModbusTCP

iml6yu.DataService.ModbusTCP is a .NET library for slave of Modbus TCP data.

## 用法

```csharp
    ILogger logger;

	DataServiceModbusOption option = new DataServiceModbusOption
	{
		IpAddress="",
        Port=502, //TCP端口号
	};

	var  dataService = new DataService.ModbusTCP.DataServiceModbusTCP(option, logger);
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


## 最后
如果在使用过程中遇到任何bug或者困难，可以添加WeChat或者是Microsoft Teams

|Wechat| Microsoft Teams|
|----|----|
|![WeChat](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAFoAAABYCAYAAAB1YOAJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsQAAA7EAZUrDhsAACLISURBVHhe7d0HtC1FES7gMYs5oKiYEROYc84RTJgDRhRUxICKmDChqIgBVAQB9aogBkSCWTGjoogiiFkwA+Zr1v3qqzO1T++5s/c5Fy5vrfeW/1qzZk93z0xPT3VV/VU955xrEuj+h3Mc6wz0P/7xj07Ruc51rr5kMbS1bbTRRn3J/3/429/+1p373Ofuj1bGf//733XHw0AXnva0pxn0s7Q961nP6q8yjje+8Y2T+93vfpN//etfeXz66adPHvCAB0ze+973TnbdddfJk570pCyHn/70p5N73OMek6OPProvmUxe+cpX5vmLcNBBB+V5f/rTn/qS+fjnP/+Zbd/znvdMnvvc5472/7TTTptc4AIXGH3elbZNNtlkcuaZZ/ZXWpLGRLy10RPWZ1uEm93sZtmmbv7tb387jw3eZS5zmZnzP/GJT+Rx+/Cbb775ive4733vm21++MMf9iXz8Zvf/CbbPuhBD5pc/OIXn1zoQhfqa5ZxdgTP9rrXva6/0mRy3ihIxO/+V9e97GUv6x7xiEd0f//73/uScUTnure85S3dnnvu2ZfMx4te9KLuy1/+cnepS10qj6973et2xx9/fHf1q1+9O+SQQ7q1a9dmOdzpTnfqjjvuuKwr7L777t13v/vd7o9//GN35JFHdve85z27S1/60ln3yU9+srvwhS/c7bzzzt01r3nN7vKXv3x38MEHd7e97W27E044obvc5S6Xz/erX/2qu8997pNq8bKXvWz3zGc+s3vwgx/c3ete98r6U045JdvHTOvOd77zTdXFxhtv3H3+85/PNu04DaH9n//85+6ud71r7v/zn//0NYE4MfHXv/51+ia++c1v9qUrIzowPW8Rtthii2xDZQyxmvOvcIUrZJvXvOY1uX/rW9+a5VSR4wte8IKTu93tbvmbJNk//vGPz/01rnGNybWuda38/fvf/z7Pi0HP43vf+96T85///JNLXvKSk3gxWfbjH/842zz96U/P41vf+tZ5vFpc5zrXyfNe+9rX9iWNRLcoSf75z3/e7bXXXl08RL5JkmD/73//O6UhHn6u1DOqod/SMHjTe+yxR/fV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)|[Join Us](https://teams.live.com/l/community/FBAE1yi-6dHrMHnSgI)|