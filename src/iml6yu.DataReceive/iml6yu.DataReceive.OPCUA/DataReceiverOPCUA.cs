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

        protected override async Task<MessageResult> WriteBatchAsync(DataWriteContract data)
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
                    var valueNodes = data.Datas.Where(d => !d.IsFlag).Select(d => (d.Address, d.Value)).ToList();
                    if (valueNodes == null || valueNodes.Count == 0)
                        return false;
                    var result = Client.WriteNodes(valueNodes.Select(t => t.Address).ToArray(), valueNodes.Select(t => t.Value).ToArray());
                    if (!result) return result;
                    //写入flag
                    var flagNodes = data.Datas.Where(d => d.IsFlag).Select(d => (d.Address, d.Value)).ToList();
                    if (flagNodes == null || flagNodes.Count == 0)
                        return result;
                    return Client.WriteNodes(flagNodes.Select(t => t.Address).ToArray(), flagNodes.Select(t => t.Value).ToArray());
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, ex.Message);
                    return false;
                }
            });
            if (r)
                return MessageResult.Success();
            return MessageResult.Failed(ResultType.Failed, $"{Option.ReceiverName}({Option.OriginHost})写入数据失败!");
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        protected override async Task<MessageResult> WriteStrictlyAsync(DataWriteContract data)
        {
            if (data == null || data.Datas == null || data.Datas.Count() == 0)
                return MessageResult.Failed(ResultType.ParameterError, "写入数据不能为空!");
            //判断设备连接
            if (!VerifyConnect())
                return MessageResult.Failed(ResultType.DeviceConnectionError, "设备未连接", null);
            //标志位不再进行数量限制
            //if (data.Datas.Count(t => t.IsFlag) > 1)
            //    return MessageResult.Failed(ResultType.ParameterError, "标志位最多只能有1个", null);

            try
            {
                var flagNodes = data.Datas.Where(t => t.IsFlag).ToList();
                var valueNodes = data.Datas.Where(t => !t.IsFlag  ).ToList();

                string[] address;
                object?[] values;
                //写入值
                if (valueNodes != null && valueNodes.Count == 0)
                {
                    address = valueNodes.Select(d => d.Address).ToArray();
                    values = valueNodes.Select(d => d.Value).ToArray();
                    var writeResult = Client.WriteNodes(address, values);
                    //写入成功后进行读取对比数值判定
                    if (!writeResult)
                        return MessageResult.Failed(ResultType.DeviceWriteError, $"{Option.ReceiverName}({Option.OriginHost})写入数据失败。数据信息：{string.Join(" , ", valueNodes.Select(t => $"{t.Address}:{t.Value}").ToList())}");
                    var writeValues = await Client.ReadNodesAsync(address.Select(t => new NodeId(t)).ToArray());
                    if (writeValues != null && writeValues.Count() == valueNodes.Count())
                    {
                        for (int i = 0; i < valueNodes.Count(); i++)
                        {
                            var expectation = valueNodes[i].Value;
                            //获取地址的值 
                            var targeValue = Convert.ChangeType(writeValues[i].Value, (TypeCode)valueNodes[i].ValueType);
                            if (targeValue != expectation)
                                return MessageResult.Failed(ResultType.DeviceWriteError, $"{Option.ReceiverName}({Option.OriginHost})写入地址{valueNodes[i].Address}失败，预期值{expectation}，实际值{targeValue}");
                        }
                    }
                }
                //写入标志位
                if (flagNodes != null && flagNodes.Count == 0)
                {
                    address = flagNodes.Select(d => d.Address).ToArray();
                    values = flagNodes.Select(d => d.Value).ToArray();
                    var writeResult = Client.WriteNodes(address, values);
                    //写入成功后进行读取对比数值判定
                    if (!writeResult)
                        return MessageResult.Failed(ResultType.DeviceWriteError, $"写入数据失败。数据信息：{string.Join(" , ", flagNodes.Select(t => $"{t.Address}:{t.Value}").ToList())}");
                    var writeValues = await Client.ReadNodesAsync(address.Select(t => new NodeId(t)).ToArray());
                    if (writeValues != null && writeValues.Count() == flagNodes.Count())
                    {
                        for (int i = 0; i < flagNodes.Count(); i++)
                        {
                            var expectation = flagNodes[i].Value;
                            //获取地址的值 
                            var targeValue = Convert.ChangeType(writeValues[i].Value, (TypeCode)flagNodes[i].ValueType);
                            if (targeValue != expectation)
                                return MessageResult.Failed(ResultType.DeviceWriteError, $"{Option.ReceiverName}({Option.OriginHost})写入地址{flagNodes[i].Address}失败，预期值{expectation}，实际值{targeValue}");
                        }
                    }
                }
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }
        public override async Task<MessageResult> WriteAsync(DataWriteContractItem data)
        {
            if (data == null)
                return MessageResult.Failed(ResultType.ParameterError, $"{Option.ReceiverName}({Option.OriginHost})写入数据不能为空!");
            return await WriteAsync(data.Address, data.Value);
        }

        public override async Task<MessageResult> WriteAsync<T>(string address, T data)
        {
            if (data == null)
                return MessageResult.Failed(ResultType.ParameterError, $"{Option.ReceiverName}({Option.OriginHost})写入数据不能为空!");

            //判断设备连接
            if (!VerifyConnect())
                return MessageResult.Failed(ResultType.Failed, $"{Option.ReceiverName}({Option.OriginHost})设备未连接", null);
            try
            {
                var r = await Client.WriteNodeAsync<T>(address, data);
                if (r)
                    return MessageResult.Success();
                return MessageResult.Failed(ResultType.Failed, $"{Option.ReceiverName}({Option.OriginHost})写入数据{address}({data})失败!");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return MessageResult.Failed(ResultType.Failed, $"{Option.ReceiverName}({Option.OriginHost})写入数据{address}({data})发生错误，{ex.Message}", ex);
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
                                        Logger.LogWarning($"{Option.ReceiverName}({Option.OriginHost})读取数据为空");
                                    else if (values.Count != kv.Value.Item1.Count)
                                        Logger.LogWarning($"{Option.ReceiverName}({Option.OriginHost})读取数据结果{values.Count}条，预期是{kv.Value.Item1.Count}条，摒弃不匹配的结果！");
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
                                Logger.LogError(ex, $"{Option.ReceiverName}({Option.OriginHost}) OPCUA  读取错误 Read Error:{ex.Message}");
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
        public override async Task<DataResult<DataReceiveContract>> DirectReadAsync(IEnumerable<DataReadItem> addressArray, CancellationToken cancellationToken = default)
        {
            try
            {
                if (addressArray == null)
                    return DataResult<DataReceiveContract>.Failed(ResultType.ParameterError, $"{Option.ReceiverName}({Option.OriginHost})参数为null,the addressArray parameter is null.");
                if (addressArray.Count() == 0)
                    return DataResult<DataReceiveContract>.Failed(ResultType.ParameterError, $"{Option.ReceiverName}({Option.OriginHost})参数为空,the addressArray length is 0.");

                var nodes = addressArray.Select(t => NodeId.Parse(t.Address)).ToArray();
                var values = await Client.ReadNodesAsync(nodes);

                if (values == null)
                    return DataResult<DataReceiveContract>.Failed(ResultType.DeviceReadError, $"{Option.ReceiverName}({Option.OriginHost})读取数据为空,read data is null.");
                if (values.Count != addressArray.Count())
                    return DataResult<DataReceiveContract>.Failed(ResultType.DeviceReadError, $"{Option.ReceiverName}({Option.OriginHost})读取数据结果{values.Count}条，预期是{addressArray.Count()}条，摒弃不匹配的结果！");
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
                        return DataResult<DataReceiveContract>.Failed(ResultType.DeviceReadError, $"{Option.ReceiverName}({Option.OriginHost})读取失败，预期类型是{((TypeCode)item.ValueType).ToString()}，而实际读取到的类型是{values[i].Value.GetType().Name},类型不匹配！");
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
