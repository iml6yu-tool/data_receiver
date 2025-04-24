using iml6yu.DataCenter.ReBalance;
using iml6yu.DataReceive.ModbusMasterTCP;
using iml6yu.DataReceive.ModbusMasterTCP.Configs;
using SqlSugar;
using System.Diagnostics;

namespace iml6yu.DataReceiverExample.ModbusTCP
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddSingleton<ISqlSugarClient>(s =>
            { 
                //Scoped”√SqlSugarClient 
                SqlSugarClient sqlSugar = new SqlSugarClient(
                    builder.Configuration.GetSection("OrderConnection").Get<ConnectionConfig>(),
                    db => {
                        db.Aop.OnError = e =>
                        {
                            Debug.WriteLine(e);
                        };
                        db.Aop.OnLogExecuting = (sql, paras) => {
                            Debug.WriteLine("---------------------------");
                            Debug.WriteLine(sql);
                            Debug.WriteLine(paras);
                            Debug.WriteLine("---------------------------");
                        };
                    });
                return sqlSugar;
            });
            DataReceiverModbusOption option = builder.Configuration.GetSection("DataReceiverOption").Get<DataReceiverModbusOption>();
            builder.Services.AddReceiver(option, true, null);
            builder.Services.AddRebalanceSingleton();
            builder.Services.AddHostedService<Worker>();

            IHost host = builder.Build();
            host.UseReBalance("L01", "L02", "L03", "L04","L01");
            host.Run();
        }
    }
}