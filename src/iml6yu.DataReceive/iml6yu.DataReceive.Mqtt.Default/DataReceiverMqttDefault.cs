using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.Mqtt.Configs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace iml6yu.DataReceive.Mqtt.Default
{
    /// <summary>
    /// 系统默认的mqtt协议，支持的数据协议是DataReceiveContract
    /// </summary>
    public class DataReceiverMqttDefault : DataReceiverMqtt
    {
        public DataReceiverMqttDefault(DataReceiverMqttOption option, ILogger logger, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null) : base(option, logger, isAutoLoadNodeConfig, nodes)
        {
        }
        /// <summary>
        /// Parses the specified JSON string and extracts a dictionary of receiver data values.
        /// </summary>
        /// <remarks>The method attempts to deserialize the input JSON into a data contract and extract
        /// receiver data values. If the JSON is invalid, or if no data is present, the method returns <see
        /// langword="null"/>. Duplicate addresses are ignored; only the first occurrence is included in the
        /// result.</remarks>
        /// <param name="json">A JSON-formatted string representing the data to parse. Must not be <see langword="null"/> or empty.</param>
        /// <returns>A <see cref="Dictionary{TKey, TValue}"/> containing receiver addresses as keys and their corresponding <see
        /// cref="ReceiverTempDataValue"/> objects as values, or <see langword="null"/> if the input is invalid or
        /// contains no data.</returns>
        protected override Dictionary<string, ReceiverTempDataValue> JsonDataPrase(string json)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<DataReceiveContract>(json);
                if (data == null || data.Datas == null || data.Datas.Count == 0) return null;
                Dictionary<string, ReceiverTempDataValue> datas = new Dictionary<string, ReceiverTempDataValue>(data.Datas.Count);
                foreach (var item in data.Datas)
                {
                    if (item.Value == null) continue;
                    if (!datas.ContainsKey(item.Address))
                        datas.Add(item.Address, new ReceiverTempDataValue(item.Value, item.Timestamp ?? data.Timestamp));
                }
                return datas;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Default JsonDataPrase error");
                return null;
            }

        }
    }
}
