using iml6yu.DataReceive.ModbusMasterTCP;
using iml6yu.DataReceive.ModbusMasterTCP.Configs;
using System.Diagnostics;

namespace iml6yu.DataReceiverExample.ModbusTCP
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
           
            DataReceiverModbusTCPOption option = builder.Configuration.GetSection("DataReceiverOption").Get<DataReceiverModbusTCPOption>();
            builder.Services.AddReceiver(option, true, null);
        
            builder.Services.AddHostedService<Worker>();

            IHost host = builder.Build();
          
            host.Run();
        }
    }
}