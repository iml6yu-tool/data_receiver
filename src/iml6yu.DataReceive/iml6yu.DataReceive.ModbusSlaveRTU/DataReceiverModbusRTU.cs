using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Mqtt.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using NModbus;
using System.IO.Ports;

namespace iml6yu.DataReceive.ModbusSlaveRTU
{
    public class DataReceiverModbusRTU : DataReceiver<NModbus.IModbusSlave, DataReceiverModbusOption, string>
    {
        private SerialPort serial;
        public DataReceiverModbusRTU(DataReceiverModbusOption option, ILogger logger, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null, CancellationTokenSource tokenSource = null) : base(option, logger, isAutoLoadNodeConfig, nodes, tokenSource)
        {
        }

        public override bool IsConnected => Client !=null && Client.;

        public override Task<MessageResult> ConnectAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<MessageResult> DisConnectAsync()
        {
            throw new NotImplementedException();
        }

        protected override IModbusSlave CreateClient(DataReceiverModbusOption option)
        {
            serial =new SerialPort (option.ComPort, option.BaudRate, option.Parity, option.DataBits, option.StopBit);

            ModbusSerialSlave


        }

        protected override Task DoAsync()
        {
            throw new NotImplementedException();
        }
    }
}
