using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.Mqtt.Configs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;

namespace iml6yu.DataReceive.Mqtt.InPlant
{
    public class DataReceiverMqttInPlant : DataReceiverMqtt
    {
        public DataReceiverMqttInPlant(DataReceiverMqttOption option, ILogger logger, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null) : base(option, logger, isAutoLoadNodeConfig, nodes)
        {
        }

        protected override Dictionary<string, ReceiverTempDataValue> JsonDataPrase(string json)
        {
            try
            {
                var list = JsonConvert.DeserializeObject<InPlantMqttEntity>(json);
                if (list == null || list.RTValue == null) return null;
                Dictionary<string, ReceiverTempDataValue> datas = new Dictionary<string, ReceiverTempDataValue>(list.RTValue.Count);
                foreach (var item in list.RTValue)
                {
                    if (item.Value == null) continue;
                    if (!datas.ContainsKey(item.Name))
                        datas.Add(item.Name, new ReceiverTempDataValue(item.Value, GetTimestamp(DateTimeOffset.FromUnixTimeSeconds(item.Timestamp))));
                }
                return datas;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "InPlant JsonDataPrase error");
                return null;
            } 
        }
    }
}
