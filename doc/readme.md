# mqtt

```csharp
  builder.Services.AddMqttReceiver(builder.Configuration.GetSection("GathererMqttOption").Get<DataReceiverMqttOption>(),
                str =>
                {
                    if (string.IsNullOrEmpty(str))
                        return null;
                    JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var data = JsonSerializer.Deserialize<InPlantMqttEntity>(str, jsonSerializerOptions);

                    if (data == null || data.RTValue == null) return null;

                    Dictionary<string, ReceiverTempDataValue> datas = new Dictionary<string, ReceiverTempDataValue>(data.RTValue.Count);
                    foreach (var item in data.RTValue)
                    {
                        if (item.Value == null) continue;
                        if (!datas.ContainsKey(item.Name))
                            datas.Add(item.Name, new ReceiverTempDataValue(item.Value, item.Timestamp));
                    }
                    return datas;
                }, true, null, null);
```

#  DataReceiverMqttOption配置

```json
{
    "GathererMqttOption": {
        "originName": "",
        "originPwd": "",
        "originHost": "127.0.0.1",
        "originPort": 1883,
        "reConnectPeriod": 10000, //重连间隔 单位ms
        "reConnectTimes": 2, //重连次数
        "connectTimeout": 60000, //连接超时时间 单位ms
        "receiverName": "warn_receiver", //
        "nodeFile": "ConfigFiles/NodeConfigs/test.json",
        "autoConnect": false,
        "autoWork": false,
        "dataInputTopics": [ "rtdvalue/report" ]
      }
  }
```