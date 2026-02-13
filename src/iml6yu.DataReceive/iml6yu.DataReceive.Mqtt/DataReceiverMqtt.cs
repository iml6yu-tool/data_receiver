using iml6yu.Data.Core;
using iml6yu.Data.Core.JsonConverts;
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.Mqtt.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using MQTTnet;
using System.Net.Http;
using System.Text.Json;
#if NET6_0
using MQTTnet.Client;
#endif

namespace iml6yu.DataReceive.Mqtt
{
    public class DataReceiverMqtt : DataReceiver<IMqttClient, DataReceiverMqttOption>
    {
        /// <summary>
        /// 地址对应Groupname
        /// <list type="number">
        /// <item>Key: FullAddress </item>
        /// <item>Value:(GroupName,Address)</item>
        /// </list>
        /// </summary>
        protected Dictionary<string, (string, string)> AddressRefGroupName = new Dictionary<string, (string, string)>();

        /// <summary>
        /// 将接收到的json字符串转成 字典形式
        /// <![CDATA[
        /// 数据格式：
        /// Key：  地址
        /// Value：临时值（值和时间戳）
        /// ]]>
        /// </summary>
        protected virtual Func<string, Dictionary<string, ReceiverTempDataValue>> DefaultDataParse { get; set; }

        /// <summary>
        /// Object转Value的json转换器
        /// </summary>
        protected JsonSerializerOptions ObjectToValueJsonConverter = new JsonSerializerOptions();

        public DataReceiverMqtt(DataReceiverMqttOption option, ILogger logger, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null) : base(option, logger, isAutoLoadNodeConfig, nodes)
        {
            ObjectToValueJsonConverter.Converters.Add(new JsonToObjectValueConvert());
            SetDataParse(JsonDataPrase);
        }

        public override MessageResult LoadConfig(List<NodeItem> nodes)
        {
            var r = base.LoadConfig(nodes);
            if (!r.State)
                Logger.LogError(r.Message);

            if (ConfigNodes == null)
                return MessageResult.Failed(ResultType.ParameterError, "", new ArgumentNullException(nameof(ConfigNodes)));

            try
            {
                foreach (var key in ConfigNodes.Keys)
                {
                    foreach (var item in ConfigNodes[key])
                    {
                        if (AddressRefGroupName.ContainsKey(item.FullAddress)) continue;
                        AddressRefGroupName.Add(item.FullAddress, (key, item.Address));
                    }
                }
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                return MessageResult.Failed(ResultType.ParameterError, ex.Message, ex);
            }
        }

        public override bool IsConnected
        {
            get
            {
                return Client != null && Client.IsConnected;
            }
        }

