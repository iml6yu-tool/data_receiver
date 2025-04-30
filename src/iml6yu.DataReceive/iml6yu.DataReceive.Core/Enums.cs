using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataReceive.Core
{
    /// <summary>
    /// 接收类型枚举
    /// </summary>
    public enum ReceiverType
    {
        /// <summary>
        /// Mqtt
        /// </summary>
        Mqtt = 0,
        /// <summary>
        /// Modbus TCP
        /// </summary>
        ModbusTCP = 1,
        /// <summary>
        /// Modbus RTU
        /// </summary>
        ModbusRTU = 2,
        /// <summary>
        /// S7NetPlc
        /// </summary>
        S7NetPlc = 3,
        /// <summary>
        /// OPCUA
        /// </summary>
        OPCUA = 4,
    }
}
