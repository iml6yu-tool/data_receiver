using iml6yu.DataService.Modbus;
using iml6yu.DataService.Modbus.Configs;
using iml6yu.DataService.ModbusRTU.Configs;
using Microsoft.Extensions.Logging;
using NModbus;
using NModbus.Serial;
using System.IO.Ports;

namespace iml6yu.DataService.ModbusRTU
{
    public class DataServiceModbusRTU : DataServiceModbus<DataServiceModbusOptionRTU>
    {
        private System.IO.Ports.SerialPort? serialPort;
        public override bool IsRuning
        {
            get
            {
                return Network != null && serialPort != null && serialPort.IsOpen;
            }
        }
        public DataServiceModbusRTU(DataServiceModbusOptionRTU option, ILogger logger) : base(option, logger)
        {
        }

        protected override IModbusSlaveNetwork CreateNetWork(DataServiceModbusOptionRTU option)
        {
            serialPort = new SerialPort(option.ComName, option.BaudRate.Value);
            if (option.Parity != null)
                serialPort.Parity = option.Parity.Value;
            if (option.DataBits != null)
                serialPort.DataBits = option.DataBits.Value;
            if (option.StopBits != null)
                serialPort.StopBits = option.StopBits.Value;

            IModbusSlaveNetwork network = Factory.CreateRtuSlaveNetwork(serialPort);
            serialPort.Open();

            return network;
        }

        public override void StopServicer()
        {
            if (serialPort?.IsOpen ?? false)
            {
                serialPort.Close();
                serialPort.Dispose();
            }
            base.StopServicer();
        }
    }
}
