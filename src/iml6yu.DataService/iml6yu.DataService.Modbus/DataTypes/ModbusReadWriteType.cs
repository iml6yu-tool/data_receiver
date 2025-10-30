using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace iml6yu.DataService.Modbus
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
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
        /// float  32bit （默认大端 ABCD）
        /// </summary>
        HoldingRegistersFloat,
        /// <summary>
        /// float  32bit 小端(DCBA)
        /// </summary>
        HoldingRegistersFloatLittleEndian,
        /// <summary>
        /// float  32bit 大端交换 (BADC)
        /// </summary>
        HoldingRegistersFloatByteSwap,
        /// <summary>
        /// float  32bit 小端交换 (CDAB)
        /// </summary>
        HoldingRegistersFloatLittleEndianByteSwap,
        /// <summary>
        /// 64bit  大端 （ABCD EFGH） 读取4个short 用于存储和读取远程设备的数据，通常用于存储控制参数、设备状态等信息, Reads contiguous block of holding registers.
        /// </summary>
        HoldingRegisters4,
        /// <summary>
        /// 64bit 大端交换 （BADC FEHG） 读取4个short 用于存储和读取远程设备的数据，通常用于存储控制参数、设备状态等信息, Reads contiguous block of holding registers.
        /// </summary>
        HoldingRegisters4ByteSwap, 
        /// <summary>
        /// double  64bit （默认大端 ABCD EFGH）
        /// </summary>
        HoldingRegistersDouble,
        /// <summary>
        /// double  64bit  小端(HGFE DCBA)
        /// </summary>
        HoldingRegistersDoubleLittleEndian,
        /// <summary>
        /// double  64bit   大端交换 （BADC FEHG）
        /// </summary>
        HoldingRegistersDoubleByteSwap,
        /// <summary>
        /// double  64bit  小端交换(FEHG CDAB)
        /// </summary>
        HoldingRegistersDoubleLittleEndianByteSwap, 
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
        /// 64bit  小端交换(FEHG CDAB) 读取4个short 用于存储和读取远程设备的数据，通常用于存储控制参数、设备状态等信息, Reads contiguous block of holding registers.
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
        /// float  32bit （默认大端 ABCD）
        /// </summary>
        ReadInputRegistersFloat,
        /// <summary>
        /// float  32bit 小端(DCBA)
        /// </summary>
        ReadInputRegistersFloatLittleEndian,
        /// <summary>
        /// float  32bit 大端交换 (BADC)
        /// </summary>
        ReadInputRegistersFloatByteSwap,
        /// <summary>
        /// float  32bit 小端交换 (CDAB)
        /// </summary>
        ReadInputRegistersFloatLittleEndianByteSwap,
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
        ReadInputRegistersLittleEndian4ByteSwap,
        /// <summary>
        /// double  64bit （默认大端 ABCD EFGH）
        /// </summary>
        ReadInputRegistersDouble,
        /// <summary>
        /// double  64bit  小端(HGFE DCBA)
        /// </summary>
        ReadInputRegistersDoubleLittleEndian,
        /// <summary>
        /// double  64bit   大端交换 （BADC FEHG）
        /// </summary>
        ReadInputRegistersDoubleByteSwap,
        /// <summary>
        /// double  64bit  小端交换(FEHG CDAB)
        /// </summary>
        ReadInputRegistersDoubleLittleEndianByteSwap,

    }
}
