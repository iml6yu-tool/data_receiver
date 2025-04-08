using iml6yu.DataReceive.PLCSiemens;
using iml6yu.DataReceive.PLCSiemens.Configs;

namespace iml6yu.DataReceiverExample
{
    public class Program
    {
        public static void Main(string[] args)
        { 
            var builder = Host.CreateApplicationBuilder(args);
            DataReceiverPlcS7Option option = builder.Configuration.GetSection("DataReceiverPlcS7").Get<DataReceiverPlcS7Option>();
            builder.Services.AddReceiver(option, true, null);


            builder.Services.AddHostedService<Worker>(); 

            var host = builder.Build();
            host.Run();
        }
    }
}