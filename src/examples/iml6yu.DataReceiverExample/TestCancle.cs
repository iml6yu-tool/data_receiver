using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataReceiverExample.S7
{
    public class TestCancle : BackgroundService
    {
        private readonly ILogger<TestCancle> logger;


        public TestCancle(ILogger<TestCancle> logger)
        {

            this.logger = logger;
        }
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {

                while (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation("TestCancle is running.");

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    }
                    catch (Exception ex)
                    {

                        throw ex;
                    }
                  
                }
            });
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return base.StopAsync(cancellationToken);
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
