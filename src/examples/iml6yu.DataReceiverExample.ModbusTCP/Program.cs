using iml6yu.DataReceive.ModbusMasterTCP;
using iml6yu.DataReceive.ModbusMasterTCP.Configs;

namespace iml6yu.DataReceiverExample.ModbusTCP
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