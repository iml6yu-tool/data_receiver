using iml6yu.DataReceive.PLCSiemens;
using System.Text.Json;

namespace iml6yu.DataReceiverExample
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> logger;
        protected readonly DataReceiverPlcS7 receiver;

        public Worker(ILogger<Worker> logger, DataReceiverPlcS7 dataReceiver)
        {
            receiver = dataReceiver;
            this.logger = logger;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            receiver.ConnectionEvent += (sender, e) =>
            {
                if (e.IsConntion)
                {
                    logger.LogInformation("Connected to PLC");
                }
                else
                {
                    logger.LogWarning("Disconnected from PLC" + e.Message);
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
