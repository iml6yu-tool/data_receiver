using iml6yu.Data.Core;
using iml6yu.Data.Core.JsonConverts;
using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.Mqtt.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using System.Net.Http;
using System.Text.Json;

namespace iml6yu.DataReceive.Mqtt
{
    public class DataReceiverMqtt : DataReceiver<IMqttClient, DataReceiverMqttOption, string>
    {
        public DataReceiverMqtt(DataReceiverMqttOption option, ILogger logger, Func<string, Dictionary<string, ReceiverTempDataValue>> dataParse, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null, CancellationTokenSource tokenSource = null) : base(option, logger, dataParse, isAutoLoadNodeConfig, nodes, tokenSource)
        {

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
            Client = new MqttFactory().CreateMqttClient();
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

        protected override Task DoAsync(CancellationTokenSource tokenSource)
        {
            if (!IsConnected)
                ConnectAsync().Wait();
            //如果取消信号发出，则断开当前连接
            return Task.Run(async () =>
              {
                  if (tokenSource.IsCancellationRequested)
                      await DisConnectAsync();
              });
        }

        /// <summary>
        /// mqtt消息接收后处理逻辑
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        protected virtual Task<Dictionary<string, ReceiverTempDataValue>> MessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            return Task.Run(() =>
              {
                  try
                  {
                      var stringContent = arg.ApplicationMessage.ConvertPayloadToString();
                      var dic = DataParse?.Invoke(stringContent);
                      return dic;
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
            if (State == ReceiverState.Working)
            {
                await Task.Run(async () =>
                {
                    var list = await MessageReceivedAsync(arg);
                    if (list != null)
                        await ReceiveDataToMessageChannelAsync(list);
                });
            }
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
            var mqttSubscribeOptionsBuilder = new MqttFactory().CreateSubscribeOptionsBuilder();
            topics.ForEach(topic =>
            {
                mqttSubscribeOptionsBuilder = mqttSubscribeOptionsBuilder.WithTopicFilter(topic);
            });

            var mqttSubscribeOptions = mqttSubscribeOptionsBuilder.Build();
            return mqttSubscribeOptions;
        }

        private MqttClientUnsubscribeOptions CreateUnSubscribeOptions(List<string> topics)
        {
            var mqttUnsubscribeOptionsBuilder = new MqttFactory().CreateUnsubscribeOptionsBuilder();
            topics.ForEach(topic =>
            {
                mqttUnsubscribeOptionsBuilder = mqttUnsubscribeOptionsBuilder.WithTopicFilter(topic);
            });

            var mqttSubscribeOptions = mqttUnsubscribeOptionsBuilder.Build();
            return mqttSubscribeOptions;
        }
    }

}
