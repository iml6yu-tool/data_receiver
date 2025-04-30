using iml6yu.DataReceive.Core.Configs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace iml6yu.DataReceive.Core
{
    public class DataReceiverFactory
    {
        public class ProviderConfig
        {
            public ProviderConfig(string providerFileName, string providerName)
            {
                ProviderFileName = providerFileName;
                ProviderName = providerName;
            }

            public string ProviderFileName { get; set; }

            public string ProviderName { get; set; }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="section"></param>
        /// <param name="logger"></param>
        /// <param name="optionProvider"></param>
        /// <param name="receiverProvider"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static IDataReceiver Create(IConfigurationSection section, ILogger logger, ProviderConfig optionProvider, ProviderConfig receiverProvider,out DataReceiverOption option)
        {

            IDataReceiver receiver; 
            if (string.IsNullOrEmpty(optionProvider.ProviderFileName))
            {
                option = Activator.CreateInstance(Type.GetType(optionProvider.ProviderName)) as DataReceiverOption;
            }
            else
            {
                var ass = Assembly.LoadFrom(optionProvider.ProviderFileName);
                option = ass.CreateInstance(optionProvider.ProviderName) as DataReceiverOption;
            }

            if (string.IsNullOrEmpty(receiverProvider.ProviderFileName))
            {
                receiver = Activator.CreateInstance(Type.GetType(receiverProvider.ProviderName), true, BindingFlags.Public
               , null, new object[] { option, logger }, System.Globalization.CultureInfo.CurrentCulture
              , null) as IDataReceiver;
            }
            else
            {
                var ass = Assembly.LoadFrom(receiverProvider.ProviderFileName);
                receiver = ass.CreateInstance(receiverProvider.ProviderName, true, BindingFlags.Public
               , null, new object[] { option, logger }, System.Globalization.CultureInfo.CurrentCulture
              , null) as IDataReceiver;
            }
            if (receiver == null)
                throw new ArgumentException($"设备采集参数配置错误，无法实例化.\r\n{JsonSerializer.Serialize(optionProvider)}\r\n{JsonSerializer.Serialize(receiverProvider)}");
           
            return receiver; 
        }
    }
}
