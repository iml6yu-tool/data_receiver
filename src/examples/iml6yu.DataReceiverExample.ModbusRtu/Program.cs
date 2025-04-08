using iml6yu.DataReceive.ModbusMasterRTU;
using iml6yu.DataReceive.ModbusMasterRTU.Configs;

namespace iml6yu.DataReceiverExample.ModbusRtu
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            DataReceiverModbusOption option = builder.Configuration.GetSection("DataReceiverOption").Get<DataReceiverModbusOption>();
            builder.Services.AddReceiver(option, true, null);

            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}