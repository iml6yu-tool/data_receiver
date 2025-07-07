using iml6yu.Data.Core;
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.ModbusMaster;
using iml6yu.DataReceive.ModbusMasterRTU.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using NModbus;
using NModbus.Serial;
using System.IO.Ports;
using System.Xml.Linq;

namespace iml6yu.DataReceive.ModbusMasterRTU
{
    public class DataReceiverModbusRTU : DataReceiverModbus<DataReceiverModbusRTUOption>
    {
        private SerialPort serial;
        public override bool IsConnected => serial != null && Client != null && serial.IsOpen;
        public DataReceiverModbusRTU(DataReceiverModbusRTUOption option, ILogger logger, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null) : base(option, logger, isAutoLoadNodeConfig, nodes)
        {

        }
        public override async Task<MessageResult> ConnectAsync()
        {
            if (IsConnected)
                return MessageResult.Success();
            else
            {
                try
                {
                    if (!serial.IsOpen)
                        serial.Open();
                    OnConnectionEvent(this.Option, new ConnectArgs(true));
                    return MessageResult.Success();

                }
                catch (Exception ex)
                {
                    OnConnectionEvent(this.Option, new ConnectArgs(false, ex.Message));
                    CreateClient(Option);
                    return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
                }
            }
        }

        public override async Task<MessageResult> DisConnectAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (serial?.IsOpen ?? false)
                        serial?.Close();
                    serial?.Dispose();
                    Client.Dispose();

                    return MessageResult.Success();
                }
                catch (Exception ex)
                {
                    return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
                }
            });
        }

        protected override IModbusMaster CreateClient(DataReceiverModbusRTUOption option)
        {
            serial = new SerialPort(option.ComPort, option.BaudRate, option.Parity, option.DataBits, option.StopBit);
            serial.Open();
            if (factory == null)
                factory = new ModbusFactory();
            Client = factory.CreateRtuMaster(serial);
            return Client;
        }
    }
}
