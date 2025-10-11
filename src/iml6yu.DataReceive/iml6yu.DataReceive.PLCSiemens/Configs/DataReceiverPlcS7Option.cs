using iml6yu.DataReceive.Core.Configs;
using S7.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace iml6yu.DataReceive.PLCSiemens.Configs
{
    public class DataReceiverPlcS7Option : DataReceiverOption
    {
        /// <summary>
        /// PLC类型 默认S7-1200
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CpuType CpuType { get;   set; } = CpuType.S71200;

        /// <summary>
        /// PLC机架 默认0
        /// </summary>
        public int Rack { get;   set; } = 0;

        /// <summary>
        /// PLC槽位 默认2
        /// </summary>
        public int Slot { get; set; } = 2;
    }
}
