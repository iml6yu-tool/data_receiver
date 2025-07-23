using iml6yu.DataService.Modbus;
using iml6yu.DataService.Modbus.Configs;
using Microsoft.Extensions.Logging;
using NModbus;
using System.Net;

namespace iml6yu.DataService.ModbusTCP
{
    public class DataServiceModbusTCP : DataServiceModbus
    {
        private System.Net.Sockets.TcpListener? listener;

        public DataServiceModbusTCP(DataServiceModbusOption option, ILogger logger) : base(option, logger)
        {
        }

        protected override IModbusSlaveNetwork CreateNetWork(DataServiceModbusOption option)
        {
            IPAddress iPAddress = IPAddress.Any;
            if (!string.IsNullOrEmpty(option.IPAddress))
                iPAddress = IPAddress.Parse(option.IPAddress);
            int port = option.Port.HasValue ? option.Port.Value : 502;
            listener = new System.Net.Sockets.TcpListener(iPAddress, port);
            IModbusSlaveNetwork network = Factory.CreateSlaveNetwork(listener);
            listener.Start();
            return network;
        }
    }
}
