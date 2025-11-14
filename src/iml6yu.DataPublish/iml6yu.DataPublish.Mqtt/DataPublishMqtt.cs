using iml6yu.Data.Core;
using iml6yu.DataPublish.Core;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using MQTTnet; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if NET6_0
using MQTTnet.Client;
#endif

namespace iml6yu.DataPublish.Mqtt
{
    public class DataPublishMqtt<TPushContent> : DataPublisher<IMqttClient, DataPublisherOption, TPushContent>
    {
        private MqttClientPublishResult publishResult;

        public DataPublishMqtt(DataPublisherOption option, ILogger logger) : base(option, logger)
        {
        }

     

        protected override bool IsConnected
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
                    .WithClientId($"iml6yu.Publisher.{Option.PublisherName}.{DateTime.Now.ToString("yyyyMMddHHmmssfff")}")
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

        public override void Dispose()
        {
            base.Dispose();
        }

        public override async Task<MessageResult> PushDataAsync(string channelName, TPushContent data)
        {
            if (data is string)
                publishResult = await Client.PublishStringAsync(channelName, data as string, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            else
                publishResult = await Client.PublishStringAsync(channelName, System.Text.Json.JsonSerializer.Serialize(data), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            if (publishResult.IsSuccess) return MessageResult.Success();
            return MessageResult.Failed(ResultType.ServerDoApiError, publishResult.ReasonString, null);
        }

        protected override IMqttClient CreateClient(DataPublisherOption option)
        {
#if NET6_0
            Client = new MqttFactory().CreateMqttClient();
#elif NET8_0_OR_GREATER
            Client = new MqttClientFactory().CreateMqttClient();
#endif
            Client.ConnectedAsync += args =>
            {
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
            return Client;
        }
    }
}
