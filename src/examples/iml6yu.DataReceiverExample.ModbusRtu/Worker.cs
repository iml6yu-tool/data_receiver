using iml6yu.DataReceive.ModbusMasterRTU;
using iml6yu.DataReceive.ModbusMasterRTU.Configs;
using System.Text.Json;

namespace iml6yu.DataReceiverExample.ModbusRtu
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> logger;
        private readonly DataReceiverModbusRTU receiver;
        public Worker(ILogger<Worker> logger, DataReceiverModbusRTU dataReceiver)
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
                    logger.LogInformation("Connected to deiver {0}",((DataReceiverModbusRTUOption)sender).ReceiverName);
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
            receiver.StartWorkAsync(cancellationToken);
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
            await receiver.StopWorkAsync();
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
