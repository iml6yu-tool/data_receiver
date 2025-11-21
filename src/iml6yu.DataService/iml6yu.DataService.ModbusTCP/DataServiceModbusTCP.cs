using iml6yu.DataService.Modbus;
using iml6yu.DataService.Modbus.Configs;
using iml6yu.DataService.ModbusTCP.Configs;
using Microsoft.Extensions.Logging;
using NModbus;
using System.Net;

namespace iml6yu.DataService.ModbusTCP
{
    public class DataServiceModbusTCP : DataServiceModbus<DataServiceModbusOptionTCP>
    {
        private System.Net.Sockets.TcpListener? listener;

        public override bool IsRuning
        {
            get
            {
                return Network != null && listener != null && listener.Server.Connected;
            }
        }
        public DataServiceModbusTCP() : base(null, null)
        {
        }
        public DataServiceModbusTCP(DataServiceModbusOptionTCP option) : base(option, null)
        {
        }

        public DataServiceModbusTCP(DataServiceModbusOptionTCP option, ILogger logger) : base(option, logger)
        {
        }

        protected override IModbusSlaveNetwork CreateNetWork(DataServiceModbusOptionTCP option)
        {
            IPAddress iPAddress = IPAddress.Any;
            if (!string.IsNullOrEmpty(option.IPAddress))
                iPAddress = IPAddress.Parse(option.IPAddress);
            int port = option.Port;
#if NET6_0
            listener?.Server.Close();
#endif
#if NET7_0_OR_GREATER
            listener?.Server.Close();
            listener?.Dispose();
#endif
            listener = new System.Net.Sockets.TcpListener(iPAddress, port);
            IModbusSlaveNetwork network = Factory.CreateSlaveNetwork(listener);
            listener.Start();
            return network;
        }
    }
}
