using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.Mqtt.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;

namespace iml6yu.DataReceive.Mqtt
{
    public static class DataReceiverMqttExtension
    {
        public static IServiceCollection AddMqttReceiver(this IServiceCollection services, DataReceiverMqttOption option, Func<string, Dictionary<string, ReceiverTempDataValue>> dataParse, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null, CancellationTokenSource stopTokenSource = null)

        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton<DataReceiverMqtt>(provider =>
            {
                var logFactory = provider.GetService<ILoggerFactory>();
                var log = logFactory.CreateLogger<DataReceiverMqtt>();
                return new DataReceiverMqtt(option, log, dataParse, isAutoLoadNodeConfig, nodes, stopTokenSource);
            });
            return services;
        }
    }
}



