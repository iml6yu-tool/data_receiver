using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace iml6yu.Data.Core.JsonConverts
{
    public class DateTimeConvert : JsonConverter<DateTime>
    {
        private string _dateTimeFormat = "yyyy-MM-dd HH:mm:ss.fff";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.Parse(reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(_dateTimeFormat));
        }

        public void SetDateTimeFormat(string format)
        {
            _dateTimeFormat = format;
        }
    }
}
