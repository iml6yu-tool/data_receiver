using iml6yu.DataService.Modbus.Configs;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace iml6yu.DataService.ModbusRTU.Configs
{
    public class DataServiceModbusOptionRTU : DataServiceModbusOption
    {
        #region RTU
        /// <summary>
        /// 端口号
        /// </summary>
        public string? ComName { get; set; }
        /// <summary>
        /// 波特率
        /// </summary>
        public int? BaudRate { get; set; }
        /// <summary>
        /// 校验位
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Parity? Parity { get; set; }
        /// <summary>
        /// 数据位
        /// </summary>
        public int? DataBits { get; set; }
        /// <summary>
        /// 停止位
        /// </summary>
        public StopBits? StopBits { get; set; }
        #endregion
    }
}
