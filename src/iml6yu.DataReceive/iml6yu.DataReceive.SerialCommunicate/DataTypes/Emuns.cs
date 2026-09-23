using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace iml6yu.DataReceive.SerialCommunicate.DataTypes
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SerialReadWriteType
    {
        /// <summary>
        /// 1Bit 默认在一个Byte中的bit（重点：bit是从0开始的，0-7）
        /// </summary>
        Bit,
        /// <summary>
        /// Byte 字节 8bit
        /// </summary>
        Byte,
        /// <summary>
        /// 2Byte  16bit  大端 (0x AB) 
        /// </summary>
        Short,
        /// <summary>
        /// 2Byte  16bit 小端 (0x BA)
        /// </summary>
        ShortLittleEndian,
        /// <summary>
        /// 4Byte  32bit 大端 (0x ABCD)
        /// </summary>
        Int,
        /// <summary>
        /// 4Byte  32bit 小端 (0x DCBA)
        /// </summary>
        IntLittleEndian,
        /// <summary>
        /// 4Byte  32bit 大端交换 (0x BADC)
        /// </summary>
        IntByteSwap,
        /// <summary>
        /// 4Byte  32bit 小端交换 (0x CDAB)
        /// </summary>
        IntLittleEndianSwap,

        /// <summary>
        /// 4Byte 32bit IEEE 754 浮点数 大端 (0x ABCD)
        /// </summary>
        Float,
        /// <summary>
        /// 4Byte 32bit IEEE 754 浮点数 小端 (0x DCBA)
        /// </summary>
        FloatLittleEndian,
        /// <summary>
        /// 4Byte 32bit IEEE 754 浮点数 大端交换 (0x BADC)
        /// </summary>
        FloatByteSwap,
        /// <summary>
        /// 4Byte 32bit IEEE 754 浮点数 小端交换 (0x CDAB)
        /// </summary>
        FloatLittleEndianByteSwap,

        /// <summary>
        /// 8Byte 64bit IEEE 754 浮点数 大端 (0x ABCD EFGH)
        /// </summary>
        Double,
        /// <summary>
        /// 8Byte 64bit IEEE 754 浮点数 小端 (0x HGFE DCBA)
        /// </summary>
        DoubleLittleEndian,
        /// <summary>
        /// 8Byte 64bit IEEE 754 浮点数 大端交换 (0x BADC FEHG)
        /// </summary>
        DoubleByteSwap,
        /// <summary>
        /// 8Byte 64bit IEEE 754 浮点数 小端交换 (0x FEHG CDAB)
        /// </summary>
        DoubleLittleEndianByteSwap,

        /// <summary>
        /// 字符串 长度在地址中指定
        /// </summary>
        String

    }
}
