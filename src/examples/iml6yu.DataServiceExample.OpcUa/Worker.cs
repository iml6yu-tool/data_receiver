using iml6yu.Data.Core.Models;
using iml6yu.DataService.OpcUa;
using iml6yu.DataService.OpcUa.Configs;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace iml6yu.DataServiceExample.OpcUa
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly DataServiceOpcUa dataService;
        public Worker(ILogger<Worker> logger, DataServiceOpcUaOption option)
        {
            _logger = logger;
            dataService = new DataServiceOpcUa(option, logger);
        }
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            dataService?.StartServicer(cancellationToken);
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            dataService?.StopServicer(); 
            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }
                if (dataService != null && !dataService.IsRuning)
                {
                    _logger.LogWarning("重启opcua数据服务");
                    dataService.StartServicer(stoppingToken);
                }
                if (dataService != null)
                {

                    var messagesList = await dataService.WriteAsync(new Data.Core.Models.DataWriteContract()
                    {
                        Key = "",
                        Id = 0,
                        Datas = new List<DataWriteContractItem>()
                        {
                            new DataWriteContractItem(){
                                Address = "ns=2;TestBoolean1",
                                Value=(new Random()).Next(0,10)%2==0,
                                ValueType = (int)TypeCode.Boolean,
                            },
                            new DataWriteContractItem(){
                                Address = "ns=2;TestBoolean2",
                                Value=(new Random()).Next(0,10)%2==0,
                                ValueType = (int)TypeCode.Boolean,
                            },
                            new DataWriteContractItem(){
                                Address = "ns=2;TestInt1",
                                Value=(new Random()).Next(0,100),
                                ValueType = (int)TypeCode.Int32,
                            },
                            new DataWriteContractItem(){
                                Address = "ns=2;TestInt2",
                                Value=(new Random()).Next(0,100),
                                ValueType = (int)TypeCode.Int64,
                            },
                            new DataWriteContractItem(){
                                Address = "ns=2;TestDouble1",
                                Value=(new Random()).Next(0,100)/100d,
                                ValueType = (int)TypeCode.Double,
                            },
                            new DataWriteContractItem(){
                                Address = "ns=2;TestFloat1",
                                Value=(new Random()).Next(0,100)/100f,
                                ValueType = (int)TypeCode.Single,
                            }
                        }
                    });
                    _logger.LogInformation(System.Text.Json.JsonSerializer.Serialize(messagesList,
                        new System.Text.Json.JsonSerializerOptions()
                        {
                            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                        }));
                    var message = await dataService.WriteAsync(new Data.Core.Models.DataWriteContractItem()
                    {
                        Address = "ns=2;TestDateTime",
                        Value = DateTime.Now,
                        ValueType = (int)TypeCode.DateTime,
                    });
                    _logger.LogInformation(System.Text.Json.JsonSerializer.Serialize(message,
                        new System.Text.Json.JsonSerializerOptions()
                        {
                            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                        }));
                    message = await dataService.WriteAsync("ns=2;TestString", $"iml6yu_{DateTime.Now.ToLongTimeString()}");
                    _logger.LogInformation(System.Text.Json.JsonSerializer.Serialize(message,
                      new System.Text.Json.JsonSerializerOptions()
                      {
                          Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                      }));
                }
                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}
