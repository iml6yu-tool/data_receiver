using System.Text.Json.Serialization;

namespace iml6yu.DataService.Core.Configs
{
    /// <summary>
    /// 默认点位地址和值
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public class DataServiceStorageDefaultDataItem<TValue> where TValue : notnull
    {
        /// <summary>
        /// 地址
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 默认值
        /// </summary>
        public TValue DefaultValue { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TypeCode? ValueType { get; set; }
    }

    public class DataServiceStorageDefaultObjectItem : DataServiceStorageDefaultDataItem<object>
    {

    }
    public class DataServiceStorageDefaultBoolItem : DataServiceStorageDefaultDataItem<bool>
    {

    }
    public class DataServiceStorageDefaultUShortItem : DataServiceStorageDefaultDataItem<ushort>
    {

    }
}
