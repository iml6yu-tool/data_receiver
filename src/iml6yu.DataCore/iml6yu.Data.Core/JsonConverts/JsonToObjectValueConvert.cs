using System;
using System.Collections.Generic;
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
                if (reader.TryGetInt32(out int intNum))
                    return intNum;
                if (reader.TryGetInt64(out long longNum))
                    return longNum;
                if (reader.TryGetDouble(out double doubleNum))
                    return doubleNum;
                else
                    return reader.GetDecimal();
            }
            if (reader.TokenType == JsonTokenType.String)
                reader.GetString();
            return null;
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value == null)
                writer.WriteNullValue();
            else
            {
                Type objType = value.GetType();
                if (objType == typeof(string) || objType == typeof(DateTime) || objType == typeof(Guid))
                    writer.WriteStringValue(value.ToString());
                else if (objType == typeof(int))
                    writer.WriteNumberValue((int)value);
                else if (objType == typeof(double))
                    writer.WriteNumberValue((double)value);
                else if (objType == typeof(decimal))
                    writer.WriteNumberValue((decimal)value);
                else if (objType == typeof(char))
                    writer.WriteNumberValue((char)value);
                else if (objType == typeof(bool))
                    writer.WriteBooleanValue((bool)value);
                else
                    writer.WriteStringValue(value.ToString());
            }
        }
    }
}
