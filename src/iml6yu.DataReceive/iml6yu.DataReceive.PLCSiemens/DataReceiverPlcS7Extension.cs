﻿using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.PLCSiemens;
using iml6yu.DataReceive.PLCSiemens.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace iml6yu.DataReceive.PLCSiemens
{
    public static class DataReceiverPlcS7Extension
    {
        /// <summary>
        /// 注入s7netplc数据接收器 
        /// </summary>
        /// <param name="services"></param>
        /// <param name="option">配置参数</param>
        /// <param name="isAutoLoadNodeConfig">是否自动加载node配置</param>
        /// <param name="nodes">node节点，如果isAutoLoadNodeConfig是true,并且nodes不是null，则使用nodes，如果nodes是null,则使用option中的节点路径读取配置json,如果都不存在则抛出异常</param>
        /// <param name="stopTokenSource">停止标识位，可null</param>
        /// <returns>services</returns>
        /// <exception cref="ArgumentNullException">主要是参数isAutoLoadNodeConfig结合nodes的使用，如果isAutoLoadNodeConfig=false,则不涉及到任何nodes的配置</exception>
        public static IServiceCollection AddReceiver(this IServiceCollection services, DataReceiverPlcS7Option option, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null)

        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton<DataReceiverPlcS7>(provider =>
            {
                var logFactory = provider.GetService<ILoggerFactory>();
                var log = logFactory.CreateLogger<DataReceiverPlcS7>();
                return new DataReceiverPlcS7(option, log, isAutoLoadNodeConfig, nodes);
            });
            return services;
        }
    }
}



