using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataReceive.ModbusMaster
{
    public static class Extension
    {
        /// <summary>
        /// 获取读取或者写入类型对应的modbus长度
        /// </summary>
        /// <param name="readType"></param>
        /// <returns></returns>

        public static ushort GetNumberOfPoint(this ModbusReadWriteType readType)
        {
            if (readType == ModbusReadWriteType.Coils || readType == ModbusReadWriteType.Inputs)
                return 1;
            if (readType == ModbusReadWriteType.HoldingRegisters || readType == ModbusReadWriteType.ReadInputRegisters)
                return 1;

            if (readType == ModbusReadWriteType.ReadInputRegistersLittleEndian2
                || readType == ModbusReadWriteType.ReadInputRegistersLittleEndian2ByteSwap
                || readType == ModbusReadWriteType.ReadInputRegisters2
                || readType == ModbusReadWriteType.ReadInputRegisters2ByteSwap
                || readType == ModbusReadWriteType.HoldingRegisters2
                || readType == ModbusReadWriteType.HoldingRegisters2ByteSwap
                || readType == ModbusReadWriteType.HoldingRegistersLittleEndian2
                || readType == ModbusReadWriteType.HoldingRegistersLittleEndian2ByteSwap
                || readType == ModbusReadWriteType.HoldingRegistersFloat
                || readType == ModbusReadWriteType.HoldingRegistersFloatLittleEndian
                || readType == ModbusReadWriteType.HoldingRegistersFloatByteSwap
                || readType == ModbusReadWriteType.HoldingRegistersFloatLittleEndianByteSwap
                || readType == ModbusReadWriteType.ReadInputRegistersFloat
                || readType == ModbusReadWriteType.ReadInputRegistersFloatLittleEndian
                || readType == ModbusReadWriteType.ReadInputRegistersFloatByteSwap
                || readType == ModbusReadWriteType.ReadInputRegistersFloatLittleEndianByteSwap)
                return 2;
            else
                return 4;
        } 
      
        public static bool[] ToModbusBooleanValues(this object source)
        {
            if (source is bool b)
                return [b];
            if (source is byte by)
                return [by == 1];
            if (source is short s)
                return [s == 1];
            if (source is ushort us)
                return [us == 1];
            if (source is int i)
                return [i == 1];
            if (source is uint ui)
                return [ui == 1];
            if (source is long l)
                return [l == 1];
            if (source is ulong ul)
                return [ul == 1];
            return [source.ToString() == "1"];
        }

        /// <summary>
        /// 数据转ushort数组（大端：AB CD）
        /// </summary>
        /// <param name="source"></param>
        /// <param name="byteArrLenght"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static ushort[] ToModbusUShortValues(this object source, int? byteArrLenght = null)
        {
            byte[] byteArray = source.ConvertToByteArray(byteArrLenght);
            return byteArray.ProcessWithEndian(false, false);
        }

        /// <summary>
        /// 数据转ushort数组（大端交换：BA DC）
        /// </summary>
        public static ushort[] ToModbusUShortBSValues(this object source, int? byteArrLenght = null)
        {
            byte[] byteArray = source.ConvertToByteArray(byteArrLenght);
            return byteArray.ProcessWithEndian(false, true);
        }

        /// <summary>
        /// 数据转ushort数组（小端：DC BA）
        /// </summary>
        public static ushort[] ToModbusUShortLEValues(this object source, int? byteArrLenght = null)
        {
            byte[] byteArray = source.ConvertToByteArray(byteArrLenght);
            return byteArray.ProcessWithEndian(true, false);
        }

        /// <summary>
        /// 数据转ushort数组（小端交换：CD AB）
        /// </summary>
        public static ushort[] ToModbusUShortLEBSValues(this object source, int? byteArrLenght = null)
        {
            byte[] byteArray = source.ConvertToByteArray(byteArrLenght);
            return byteArray.ProcessWithEndian(true, true);
        }

        /// <summary>
        /// 转换大小端
        /// </summary>
        /// <param name="bytes">数值转byte数组，默认顺序应该是 AB CD</param>
        /// <param name="isLittleEndian"></param>
        /// <param name="swapWithinUShort"></param>
        /// <returns></returns>
        private static ushort[] ProcessWithEndian(this byte[] bytes, bool isLittleEndian, bool swapWithinUShort)
        {
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            /*
             * 大端 AB CD
             * 小端 DC BA
             * 
             * 大端交换 BA DC
             * 小端交换 CD AB
             */
            if (isLittleEndian)
                Array.Reverse(bytes);

            // 如果需要交换每个ushort内部的字节序（CD AB模式），则进行处理
            if (swapWithinUShort)
            {
                for (int i = 0; i < bytes.Length; i += 2)
                {
                    if (i + 1 < bytes.Length)
                    {
                        (bytes[i], bytes[i + 1]) = (bytes[i + 1], bytes[i]);
                    }
                }
            }

            return bytes.ToUShortArray();
        }

        // 字节数组转ushort数组
        private static ushort[] ToUShortArray(this byte[] bytes)
        {
            if (bytes.Length % 2 != 0)
                throw new ArgumentException("Byte array length must be even");

            ushort[] result = new ushort[bytes.Length / 2];

            for (int i = 0; i < result.Length; i++)
            {
                int offset = bytes.Length - i * 2 - 2;
                result[i] = BitConverter.ToUInt16(bytes, offset);
            }
            return result;
        }

        private static byte[] ConvertToByteArray(this object source, int? byteArrLenght)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var byteArray = source switch
            {
                float f => BitConverter.GetBytes(f),
                double d => BitConverter.GetBytes(d),
                int i => BitConverter.GetBytes(i),
                uint ui => BitConverter.GetBytes(ui),
                long l => BitConverter.GetBytes(l),
                ulong ul => BitConverter.GetBytes(ul),
                ushort us => BitConverter.GetBytes(us),
                short s => BitConverter.GetBytes(s),
                byte b => BitConverter.GetBytes((short)b),
                _ => throw new NotSupportedException($"Unsupported type: {source.GetType()}")
            };

            if (byteArrLenght.HasValue && byteArray.Length != byteArrLenght)
                Array.Resize(ref byteArray, byteArrLenght.Value);
            return byteArray;
        }
    }
}