        public override async Task<MessageResult> ConnectAsync()
        {
            try
            {
                if (IsConnected)
                    return MessageResult.Success();

                var mqttOptBuilder = new MqttClientOptionsBuilder()
                    .WithTcpServer(Option.OriginHost, Option.OriginPort)
                    .WithClientId($"iml6yu.Receiver.{Option.ReceiverName}.{DateTime.Now.ToString("yyyyMMddHHmmssfff")}")
                    .WithTimeout(TimeSpan.FromMilliseconds(Option.ConnectTimeout));
                if (!string.IsNullOrEmpty(Option.OriginName) && !string.IsNullOrEmpty(Option.OriginPwd))
                    mqttOptBuilder.WithCredentials(Option.OriginName, Option.OriginPwd);

                var mqttOption = mqttOptBuilder.Build();

                var result = await Client.ConnectAsync(mqttOption);

                if (result.ResultCode == MqttClientConnectResultCode.Success)
                    return MessageResult.Success();

                return MessageResult.Failed(ResultType.ServerNetworkError, result.ReasonString, null);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

        public override async Task<MessageResult> DisConnectAsync()
        {
            try
            {
                if (IsConnected)
                {
                    if (Option?.DataInputTopics?.Count > 0)
                    {
                        await Client.UnsubscribeAsync(CreateUnSubscribeOptions(Option.DataInputTopics));
                    }
                    await Client.DisconnectAsync();
                }
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }

        }

        protected override IMqttClient CreateClient(DataReceiverMqttOption option)
        {
#if NET6_0
            Client = new MqttFactory().CreateMqttClient();
#elif NET8_0_OR_GREATER
            Client = new MqttClientFactory().CreateMqttClient();
#endif
            Client.ConnectedAsync += args =>
            {
                if (option.DataInputTopics?.Count > 0)
                    SubscribeMqtt(option.DataInputTopics);
                OnConnectionEvent(option, new ConnectArgs()
                {
                    IsConntion = true,
                    Message = "success"
                });
                return Task.CompletedTask;
            };
            Client.DisconnectedAsync += args =>
            {
                OnConnectionEvent(option, new ConnectArgs()
                {
                    IsConntion = false,
                    Message = args.Reason.ToString()
                });
                return Task.CompletedTask;
            };
            Client.ApplicationMessageReceivedAsync += Client_ApplicationMessageReceivedAsync;
            return Client;
        }

        protected override Task WhileDoAsync(CancellationToken token)
        {
            if (!IsConnected)
                ConnectAsync().Wait();
            //如果取消信号发出，则断开当前连接
            return Task.Run(async () =>
              {
                  while (!token.IsCancellationRequested)
                  {
                      if (token.IsCancellationRequested)
                      {
                          await DisConnectAsync();
                          return;
                      }
                      await Task.Delay(2000);
                  }

              });
        }

        /// <summary>
        /// mqtt消息接收后处理逻辑
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        protected virtual Task<Dictionary<string, Dictionary<string, ReceiverTempDataValue>>> MessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            return Task.Run(() =>
              {
                  try
                  {
                      var stringContent = arg.ApplicationMessage.ConvertPayloadToString();
                      var dic = DataParse?.Invoke(stringContent);
                      if (dic == null || dic.Count == 0)
                          return new Dictionary<string, Dictionary<string, ReceiverTempDataValue>>();

                      if (AddressRefGroupName.Count == 0)
                          return new Dictionary<string, Dictionary<string, ReceiverTempDataValue>>() { { Option.ProductLineName, dic } };

                      var result = new Dictionary<string, Dictionary<string, ReceiverTempDataValue>>();
                      foreach (var address in dic.Keys)
                      {
                          if (!AddressRefGroupName.ContainsKey(address)) continue;
                          if (!result.ContainsKey(AddressRefGroupName[address].Item1))
                              result.Add(AddressRefGroupName[address].Item1, new Dictionary<string, ReceiverTempDataValue>());
                          result[AddressRefGroupName[address].Item1].Add(AddressRefGroupName[address].Item2, dic[address]);
                      }
                      return result;
                  }
                  catch (Exception ex)
                  {
                      OnErrorEvent(this, new ExceptionArgs() { Ex = ex, Message = "Parse message of mqtt received error" });
                      return null;
                  }

              });
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        /// <summary>
        /// 接收到mqtt的数据后进行处理
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task Client_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            await Task.Run(async () =>
            {
                var list = await MessageReceivedAsync(arg);
                if (list != null)
                    foreach (var key in list.Keys)
                    {
                        await ReceiveDataToMessageChannelAsync(Option.ProductLineName, list[key]);
                    }
            });
        }

        private async void SubscribeMqtt(List<string> topics)
        {
            MqttClientSubscribeOptions mqttSubscribeOptions = CreateSubscribeOptions(topics);
            var result = await Client.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
            var badTopic = result.Items.Where(
                t => (t.ResultCode != MqttClientSubscribeResultCode.GrantedQoS0)
                && (t.ResultCode != MqttClientSubscribeResultCode.GrantedQoS1)
                && (t.ResultCode != MqttClientSubscribeResultCode.GrantedQoS2)).Select(t => t.TopicFilter.Topic).ToList();
            if (badTopic.Count > 0)
            {
                OnErrorEvent(Option, new ExceptionArgs()
                {
                    Ex = null,
                    Message = $"主题{string.Join(",", badTopic)}均订阅失败，请检查mqtt或者主题是否正常"
                });
            }
        }

        private MqttClientSubscribeOptions CreateSubscribeOptions(List<string> topics)
        {
#if NET6_0
            var mqttSubscribeOptionsBuilder = new MqttFactory().CreateSubscribeOptionsBuilder();
#elif NET8_0_OR_GREATER
            var mqttSubscribeOptionsBuilder = new MqttClientFactory().CreateSubscribeOptionsBuilder();
#endif

            topics.ForEach(topic =>
            {
                mqttSubscribeOptionsBuilder = mqttSubscribeOptionsBuilder.WithTopicFilter(topic);
            });

            var mqttSubscribeOptions = mqttSubscribeOptionsBuilder.Build();
            return mqttSubscribeOptions;
        }

        private MqttClientUnsubscribeOptions CreateUnSubscribeOptions(List<string> topics)
        {
#if NET6_0
            var mqttUnsubscribeOptionsBuilder = new MqttFactory().CreateUnsubscribeOptionsBuilder();
#elif NET8_0_OR_GREATER
            var mqttUnsubscribeOptionsBuilder = new MqttClientFactory().CreateUnsubscribeOptionsBuilder();
#endif
            topics.ForEach(topic =>
            {
                mqttUnsubscribeOptionsBuilder = mqttUnsubscribeOptionsBuilder.WithTopicFilter(topic);
            });

            var mqttSubscribeOptions = mqttUnsubscribeOptionsBuilder.Build();
            return mqttSubscribeOptions;
        }

        private Dictionary<string, ReceiverTempDataValue> JsonDataPrase(string json)
        {

            try
            {
                var data = JsonSerializer.Deserialize<DataReceiveContract>(json, ObjectToValueJsonConverter);
                if (data != null && data.Datas != null && data.Datas.Count > 0)
                {
                    return data.Datas.GroupBy(t => t.Address).ToDictionary(t => t.Key, t => new ReceiverTempDataValue(t.FirstOrDefault().Value, data.Timestamp));
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "接收mqtt数据转json发生异常");
                OnErrorEvent(Option, new ExceptionArgs()
                {
                    Code = ResultType.ServerUnKonwError.ToString(),
                    Ex = ex,
                    Message = ex.Message
                });
                return null;
            }
        }

        public override async Task<MessageResult> WriteAsync(DataWriteContract data)
        {
            return await WriteAsync(data.Key, data);
        }

        public override async Task<MessageResult> WriteAsync(DataWriteContractItem data)
        {
            return MessageResult.Failed(ResultType.NotImplemented, "Mqtt not allow this method", null);
        }

        /// <summary>
        /// topicname
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="topicname"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public override async Task<MessageResult> WriteAsync<T>(string topicname, T data)
        {
            var result = await Client.PublishStringAsync(topicname, JsonSerializer.Serialize(data, WriteDataSerializerOptions));

            if (result.IsSuccess)
                return MessageResult.Success();
            return MessageResult.Failed((int)result.ReasonCode, result.ReasonString, null);
        }

        public override async Task<DataResult<DataReceiveContract>> DirectReadAsync(IEnumerable<DataReceiveContractItem> addressArray, CancellationToken cancellationToken = default)
        {
            if (addressArray == null)
                return DataResult<DataReceiveContract>.Failed(ResultType.ParameterError, $"参数为null,the addressArray parameter is null.");
            if (addressArray.Count() == 0)
                return DataResult<DataReceiveContract>.Failed(ResultType.ParameterError, $"参数为空,the addressArray length is 0.");

            var allDatas = await ReadAllAsync(cancellationToken);
            if (!allDatas.State)
                return DataResult<DataReceiveContract>.Failed(allDatas.Code, allDatas.Message, allDatas.Error);
            DataReceiveContract data = new DataReceiveContract()
            {
                Id = iml6yu.Fingerprint.GetId(),
                Key = Option.ProductLineName,
                Timestamp = GetTimestamp(),
                Datas = new List<DataReceiveContractItem>()
            };
            foreach (var address in addressArray)
            {
                var item = allDatas.Data?.FirstOrDefault(t => t.Address == address.Address);
                data.Datas.Add(item);
            }
            return DataResult<DataReceiveContract>.Success(data);
        }
    }

}
