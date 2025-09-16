using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.OPCUA.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using OpcUaHelper;

namespace iml6yu.DataReceive.OPCUA
{
    public class DataReceiverOPCUA : DataReceiver<OpcUaClient, DataReceiverOPCUAOption>
    {
        /// <summary>
        /// 按照GroupName，Interval进行分组
        /// </summary>
        protected Dictionary<string, Dictionary<int, (List<NodeItem>, NodeId[])>> readNodes;
        public override bool IsConnected => Client?.Connected ?? false;
        public DataReceiverOPCUA(DataReceiverOPCUAOption option, ILogger logger, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null) : base(option, logger, isAutoLoadNodeConfig, nodes)
        {
        }
        public override MessageResult LoadConfig(List<NodeItem> nodes)
        {
            var r = base.LoadConfig(nodes);
            if (!r.State)
                return r;

            if (ConfigNodes == null)
                return MessageResult.Failed(ResultType.ParameterError, "", new ArgumentNullException(nameof(ConfigNodes)));
            try
            {
                readNodes = nodes.GroupBy(t => t.GroupName ?? "default").ToDictionary(t => t.Key, t => t.GroupBy(x => x.Interval).ToDictionary(x => x.Key, x => (x.ToList(), x.Select(n => new NodeId(n.FullAddress)).ToArray())));
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return MessageResult.Failed(ResultType.ParameterError, ex.Message, ex);
            }
        }
        public override async Task<MessageResult> ConnectAsync()
        {
            if (VerifyConnect())
                return MessageResult.Success(ResultType.Code201);
            try
            {
                var serviceUrl = Option.OriginPort.HasValue ? $"{Option.OriginHost}:{Option.OriginPort}" : Option.OriginHost;
                await Client.ConnectServer(serviceUrl);
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

        public override async Task<MessageResult> DisConnectAsync()
        {

            MessageResult r = await Task.Run(() =>
            {
                try
                {
                    Client?.Disconnect();
                    return MessageResult.Success();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, ex.Message);
                    return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
                }
            });
            return r;
        }

        public override async Task<MessageResult> WriteAsync(DataWriteContract data)
        {
            if (data == null || data.Datas == null || data.Datas.Count() == 0)
                return MessageResult.Failed(ResultType.ParameterError, "写入数据不能为空!");
            //判断设备连接
            if (!VerifyConnect())
                return MessageResult.Failed(ResultType.Failed, "设备未连接", null);

            var r = await Task.Run(() =>
            {
                try
                {
                    string[] address = data.Datas.Select(d => d.Address).ToArray();
                    object[] values = data.Datas.Where(d => d.Value != null).Select(d => d.Value).ToArray();
                    if (address.Length != values.Length)
                        return false;
                    return Client.WriteNodes(address, values);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, ex.Message);
                    return false;
                }
            });
            if (r)
                return MessageResult.Success();
            return MessageResult.Failed(ResultType.Failed, "写入数据失败!");
        }

        public override async Task<MessageResult> WriteAsync(DataWriteContractItem data)
        {
            if (data == null)
                return MessageResult.Failed(ResultType.ParameterError, "写入数据不能为空!");
            return await WriteAsync(data.Address, data.Value);
        }

        public override async Task<MessageResult> WriteAsync<T>(string address, T data)
        {
            if (data == null)
                return MessageResult.Failed(ResultType.ParameterError, "写入数据不能为空!");

            //判断设备连接
            if (!VerifyConnect())
                return MessageResult.Failed(ResultType.Failed, "设备未连接", null);
            try
            {
                var r = await Client.WriteNodeAsync<T>(address, data);
                if (r)
                    return MessageResult.Success();
                return MessageResult.Failed(ResultType.Failed, $"写入数据{address}({data})失败!");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return MessageResult.Failed(ResultType.Failed, $"写入数据{address}({data})发生错误，{ex.Message}", ex);
            }
        }

        protected override OpcUaClient CreateClient(DataReceiverOPCUAOption option)
        {
            Client = new OpcUaClient();
            Client.ConnectComplete += (es, ee) =>
            {
                OnConnectionEvent(option, new Data.Core.ConnectArgs()
                {
                    IsConntion = Client.Connected,
                    Message = "连接失败"
                });
            };
            Client.OpcUaName = option.ReceiverName;
            Client.ReconnectPeriod = option.ReConnectPeriod;
            if (!string.IsNullOrEmpty(option.OriginName) && !string.IsNullOrEmpty(option.OriginPwd))
                Client.UserIdentity = new UserIdentity(option.OriginName, option.OriginPwd);
            else
                Client.UserIdentity = new UserIdentity(new AnonymousIdentityToken());

            return Client;
        }

        protected override Task WhileDoAsync(CancellationToken token)
        {
            return Task.Run(() =>
            {
                //按组分
                Parallel.ForEach(readNodes.Values, item =>
                {
                    //按时间间隔分
                    Parallel.ForEach(item, async kv =>
                    {
                        while (!token.IsCancellationRequested)
                        {
                            if (VerifyConnect())
                            {
                                Dictionary<string, ReceiverTempDataValue> tempDatas = new Dictionary<string, ReceiverTempDataValue>();
                                var values = await Client.ReadNodesAsync(kv.Value.Item2.ToArray());
                                if (values == null)
                                    Logger.LogWarning("读取数据为空");
                                else if (values.Count != kv.Value.Item1.Count)
                                    Logger.LogWarning($"读取数据结果{values.Count}条，预期是{kv.Value.Item1.Count}条，摒弃不匹配的结果！");
                                else
                                {
                                    var ts = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                                    for (var i = 0; i < values.Count; i++)
                                    {
                                        tempDatas.Add(kv.Value.Item1[i].Address, new ReceiverTempDataValue(values[i], ts));
                                    }
                                    await ReceiveDataToMessageChannelAsync(Option.ProductLineName, tempDatas);
                                }
                            }
                            await Task.Delay(kv.Key);
                        }
                    });
                });
            }, token);
        }
    }
}
