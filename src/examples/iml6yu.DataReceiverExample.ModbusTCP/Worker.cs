using iml6yu.DataCenter.ReBalance.Services;
using iml6yu.DataReceive.ModbusMasterTCP;
using iml6yu.DataReceive.ModbusMasterTCP.Configs;
using System.Text.Json;

namespace iml6yu.DataReceiverExample.ModbusTCP
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> logger;
        private readonly DataReceiverModbusTCP receiver;
        private readonly ReBalanceService reBalanceService;
        public Worker(ILogger<Worker> logger, DataReceiverModbusTCP dataReceiver, iml6yu.DataCenter.ReBalance.Services.ReBalanceService reBalance)
        {
            reBalanceService = reBalance;
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
            _ = reBalanceService.StartInsertReBalanceService();
            _ = receiver.StartWorkAsync(cancellationToken);
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            reBalanceService.StopInsertReBalanceService();
            await receiver.StopWorkAsync();
            await base.StopAsync(cancellationToken); 
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }
                await Task.Delay(1000000, stoppingToken);
            }
        }
    }
}
