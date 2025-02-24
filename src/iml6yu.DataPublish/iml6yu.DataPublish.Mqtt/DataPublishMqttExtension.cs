using iml6yu.DataPublish.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataPublish.Mqtt
{
    public static class DataPublishMqttExtension
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TPushContent">推送数据的类型</typeparam>
        /// <param name="services"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">主要是参数isAutoLoadNodeConfig结合nodes的使用，如果isAutoLoadNodeConfig=false,则不涉及到任何nodes的配置</exception>

        public static IServiceCollection AddPublisher<TPushContent>(this IServiceCollection services, DataPublisherOption option)

        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton(provider =>
            {
                var logFactory = provider.GetService<ILoggerFactory>();
                var log = logFactory.CreateLogger<DataPublishMqtt<TPushContent>>();
                return new DataPublishMqtt<TPushContent>(option, log);
            });
            return services;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TPushContent">推送数据的类型</typeparam>
        /// <param name="services"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">主要是参数isAutoLoadNodeConfig结合nodes的使用，如果isAutoLoadNodeConfig=false,则不涉及到任何nodes的配置</exception>

        public static IServiceCollection AddPublisher(this IServiceCollection services, DataPublisherOption option)

        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton(provider =>
            {
                var logFactory = provider.GetService<ILoggerFactory>();
                var log = logFactory.CreateLogger<DataPublishMqtt<object>>();
                return new DataPublishMqtt<object>(option, log);
            });
            return services;
        }
    }

}
