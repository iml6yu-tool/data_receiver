using iml6yu.DataService.OpcUa;
using iml6yu.DataService.OpcUa.Configs;

namespace iml6yu.DataServiceExample.OpcUa
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddHostedService<Worker>();

             DataServiceOpcUaOption? option
                = builder.Configuration.GetSection("option").Get<DataServiceOpcUaOption>();

            if(option!=null)
            {
                builder.Services.AddSingleton(option); 
            }

            var host = builder.Build();
            host.Run();
        }
    }
}