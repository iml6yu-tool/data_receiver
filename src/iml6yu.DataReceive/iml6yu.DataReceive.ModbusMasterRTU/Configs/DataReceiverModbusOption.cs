using iml6yu.DataReceive.Core.Configs;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataReceive.ModbusMasterRTU.Configs
{
    public class DataReceiverModbusOption : DataReceiverOption
    {

        /// <summary>
        /// 串口号
        /// </summary>
        public string ComPort { get; set; }
        /// <summary>
        /// 波特率 默认9600
        /// </summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>
        /// 校验位 默认None  
        /// </summary>
        public Parity Parity { get; set; } = Parity.None;

        /// <summary>
        /// 数据位 默认8
        /// </summary>
        public int DataBits { get; set; } = 8;
        /// <summary>
        /// 停止位 默认1
        /// </summary>
        public StopBits StopBit { get; set; } = StopBits.One;
    }
}
