using iml6yu.Data.Core;
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.PLCSiemens.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using S7.Net;
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
                    var item = ConvertFullAddressToS7DataItem(t.Address, (TypeCode)t.ValueType);
                    if (item == null)
                        return MessageResult.Failed(ResultType.ParameterError, $"the address({t.Address}) is error");
                    item.Value = t.Value;
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
                var item = ConvertFullAddressToS7DataItem(data.Address, (TypeCode)data.ValueType);
                if (item == null)
                    return MessageResult.Failed(ResultType.ParameterError, $"the address({data.Address}) is error");
                item.Value = data.Value;

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
                var item = ConvertFullAddressToS7DataItem(address, Convert.GetTypeCode(data));
                if (item == null)
                    return MessageResult.Failed(ResultType.ParameterError, $"the address({address}) is error");
                item.Value = data;

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
                            await Task.Delay(item.Key == 0 ? 500 : item.Key, tokenSource);
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
                    var dataItem = ConvertFullAddressToS7DataItem(t.FullAddress, t.ValueTypeCode);
                    return (t.FullAddress, t.Address, dataItem);
                }).Where(t => t.Item3 != null).ToDictionary(t => t.Item2, t => t.Item3));
                result.Add(key, s7DataItems);
            }
            return result;
        }

        private DataItem? ConvertFullAddressToS7DataItem(string fullAddress, TypeCode valueType)
        {
            if (string.IsNullOrEmpty(fullAddress))
            {
                Logger.LogError($"node config error. exist [FullAdress] is null or empty");
                return null;
            }
            try
            {
                var item = DataItem.FromAddress(fullAddress);
                var vartype = TypeCodeRefVarType(valueType);
                if (vartype != null)
                    item.VarType = vartype.Value;

                return item;
            }
            catch (Exception ex)
            {
                Logger.LogError($"FullAddress ({fullAddress}) convert dataitem error,detail:\r\n{ex.Message}");
                return null;
            }


            //var array = fullAddress.ToUpper().Split(new char[] { '.', '。' }, StringSplitOptions.RemoveEmptyEntries);
            //if (array.Length < 2)
            //{
            //    Logger.LogError($"node({fullAddress}) config error.");
            //    return null;
            //}
            //if (!array[0].StartsWith("DB"))
            //{
            //    Logger.LogError($"only support DataBlock. The block of {fullAddress} is not support");
            //    return null;
            //}
            //if (!int.TryParse(array[0].Replace("DB", ""), out int db))
            //{
            //    Logger.LogError($"node({fullAddress}) config error.the fomat is [DBxx.xxx]");
            //    return null;
            //}
            ////两步验证数据类型，如果通过地址能获取到数据类型，则直接使用地址中的数据类型
            //var varType = AddressToVarType(addressSecond: array[1].Substring(2, 1));
            //varType = varType ?? TypeCodeRefVarType(valueType);
            //if (varType is null)
            //{
            //    Logger.LogError($"node({fullAddress}) config error.the VauleType is not support");
            //    return null;
            //}

            //return new DataItem()
            //{
            //    DataType = DataType.DataBlock,
            //    VarType = varType.Value,
            //    DB = db,
            //    StartByteAdr = 0,
            //    BitAdr = 0,
            //    Count = 1,
            //    Value = new object()
            //};
        }

        /// <summary>
        /// Typecode 转 vartype
        /// </summary>
        /// <param name="typeCode"></param>
        /// <returns></returns>
        private VarType? TypeCodeRefVarType(TypeCode typeCode) => typeCode switch
        {
            TypeCode.Boolean => VarType.Bit,
            TypeCode.Byte => VarType.Byte,
            TypeCode.UInt16 => VarType.Word,
            TypeCode.UInt32 => VarType.DWord,
            TypeCode.Int16 => VarType.Int,
            TypeCode.Int32 => VarType.DInt,
            TypeCode.Single => VarType.Real,
            TypeCode.Double => VarType.LReal,
            TypeCode.String => VarType.String,
            TypeCode.DateTime => VarType.DateTime,
            _ => null
        };

        ///// <summary>
        ///// 根据地址的第二个字符来判断数据类型
        ///// </summary>
        ///// <param name="addressSecond"></param>
        ///// <returns></returns>
        //private VarType? AddressToVarType(string addressSecond) => addressSecond switch
        //{
        //    "X" => VarType.Bit,
        //    "B" => VarType.Byte,
        //    "W" => VarType.Word,
        //    "D" => VarType.DWord,
        //    _ => null
        //};

    }
}
