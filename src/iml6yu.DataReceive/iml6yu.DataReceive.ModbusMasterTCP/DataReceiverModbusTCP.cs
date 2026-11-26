using iml6yu.Data.Core;
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.ModbusMaster;
using iml6yu.DataReceive.ModbusMasterTCP.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using NModbus;
using System.Net.Sockets;

namespace iml6yu.DataReceive.ModbusMasterTCP
{
    public class DataReceiverModbusTCP : DataReceiverModbus<DataReceiverModbusTCPOption>
    {
        private TcpClient tcp;
        public override bool IsConnected => tcp != null && Client != null && tcp.Connected;

        public DataReceiverModbusTCP(DataReceiverModbusTCPOption option, ILogger logger, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null) : base(option, logger, isAutoLoadNodeConfig, nodes)
        {
        }

        public override async Task<MessageResult> ConnectAsync()
        {
            if (IsConnected)
                return MessageResult.Success();
            else
            {
                try
                {
                    if (!tcp.Connected)
                        await tcp.ConnectAsync(Option.OriginHost ?? "127.0.0.1", Option.OriginPort ?? 502);
                    OnConnectionEvent(this.Option, new ConnectArgs(true));
                    return MessageResult.Success();
                }
                catch (Exception ex)
                {
                    OnConnectionEvent(this.Option, new ConnectArgs(false, ex.Message));
                    await DisConnectAsync();
                    CreateClient(Option);
                    return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
                }
            }
        }

        public override async Task<MessageResult> DisConnectAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (tcp?.Connected ?? false)
                        tcp?.Close();
                    tcp?.Dispose();
                    Client.Dispose();

                    return MessageResult.Success();
                }
                catch (Exception ex)
                {
                    return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
                }
            });
        }

        protected override IModbusMaster CreateClient(DataReceiverModbusTCPOption option)
        {
            tcp = new TcpClient();//option.OriginHost, option.OriginPort ?? 502 
            if (factory == null)
                factory = new ModbusFactory();
            Client = factory.CreateMaster(tcp);
            return Client;
        }

        protected override Task WhileDoAsync(CancellationToken token)
        {

            return Task.Run(async () =>
            {
                //按照分组进行多线程并行
                await Parallel.ForEachAsync(readNodes, async (readNode, groupToken) =>
                  {
                      await Parallel.ForEachAsync(readNode.Value, async (item, intervalToken) =>
                           {
                           while (!token.IsCancellationRequested)
                           {
                               try
                               {
                                   if (VerifyConnect())
                                   {
                                       Dictionary<string, ReceiverTempDataValue> tempDatas = new Dictionary<string, ReceiverTempDataValue>();

                                       foreach (var readConfig in item.Value)
                                       {
                                           foreach (var node in readConfig.ReadItems)
                                           {
                                               tempDatas = ReadModbusNodeItem(readConfig, node, tempDatas);
                                           }
                                       }
                                       await ReceiveDataToMessageChannelAsync(Option.ProductLineName, tempDatas);
                                   }
                                   await Task.Delay(item.Key == 0 ? 500 : item.Key);
                               }
                               catch (Exception ex)
                               {
                                   Logger.LogError(ex, "ModbusTCP Read Error:{0}", ex.Message);
                                   await Task.Delay(item.Key == 0 ? 500 : item.Key);
                               }
                           }
                       });

                  });
            }, token);
        }
    }
}
