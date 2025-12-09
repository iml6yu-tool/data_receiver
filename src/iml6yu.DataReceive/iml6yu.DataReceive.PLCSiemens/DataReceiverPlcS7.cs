using iml6yu.Data.Core;
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.PLCSiemens.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using S7.Net;
using S7.Net.Protocol.S7;
using S7.Net.Types;

namespace iml6yu.DataReceive.PLCSiemens
{
    public class DataReceiverPlcS7 : DataReceiver<S7.Net.Plc, DataReceiverPlcS7Option>
    {
        /// <summary>
        ///分组读取节点
        ///<list type="bullet">
        ///<item>Key:GroupName</item>
        ///<item>Value:配置节点</item>
        ///<list type="bullet">
        ///<item>key：读取间隔</item> 
        ///<item>value中的  string, DataItem 类型对应的Address(非FullAddress) -> DataItem</item>
        ///</list>
        ///</list>  
        /// </summary>
        private Dictionary<string, Dictionary<int, Dictionary<string, DataItem>>> readNodes;

        public override bool IsConnected => Client != null && Client.IsConnected;
        public DataReceiverPlcS7(DataReceiverPlcS7Option option, ILogger logger, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null) : base(option, logger, isAutoLoadNodeConfig, nodes)
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
                readNodes = ConvertConfigNodeToS7ReadConfig(ConfigNodes);
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                return MessageResult.Failed(ResultType.ParameterError, ex.Message, ex);
            }
        }

        public override async Task<MessageResult> ConnectAsync()
        {
            if (IsConnected)
                return MessageResult.Success();
            try
            {
                CreateClient(Option);
                await Client.OpenAsync();
                OnConnectionEvent(this.Option, new ConnectArgs(true));
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                OnConnectionEvent(this.Option, new ConnectArgs(false, ex.Message));
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

        public override async Task<MessageResult> DisConnectAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (IsConnected)
                        Client.Close();
                    Client = null;
                    return MessageResult.Success();
                }
                catch (Exception ex)
                {
                    return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
                }
            });
        }

        public override async Task<MessageResult> WriteAsync(DataWriteContract data)
        {
            if (!IsConnected)
                return MessageResult.Failed(ResultType.DeviceWriteError, $"the dirver({Option.OriginHost}) not connect");

            if (data == null || data.Datas == null || data.Datas.Count() == 0 || data.Datas.Any(t => string.IsNullOrEmpty(t.Address)))
                return MessageResult.Failed(ResultType.ParameterError, "the write data is null or empty or item any address is null");

            try
            {
                List<DataItem> writeNodes = new List<DataItem>();
                foreach (var t in data.Datas)
                {
                    var item = DataItem.FromAddressAndValue(t.Address, t.Value);
                    if (item == null)
                        return MessageResult.Failed(ResultType.ParameterError, $"the address({t.Address}) is error");
                    writeNodes.Add(item);
                }

                await Client.WriteAsync(writeNodes.ToArray());
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError($"write data error. {ex.Message}");
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

        public override async Task<MessageResult> WriteAsync(DataWriteContractItem data)
        {
            if (!IsConnected)
                return MessageResult.Failed(ResultType.DeviceWriteError, $"the dirver({Option.OriginHost}) not connect");

            try
            {
                var item = DataItem.FromAddressAndValue(data.Address, data.Value);
                if (item == null)
                    return MessageResult.Failed(ResultType.ParameterError, $"the address({data.Address}) is error");
                await Client.WriteAsync(item);
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError($"write data error. {ex.Message}");
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

        public override async Task<MessageResult> WriteAsync<T>(string address, T data)
        {
            if (!IsConnected)
                return MessageResult.Failed(ResultType.DeviceWriteError, $"the dirver({Option.OriginHost}) not connect");

            try
            {
                var item = DataItem.FromAddressAndValue(address, data);
                if (item == null)
                    return MessageResult.Failed(ResultType.ParameterError, $"the address({address}) is error");

                await Client.WriteAsync(item);
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError($"write data error. {ex.Message}");
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

        protected override Plc CreateClient(DataReceiverPlcS7Option option)
        {
            if (IsConnected)
                return Client;
            Client = new Plc(
                (S7.Net.CpuType)option.CpuType,
                option.OriginHost,
                (short)option.Rack,
               (short)option.Slot);
            return Client;
        }

        protected override Task WhileDoAsync(CancellationToken tokenSource)
        {
            return Task.Run(() =>
            {   //读取数据
                Parallel.ForEach(readNodes, readNode =>
                {
                    Parallel.ForEach(readNode.Value, async item =>
                    {
                        while (!tokenSource.IsCancellationRequested)
                        {
                            try
                            {
                                if (IsConnected)
                                    _ = Client.ReadMultipleVarsAsync(item.Value.Values.ToList()).ContinueWith(async t =>
                                    {
                                        if (t.IsFaulted)
                                        {
                                            Logger.LogError($"{Option.ReceiverName}({Option.OriginHost}:{Option.OriginPort}) read data error. {t.Exception?.Message}");
                                        }
                                        //获取当前时间戳
                                        var timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                                        var result = t.Result;
                                        Dictionary<string, ReceiverTempDataValue> tempDatas = new Dictionary<string, ReceiverTempDataValue>();
                                        foreach (var value in result)
                                        {
                                            var dataItem = item.Value.FirstOrDefault(t => t.Value == value);
                                            if (dataItem.Key != null)
                                            {
                                                tempDatas.Add(dataItem.Key, new ReceiverTempDataValue(value.Value, timestamp));
                                            }
                                        }
                                        await ReceiveDataToMessageChannelAsync(Option.ProductLineName, tempDatas);
                                    });
                                //结束了，就等待下一个间隔，直接退出吧
                                if (tokenSource.IsCancellationRequested)
                                    return;
                                await Task.Delay(item.Key == 0 ? 500 : item.Key);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"{Option.ReceiverName}({Option.OriginHost}:{Option.OriginPort}) read data error. {ex.Message}");
                                await Task.Delay(item.Key == 0 ? 500 : item.Key);
                            } 
                        } 
                    });

                });
            }, tokenSource);

        }

        /// <summary>
        /// 将NodeConfig转成符合S7读取的点位对象
        /// </summary>
        /// <param name="configNodes"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<int, Dictionary<string, DataItem>>> ConvertConfigNodeToS7ReadConfig(Dictionary<string, List<NodeItem>> configNodes)
        {
            Dictionary<string, Dictionary<int, Dictionary<string, DataItem>>> result = new Dictionary<string, Dictionary<int, Dictionary<string, DataItem>>>();
            foreach (var key in configNodes.Keys)
            {
                //groupby 一下configNodes,判断一下这个列表中的Address是否存在重复的，如果存在重复的，记录一个错误信息 
                var groupByAddress = configNodes[key].GroupBy(t => t.Address).Where(t => t.Count() > 1).Select(t => t.Key).ToList();
                if (groupByAddress.Count() > 1)
                {
                    Logger.LogError($"node config error. the address exist same item( {string.Join(";", groupByAddress)} ), the first item is usefull!");
                }

                var intervalToNodeItem = configNodes[key].GroupBy(t => t.Interval).ToDictionary(t => t.Key, t => t.ToList());

                //分组读取节点
                var s7DataItems = intervalToNodeItem.ToDictionary(t => t.Key, t => t.Value.Select(t =>
                {
                    var dataItem = DataItem.FromAddress(t.FullAddress);
                    return (t.FullAddress, t.Address, dataItem);
                }).Where(t => t.Item3 != null).ToDictionary(t => t.Item2, t => t.Item3));
                result.Add(key, s7DataItems);
            }
            return result;
        }

        //private DataItem? ConvertFullAddressToS7DataItem(string fullAddress, TypeCode valueType)
        //{
        //    if (string.IsNullOrEmpty(fullAddress))
        //    {
        //        Logger.LogError($"node config error. exist [FullAdress] is null or empty");
        //        return null;
        //    }
        //    try
        //    {
        //        var item = DataItem.FromAddress(fullAddress);
        //        var vartype = TypeCodeRefVarType(valueType);
        //        if (vartype != null)
        //            item.VarType = vartype.Value;

        //        return item;
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.LogError($"FullAddress ({fullAddress}) convert dataitem error,detail:\r\n{ex.Message}");
        //        return null;
        //    }


        //    //var array = fullAddress.ToUpper().Split(new char[] { '.', '。' }, StringSplitOptions.RemoveEmptyEntries);
        //    //if (array.Length < 2)
        //    //{
        //    //    Logger.LogError($"node({fullAddress}) config error.");
        //    //    return null;
        //    //}
        //    //if (!array[0].StartsWith("DB"))
        //    //{
        //    //    Logger.LogError($"only support DataBlock. The block of {fullAddress} is not support");
        //    //    return null;
        //    //}
        //    //if (!int.TryParse(array[0].Replace("DB", ""), out int db))
        //    //{
        //    //    Logger.LogError($"node({fullAddress}) config error.the fomat is [DBxx.xxx]");
        //    //    return null;
        //    //}
        //    ////两步验证数据类型，如果通过地址能获取到数据类型，则直接使用地址中的数据类型
        //    //var varType = AddressToVarType(addressSecond: array[1].Substring(2, 1));
        //    //varType = varType ?? TypeCodeRefVarType(valueType);
        //    //if (varType is null)
        //    //{
        //    //    Logger.LogError($"node({fullAddress}) config error.the VauleType is not support");
        //    //    return null;
        //    //}

        //    //return new DataItem()
        //    //{
        //    //    DataType = DataType.DataBlock,
        //    //    VarType = varType.Value,
        //    //    DB = db,
        //    //    StartByteAdr = 0,
        //    //    BitAdr = 0,
        //    //    Count = 1,
        //    //    Value = new object()
        //    //};
        //}

        ///// <summary>
        ///// Typecode 转 vartype
        ///// </summary>
        ///// <param name="typeCode"></param>
        ///// <returns></returns>
        //private VarType? TypeCodeRefVarType(TypeCode typeCode) => typeCode switch
        //{
        //    TypeCode.Boolean => VarType.Bit,
        //    TypeCode.Byte => VarType.Byte,
        //    TypeCode.UInt16 => VarType.Word,
        //    TypeCode.UInt32 => VarType.DWord,
        //    TypeCode.Int16 => VarType.Int,
        //    TypeCode.Int32 => VarType.DInt,
        //    TypeCode.Single => VarType.Real,
        //    TypeCode.Double => VarType.LReal,
        //    TypeCode.String => VarType.String,
        //    TypeCode.DateTime => VarType.DateTime,
        //    _ => null
        //};

        public override async Task<DataResult<DataReceiveContract>> DirectReadAsync(IEnumerable<DataReceiveContractItem> addressArray, CancellationToken cancellationToken = default)
        {
            try
            {
                if (addressArray == null)
                    return DataResult<DataReceiveContract>.Failed(ResultType.ParameterError, $"参数为null,the addressArray parameter is null.");
                if (addressArray.Count() == 0)
                    return DataResult<DataReceiveContract>.Failed(ResultType.ParameterError, $"参数为空,the addressArray length is 0.");

                var address = addressArray.Select(t => DataItem.FromAddress(t.Address)).ToList();
                var values = await Client.ReadMultipleVarsAsync(address, cancellationToken);
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

        public override async Task<MessageResult> WriteWithVerifyAsync(DataWriteContract data)
        {
            if (!IsConnected)
                return MessageResult.Failed(ResultType.DeviceWriteError, $"the dirver({Option.OriginHost}) not connect");

            if (data == null || data.Datas == null || data.Datas.Count() == 0 || data.Datas.Any(t => string.IsNullOrEmpty(t.Address)))
                return MessageResult.Failed(ResultType.ParameterError, "the write data is null or empty or item any address is null");
            if (data.Datas.Count(t => t.IsFlag) > 1)
                return MessageResult.Failed(ResultType.ParameterError, "the flag item is more than 1", null);
            try
            {
                List<DataItem> writeNodes = new List<DataItem>();
                foreach (var t in data.Datas)
                {
                    if (t.IsFlag) continue;
                    var item = DataItem.FromAddressAndValue(t.Address, t.Value);
                    if (item == null)
                        return MessageResult.Failed(ResultType.ParameterError, $"the address({t.Address}) is error");
                    writeNodes.Add(item);
                }

                await Client.WriteAsync(writeNodes.ToArray());
                var writeResult = await Client.ReadMultipleVarsAsync(writeNodes);
                foreach (var item in writeResult)
                {
                    var address = data.Datas.FirstOrDefault(t => DataItem.FromAddress(t.Address) == item);
                    if (address == null)
                        return MessageResult.Failed(ResultType.DeviceWriteError, $"数据写入失败");
                    if (!VerifyValueEqual(item.Value, address.Value, address.ValueType))
                        return MessageResult.Failed(ResultType.DeviceWriteError, $"数据{address.Address}写入失败,当前值是{item.Value},预期值是{address.Value}");
                }
                if (data.Datas.Any(t => t.IsFlag))
                {
                    var address = data.Datas.First(t => t.IsFlag);
                    var item = DataItem.FromAddressAndValue(address.Address, address.Value);
                    //写入数据
                    await Client.WriteAsync(item);
                    //读取当前写入的数据
                    var v = await Client.ReadAsync(address.Address);
                    //验证当前读取的值和预期写入的值是否相等
                    if (!VerifyValueEqual(v, address.Value, address.ValueType))
                        return MessageResult.Failed(ResultType.DeviceWriteError, $"数据{address.Address}写入失败,当前值是{v},预期值是{address.Value}");
                }
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError($"write data error. {ex.Message}");
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

    }
}
