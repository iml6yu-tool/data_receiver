using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.SerialCommunicate.Configs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataReceive.SerialCommunicate
{
    /// <summary>
    /// 串口通信类库 暂时先不写了。
    /// </summary>
    public class DataReceiverSerialCommunicateDefault : DataReceiverSerialCommunicate
    {
        public DataReceiverSerialCommunicateDefault(DataReceiverSerialCommunicateOption option, ILogger logger, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null) : base(option, logger, isAutoLoadNodeConfig, nodes)
        {
        }

        protected override Dictionary<string, ReceiverTempDataValue> DefaultDataParse(byte[] data)
        {
            throw new NotImplementedException();
        }
    }
}
