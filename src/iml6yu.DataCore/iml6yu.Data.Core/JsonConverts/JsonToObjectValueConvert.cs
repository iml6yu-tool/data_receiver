using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace iml6yu.Data.Core.JsonConverts
{
    public class JsonToObjectValueConvert : JsonConverter<Object>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.True)
                return true;

            if (reader.TokenType == JsonTokenType.False)
                return false;

            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.Number)
            {
                //整数：由窄到宽，先有符号再无符号，保证 ushort/uint/ulong 都能被命中
                if (reader.TryGetByte(out byte byteNum))        //0 ~ 255
                    return byteNum;
                if (reader.TryGetInt16(out short shortNum))     //-32768 ~ 32767
                    return shortNum;
                if (reader.TryGetUInt16(out ushort ushortNum))  //32768 ~ 65535
                    return ushortNum;
                if (reader.TryGetInt32(out int intNum))         //-2147483648 ~ 2147483647
                    return intNum;
                if (reader.TryGetUInt32(out uint uintNum))      //2147483648 ~ 4294967295
                    return uintNum;
                if (reader.TryGetInt64(out long longNum))       //-2^63 ~ 2^63-1
                    return longNum;
                if (reader.TryGetUInt64(out ulong ulongNum))    //2^63 ~ 2^64-1
                    return ulongNum;
                //小数：先用 decimal 保精度，超出 decimal 范围再退回 double
                if (reader.TryGetDecimal(out decimal decimalNum))
                    return decimalNum;
                if (reader.TryGetDouble(out double doubleNum))
                    return doubleNum;
                throw new JsonException($"无法解析的数值：{Encoding.UTF8.GetString(reader.ValueSpan)}");
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                var stringV = reader.GetString();
                if (string.IsNullOrEmpty(stringV))
                    return stringV;

                //Guid 判定最严格，先判，避免带 '-' 的字符串白白走一次日期解析
                if (Guid.TryParse(stringV, out Guid guid))
                    return guid;

                //只对“看起来像日期时间”的字符串做解析，避免 "20240101"、"1-2" 之类被误判
                if (IsDateTimeLike(stringV)
                    && DateTime.TryParse(stringV, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dt))
                    return dt;

                return stringV;
            }

            return null;
        }

        /// <summary>
        /// 粗略判断字符串是否可能是日期时间：长度至少 8 且包含日期时间分隔符。
        /// 纯数字串（如 20240101）不算，防止被 DateTime.TryParse 吞掉。
        /// </summary>
        private static bool IsDateTimeLike(string value)
        {
            if (value.Length < 8)
                return false;

            foreach (var c in value)
            {
                if (c == '-' || c == '/' || c == ':' || c == 'T' || c == 't')
                    return true;
            }
            return false;
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value == null)
                writer.WriteNullValue();
            else
            {
                Type valueType = value.GetType();

                // 使用默认序列化器处理复杂类型
                JsonSerializer.Serialize(writer, value, valueType, options);

                //Type objType = value.GetType();
                //if (objType == typeof(string) || objType == typeof(DateTime) || objType == typeof(Guid))
                //    writer.WriteStringValue(value.ToString());
                //else if (objType == typeof(int))
                //    writer.WriteNumberValue((int)value);
                //else if (objType == typeof(double))
                //    writer.WriteNumberValue((double)value);
                //else if (objType == typeof(decimal))
                //    writer.WriteNumberValue((decimal)value);
                //else if (objType == typeof(char))
                //    writer.WriteNumberValue((char)value);
                //else if (objType == typeof(bool))
                //    writer.WriteBooleanValue((bool)value);
                //else
                //    writer.WriteStringValue(value.ToString());
            }
        }
    }
}
