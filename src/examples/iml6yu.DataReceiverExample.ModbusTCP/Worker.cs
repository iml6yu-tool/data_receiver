using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.ModbusMasterTCP;
using iml6yu.DataReceive.ModbusMasterTCP.Configs;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace iml6yu.DataReceiverExample.ModbusTCP
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> logger;
        private readonly DataReceiverModbusTCP receiver;
        public Worker(ILogger<Worker> logger, DataReceiverModbusTCP dataReceiver)
        {

            this.receiver = dataReceiver;
            this.logger = logger;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {


            receiver.ConnectionEvent += (sender, e) =>
            {
                if (e.IsConntion)
                {
                    logger.LogInformation("Connected to deiver {0}", ((DataReceiverModbusTCPOption)sender).ReceiverName);
                }
                else
                {
                    logger.LogWarning("Disconnected from deiver" + e.Message);
                }
            };

            receiver.ErrorEvent += (sender, e) =>
            {
                logger.LogError(e.Ex, "Error occurred: {Message}", e.Message);
            };

            receiver.WarnEvent += (sender, e) =>
            {
                logger.LogWarning("Warning: {Message}", e.Message);
            };

            receiver.DataChangedEvent += (sender, e) =>
            {
                logger.LogInformation("Data changed: at {TagName} \r\n {Value}", DateTimeOffset.FromUnixTimeMilliseconds(e.Timestamp).ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    JsonSerializer.Serialize(e.Datas));
            };
            //receiver.DataIntervalEvent += async (sender, e) =>
            //{
            //    await reBalanceService.AddReBalance(e);
            //};

            _ = receiver.StartWorkAsync(cancellationToken);
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {

            await receiver.StopWorkAsync();
            await base.StopAsync(cancellationToken);
        }
        int index = 0;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }
                //var a = await receiver.DirectReadAsync([new DataReadItem() {
                //    Address ="1.Coils.0",
                //    ValueType =TypeCode.Boolean
                //},new DataReadItem() {
                //    Address ="1.Coils.1",
                //    ValueType =TypeCode.Boolean
                //},new DataReadItem() {
                //    Address ="1.Coils.2",
                //    ValueType =TypeCode.Boolean
                //}]);

                //Console.ForegroundColor = ConsoleColor.Green;
                //Console.WriteLine("主动读取数据");
                //Console.WriteLine(JsonSerializer.Serialize(a));
                //Console.ForegroundColor = ConsoleColor.White;
                index++;
                //if (index % 3 == 0)
                //{
                //var x = await receiver.WriteWithVerifyAsync(new DataWriteContract()
                //{
                //    Id = index,
                //    Key = "",
                //    Datas = [new DataWriteContractItem() {
                //        Address ="1.Coils.4",
                //        Value = true,
                //        ValueType =TypeCode.Boolean
                //    },new DataWriteContractItem() {
                //        Address ="1.Coils.5",
                //        Value = index%2,
                //        ValueType =TypeCode.Boolean
                //    },new DataWriteContractItem() {
                //        Address ="1.Coils.6",
                //        Value = true,
                //        ValueType =TypeCode.Boolean,
                //        IsFlag=true
                //    }]
                //});

                var x = await receiver.WriteAsync(new DataWriteContract()
                {
                    Id = index,
                    Key = "",
                    Datas = [new DataWriteContractItem() {
                            Address ="1.Coils.0",
                            Value = true,
                            ValueType =TypeCode.Boolean
                        }]
                });
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"写入结果:{JsonSerializer.Serialize(x, new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                })}");
                Console.ForegroundColor = ConsoleColor.White;
                //}
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
