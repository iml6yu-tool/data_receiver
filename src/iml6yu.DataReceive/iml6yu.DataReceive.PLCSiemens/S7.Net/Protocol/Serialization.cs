using Newtonsoft.Json.Linq;
using S7.Net.Types;
using System;
using System.Collections.Generic;

namespace S7.Net.Protocol
{
    internal static class Serialization
    {
        public static ushort GetWordAt(IList<byte> buf, int index)
        {
            return (ushort)((buf[index] << 8) + buf[index]);
        }

        public static byte[] SerializeDataItem(DataItem dataItem)
        {
            if (dataItem.Value == null)
            {
                throw new Exception($"DataItem.Value is null, cannot serialize. StartAddr={dataItem.StartByteAdr} VarType={dataItem.VarType}");
            }

            if (dataItem.Value is string s)
                return dataItem.VarType switch
                {
                    VarType.S7String => S7String.ToByteArray(s, dataItem.Count),
                    VarType.S7WString => S7WString.ToByteArray(s, dataItem.Count),
                    _ => Types.String.ToByteArray(s, dataItem.Count)
                };

            if (dataItem.VarType == VarType.Date)
            {
                return Date.ToByteArray((System.DateTime)dataItem.Value);
            }

            #region 添加更多类型的解析 不要依赖SerializeValue方法，这个方法解析不考虑dataItem.VarType ，有隐形bug
            if (dataItem.VarType == VarType.Bit)
            {
                return new[] { (byte)((bool)dataItem.Value ? 1 : 0) };
            }
            if (dataItem.VarType == VarType.Byte)
            {
                return Types.Byte.ToByteArray((byte)dataItem.Value);
            }
            if (dataItem.VarType == VarType.Word || dataItem.VarType == VarType.Int)
            {
                if (dataItem.Value.GetType().Name == "Int16")
                    return Types.Int.ToByteArray((Int16)dataItem.Value);
                else
                    return Types.Word.ToByteArray((UInt16)dataItem.Value);
            }

            if (dataItem.VarType == VarType.DWord || dataItem.VarType == VarType.DInt)
            {
                if (dataItem.Value.GetType().Name == "Int32")
                    return Types.DInt.ToByteArray((Int32)dataItem.Value);
                else
                    return Types.DWord.ToByteArray((UInt32)dataItem.Value);
            }

            if (dataItem.VarType == VarType.Real)
            {
                return Types.Real.ToByteArray((float)dataItem.Value);
            }

            if (dataItem.VarType == VarType.LReal)
            {
                return Types.LReal.ToByteArray((double)dataItem.Value);
            }

            if (dataItem.VarType == VarType.String )
            {
                var stringVal = (string)dataItem.Value;
                return Types.String.ToByteArray(stringVal, stringVal.Length);
            }
            if (dataItem.VarType == VarType.S7String)
            {
                var stringVal = (string)dataItem.Value;
                return Types.S7String.ToByteArray(stringVal, stringVal.Length);
            }
            if (dataItem.VarType == VarType.S7WString)
            {
                var stringVal = (string)dataItem.Value;
                return Types.S7WString.ToByteArray(stringVal, stringVal.Length);
            } 

            if (dataItem.VarType == VarType.DateTime)
            {
                return Types.DateTime.ToByteArray((System.DateTime)dataItem.Value);
            }
            if (dataItem.VarType == VarType.DateTimeLong)
            {
                return Types.DateTimeLong.ToByteArray((System.DateTime)dataItem.Value);
            }
            #endregion
            return SerializeValue(dataItem.Value);
        }

        public static byte[] SerializeValue(object value)
        {
            switch (value.GetType().Name)
            {
                case "Boolean":
                    return new[] { (byte)((bool)value ? 1 : 0) };
                case "Byte":
                    return Types.Byte.ToByteArray((byte)value);
                case "Int16":
                    return Types.Int.ToByteArray((Int16)value);
                case "UInt16":
                    return Types.Word.ToByteArray((UInt16)value);
                case "Int32":
                    return Types.DInt.ToByteArray((Int32)value);
                case "UInt32":
                    return Types.DWord.ToByteArray((UInt32)value);
                case "Single":
                    return Types.Real.ToByteArray((float)value);
                case "Double":
                    return Types.LReal.ToByteArray((double)value);
                case "DateTime":
                    return Types.DateTime.ToByteArray((System.DateTime)value);
                case "Byte[]":
                    return (byte[])value;
                case "Int16[]":
                    return Types.Int.ToByteArray((Int16[])value);
                case "UInt16[]":
                    return Types.Word.ToByteArray((UInt16[])value);
                case "Int32[]":
                    return Types.DInt.ToByteArray((Int32[])value);
                case "UInt32[]":
                    return Types.DWord.ToByteArray((UInt32[])value);
                case "Single[]":
                    return Types.Real.ToByteArray((float[])value);
                case "Double[]":
                    return Types.LReal.ToByteArray((double[])value);
                case "String":
                    // Hack: This is backwards compatible with the old code, but functionally it's broken
                    // if the consumer does not pay attention to string length.
                    var stringVal = (string)value;
                    return Types.String.ToByteArray(stringVal, stringVal.Length);
                case "DateTime[]":
                    return Types.DateTime.ToByteArray((System.DateTime[])value);
                case "DateTimeLong[]":
                    return Types.DateTimeLong.ToByteArray((System.DateTime[])value);
                default:
                    throw new InvalidVariableTypeException();
            }
        }

        public static void SetAddressAt(ByteArray buffer, int index, int startByte, byte bitNumber)
        {
            var start = startByte * 8 + bitNumber;
            buffer[index + 2] = (byte)start;
            start >>= 8;
            buffer[index + 1] = (byte)start;
            start >>= 8;
            buffer[index] = (byte)start;
        }

        public static void SetWordAt(ByteArray buffer, int index, ushort value)
        {
            buffer[index] = (byte)(value >> 8);
            buffer[index + 1] = (byte)value;
        }
    }
}
