using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.SerialCommunicate.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace iml6yu.DataReceive.SerialCommunicate
{
    public static class DataReceiverSerialCommunicateExtension
    {
        public static IServiceCollection AddSerialCommunicateReceiver(this IServiceCollection services, DataReceiverSerialCommunicateOption option, Func<string, Dictionary<string, ReceiverTempDataValue>> dataParse, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton<DataReceiverSerialCommunicate>(provider =>
            {
                var logFactory = provider.GetService<ILoggerFactory>();
                var log = logFactory.CreateLogger<DataReceiverSerialCommunicate>();
                var instance = new DataReceiverSerialCommunicate(option, log, isAutoLoadNodeConfig, nodes);
                instance.SetDataParse(dataParse);
                return instance;
            });
            return services;
        }

        public static IServiceCollection AddReceiver(this IServiceCollection services, DataReceiverSerialCommunicateOption option, Func<string, Dictionary<string, ReceiverTempDataValue>> dataParse, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton<DataReceiverSerialCommunicate>(provider =>
            {
                var logFactory = provider.GetService<ILoggerFactory>();
                var log = logFactory.CreateLogger<DataReceiverSerialCommunicate>();
                var instance = new DataReceiverSerialCommunicate(option, log, isAutoLoadNodeConfig, nodes);
                instance.SetDataParse(dataParse);
                return instance;
            });
            return services;
        }
    }
}