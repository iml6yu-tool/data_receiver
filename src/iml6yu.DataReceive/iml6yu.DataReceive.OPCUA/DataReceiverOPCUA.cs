using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.OPCUA.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using OpcUaHelper;
using System.Linq;

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
                return MessageResult.Failed(ResultType.DeviceConnectionError, "设备未连接", null);

            var r = await Task.Run(() =>
            {
                try
                {
                    string[] address = data.Datas.Select(d => d.Address).ToArray();
                    object?[] values = data.Datas.Where(d => d.Value != null).Select(d => d.Value).ToArray();
                    if (values == null || address.Length != values.Length)
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

        public override async Task<MessageResult> WriteWithVerifyAsync(DataWriteContract data)
        {
            if (data == null || data.Datas == null || data.Datas.Count() == 0)
                return MessageResult.Failed(ResultType.ParameterError, "写入数据不能为空!");
            //判断设备连接
            if (!VerifyConnect())
                return MessageResult.Failed(ResultType.DeviceConnectionError, "设备未连接", null);

            if (data.Datas.Count(t => t.IsFlag) > 1)
                return MessageResult.Failed(ResultType.ParameterError, "标志位最多只能有1个", null);

            try
            {
                var flag = data.Datas.FirstOrDefault(t => t.IsFlag);
                string[] address;
                object?[] values;
                if (flag == null)
                {
                    address = data.Datas.Select(d => d.Address).ToArray();
                    values = data.Datas.Where(d => d.Value != null).Select(d => d.Value).ToArray();
                }
                else
                {
                    address = data.Datas.Where(t => !t.IsFlag).Select(d => d.Address).ToArray();
                    values = data.Datas.Where(d => d.Value != null && !d.IsFlag).Select(d => d.Value).ToArray();
                }
                if (values == null || address.Length != values.Length)
                    return MessageResult.Failed(ResultType.Failed, "写入数据失败，并非所有需要写入的地址都给了有效数值！");
                var writeResult = Client.WriteNodes(address, values);
                if (!writeResult)
                    return MessageResult.Failed(ResultType.Failed, "写入数据失败!");

                #region 这里不做读取判断了，依赖OPC的写入类库的写入结果作为判断
                //var newValues = await DirectReadAsync(data.Datas.Select(t => (DataReceiveContractItem)t).ToArray());

                //foreach(var newV in newValues)
                //逻辑没写完，要考虑到读，结果是否一致，如果读取失败怎么处理咱么也没想好，不可能一直读下去
                #endregion

                if (flag != null)
                {
                    if (Client.WriteNode(flag.Address, flag.Value))
                        return MessageResult.Success();

                    return MessageResult.Failed(ResultType.Failed, "写入数据失败!");
                }
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }


            return MessageResult.Success();

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
                            try
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
                                        var ts = GetTimestamp();
                                        for (var i = 0; i < values.Count; i++)
                                        {
                                            tempDatas.Add(kv.Value.Item1[i].Address, new ReceiverTempDataValue(values[i].Value, ts));
                                        }
                                        await ReceiveDataToMessageChannelAsync(Option.ProductLineName, tempDatas);
                                    }
                                }
                                if (token.IsCancellationRequested)
                                    return;
                                await Task.Delay(kv.Key);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError(ex, "OPCUA Read Error:{0}", ex.Message);
                                await Task.Delay(kv.Key);
                            }

                        }
                    });
                });
            }, token);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addressArray"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task<DataResult<DataReceiveContract>> DirectReadAsync(IEnumerable<DataReceiveContractItem> addressArray, CancellationToken cancellationToken = default)
        {
            try
            {
                if (addressArray == null)
                    return DataResult<DataReceiveContract>.Failed(ResultType.ParameterError, $"参数为null,the addressArray parameter is null.");
                if (addressArray.Count() == 0)
                    return DataResult<DataReceiveContract>.Failed(ResultType.ParameterError, $"参数为空,the addressArray length is 0.");

                var nodes = addressArray.Select(t => NodeId.Parse(t.Address)).ToArray();
                var values = await Client.ReadNodesAsync(nodes);

                if (values == null)
                    return DataResult<DataReceiveContract>.Failed(ResultType.DeviceReadError, $"读取数据为空,read data is null.");
                if (values.Count != addressArray.Count())
                    return DataResult<DataReceiveContract>.Failed(ResultType.DeviceReadError, $"读取数据结果{values.Count}条，预期是{addressArray.Count()}条，摒弃不匹配的结果！");
                DataReceiveContract data = new DataReceiveContract()
                {
                    Id = iml6yu.Fingerprint.GetId(),
                    Key = Option.ProductLineName,
                    Timestamp = GetTimestamp(),
                    Datas = new List<DataReceiveContractItem>()
                };
                for (var i = 0; i < values.Count; i++)
                {
                    var item = addressArray.ElementAt(i);
                    if (!VerifyValue(values[i].Value, item.ValueType, out object v))
                        return DataResult<DataReceiveContract>.Failed(ResultType.DeviceReadError, $"读取失败，预期类型是{((TypeCode)item.ValueType).ToString()}，而实际读取到的类型是{item.Value.GetType().Name},类型不匹配！");
                    data.Datas.Add(new DataReceiveContractItem()
                    {
                        Address = item.Address,
                        Timestamp = data.Timestamp,
                        Value = v,
                        ValueType = item.ValueType
                    });
                }
                return DataResult<DataReceiveContract>.Success(data);
            }
            catch (Exception ex)
            {
                return DataResult<DataReceiveContract>.Failed(ResultType.Failed, ex.Message, ex);
            }
        }
    }
}
