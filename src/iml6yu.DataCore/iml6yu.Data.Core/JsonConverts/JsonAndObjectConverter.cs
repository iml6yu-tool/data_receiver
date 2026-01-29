using System.Text.Encodings.Web;
using System.Text.Json;

namespace iml6yu.Data.Core.JsonConverts
{
    public static class JsonAndObjectConverter
    {
        public static JsonSerializerOptions JsonSerializerOption = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true
        };

        static JsonAndObjectConverter()
        {
            JsonSerializerOption.Converters.Add(new JsonToObjectValueConvert());
            JsonSerializerOption.Converters.Add(new DateTimeConvert());
        }

        public static string ObjectToJson<T>(this T obj, string dateTimeFomatter = null) where T : class
        {
            if (obj == null)
                return string.Empty;
            if (!string.IsNullOrEmpty(dateTimeFomatter))
                (JsonSerializerOption.Converters.FirstOrDefault(t => t is DateTimeConvert) as DateTimeConvert)?.SetDateTimeFormat(dateTimeFomatter);
            return System.Text.Json.JsonSerializer.Serialize<T>(obj, JsonSerializerOption);
        }
        public static T JsonToObject<T>(this string json) where T : class
        {
            if (string.IsNullOrEmpty(json))
                return default(T);


            return System.Text.Json.JsonSerializer.Deserialize<T>(json, JsonSerializerOption);
        }
    }
}
