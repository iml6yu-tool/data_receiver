using iml6yu.DataReceive.Core.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataReceive.Mqtt.Configs
{
    public class DataReceiverMqttOption:DataReceiverOption
    {

        /// <summary>
        /// 数据输入topic，也就是订阅的topic
        /// </summary>
        public List<string> DataInputTopics { get; set; } 
    }
}
