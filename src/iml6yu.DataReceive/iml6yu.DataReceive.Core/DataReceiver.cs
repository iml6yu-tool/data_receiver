using iml6yu.Data.Core;
using iml6yu.Data.Core.Extensions;
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace iml6yu.DataReceive.Core
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TClient">客户端类型</typeparam>
    /// <typeparam name="TOption">客户端配置参数</typeparam>
    /// <typeparam name="TReceiveContent">接收文件内容</typeparam>
    public abstract class DataReceiver<TClient, TOption> : IDataReceiver<TClient, TOption>
        where TClient : class
        where TOption : DataReceiverOption
    {
        public TClient Client { get; set; }
        public TOption Option { get; set; }

        public ReceiverState State
        {
            get
            {
                if (DoTask == null)
                    return ReceiverState.Uninitialized;

                //客户端没有实例化，就是未实例化
                if (!IsConnected && Client == null)
                    return ReceiverState.Uninitialized;

                //如果没有连接，会自动重连，所以属于准备中
                if (!IsConnected && Client != null)
                    return ReceiverState.Reading;
                if (DoTask.Status == TaskStatus.Created)
                    return ReceiverState.Ready;
                if (DoTask.Status == TaskStatus.WaitingForActivation)
                    return ReceiverState.Ready;
                if (DoTask.Status == TaskStatus.WaitingToRun)
                    return ReceiverState.Ready;
                if (DoTask.Status == TaskStatus.Running)
                    return ReceiverState.Working;
                if (DoTask.Status == TaskStatus.WaitingForChildrenToComplete)
                    return ReceiverState.Working;
                if (DoTask.Status == TaskStatus.RanToCompletion)
                    return ReceiverState.Working;
                if (DoTask.Status == TaskStatus.Canceled)
                    return ReceiverState.Stoped;
                if (DoTask.Status == TaskStatus.Faulted)
                    return ReceiverState.Error;
                return ReceiverState.Stoped;
            }
        }

        public abstract bool IsConnected { get; }

        public string Name { get => Option.ReceiverName; }
        protected ILogger Logger { get; }
        protected Task DoTask;
        /// <summary>
        /// 配置的节点
        /// <list type="bullet">
        /// <item>Key:GroupName</item>
        /// <item>Value:配置的节点</item>
        /// </list>
        /// </summary>
        protected Dictionary<string, List<NodeItem>> ConfigNodes { get; set; }
        /// <summary>
        /// 数据缓存
        /// </summary>
        protected ConcurrentDictionary<string, CacheDataItem> CacheDataDic { get; set; }

        protected Dictionary<string, HashSet<string>> Subscribers { get; set; }

        /// <summary>
        /// 消息通道 Key：产线名称ProductLineName，Value：对应的是已给字典 Address --> Values
        /// <list type="bullet">
        /// <item>Key:address</item>
        /// <item>Value:value,timestamp</item>
        /// </list> 
        /// </summary> 
        protected Channel<KeyValuePair<string, Dictionary<string, ReceiverTempDataValue>>> MessageChannel { get; }

        /// <summary>
        /// 字符串数据转换器
        /// </summary>
        protected Func<string, Dictionary<string, ReceiverTempDataValue>> DataParse { get; set; }

        protected JsonSerializerOptions WriteDataSerializerOptions = new JsonSerializerOptions()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private static readonly int maxMessageCount = 5000;
        private static readonly int maxMessageWarningCount = (int)(maxMessageCount * 0.8);

        #region event
        /// <summary>
        /// 连接事件，当连接状态发生变化时触发
        /// </summary> 
        public event EventHandler<ConnectArgs> ConnectionEvent;
        /// <summary>
        /// 错误事件，当类库内发生错误时触发
        /// </summary>
        public event EventHandler<ExceptionArgs> ErrorEvent;

        /// <summary>
        /// 警告事件，当类库内发生警告时触发
        /// </summary>
        public event EventHandler<WarnArgs> WarnEvent;

        /// <summary>
        /// 当数据发生变动时触发
        /// </summary>
        public event EventHandler<DataReceiveContract> DataChangedEvent;
        /// <summary>
        /// 数据定时读取出发事件
        /// </summary>
        public event EventHandler<DataReceiveContract> DataIntervalEvent;
        /// <summary>
        /// 当订阅数据发生变动时触发
        /// </summary> 
        public event EventHandler<DataReceiveContract> DataSubscribeEvent;
        #endregion 

        /// <summary>
        /// 
        /// </summary>
        /// <param name="option">配置参数</param>
        /// <param name="logger">日志</param>
        /// <param name="nodes">配置的节点信息，当NodeFile有内容时，此参数可以是null</param>
        /// <param name="tokenSource"></param>
        /// <exception cref="ArgumentException"></exception>

        public DataReceiver(TOption option, ILogger logger, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null)
        {
            if (isAutoLoadNodeConfig && string.IsNullOrEmpty(option.NodeFile) && nodes == null)
                throw new ArgumentException($"当自动加载Node配置时，option.NodeFile和nodes参数不可以同时时空\r\nen: When AutoConnect, 'option.NodeFile' and 'nodes' are not both null");
            iml6yu.Fingerprint.UseFingerprint();
            Logger = logger;
            Option = option;
            Subscribers = new Dictionary<string, HashSet<string>>();
            MessageChannel = Channel.CreateBounded<KeyValuePair<string, Dictionary<string, ReceiverTempDataValue>>>(new BoundedChannelOptions(maxMessageCount)
            {
                AllowSynchronousContinuations = true,
                FullMode = BoundedChannelFullMode.DropOldest
            });

            Client = CreateClient(Option);

            if (isAutoLoadNodeConfig)
            {
                MessageResult loadResult;
                if (nodes != null && nodes.Count > 0)
                {
                    loadResult = LoadConfig(nodes);
                }
                else
                {
                    loadResult = LoadConfigAsync(Option.NodeFile).Result;
                }
                if (!loadResult.State)
                    Logger.LogError($"load config error!\r\n{loadResult.Message}");
            }

            if (Option.AutoConnect)
            {
                ConnectAsync().Wait();
            }
        }

        public DataReceiver(TOption option, ILogger logger, Func<string, Dictionary<string, ReceiverTempDataValue>> dataParse, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null) : this(option, logger, isAutoLoadNodeConfig, nodes)
        {
            SetDataParse(dataParse);
        }


        public void SetDataParse(Func<string, Dictionary<string, ReceiverTempDataValue>> dataParse)
        {
            DataParse = dataParse;
        }
        public async Task<MessageResult> LoadConfigAsync(string optionNodeFile)
        {
            try
            {
                var nodes = await optionNodeFile.ReadJsonContentAsync<List<NodeItem>>(Encoding.UTF8, CancellationToken.None);
                return LoadConfig(nodes);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex?.ToString());
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

        public virtual MessageResult LoadConfig(List<NodeItem> nodes)
        {
            if (nodes == null)
                return MessageResult.Failed(ResultType.ParameterError, nameof(nodes) + " parameter is null!");
            try
            {
                ConfigNodes = nodes.GroupBy(t => t.GroupName).ToDictionary(t => t.Key, t => t.ToList());
                CacheDataDic = new ConcurrentDictionary<string, CacheDataItem>(
                    nodes.GroupBy(t => t.Address)
                    .ToDictionary(t => t.Key, t => new CacheDataItem(DateTimeOffset.Now.ToUnixTimeMilliseconds(), new DataReceiveContractItem()
                    {
                        Address = t.First().Address,
                        ValueType = (int)t.First().ValueTypeCode
                    })));
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex?.ToString());

                #region 发生异常后给定空数据，让程序能够运行，但是无法接收数据
                ConfigNodes = ConfigNodes ?? new Dictionary<string, List<NodeItem>>();
                CacheDataDic = CacheDataDic ?? new ConcurrentDictionary<string, CacheDataItem>();
                #endregion

                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }

        }

        public async Task<CollectionResult<DataReceiveContractItem>> ReadAllAsync(CancellationToken cancellationToken = default)
        {
            if (CacheDataDic == null)
                return CollectionResult<DataReceiveContractItem>.Failed(ResultType.NotFind, "CacheDataDic is not init");

            return await Task.Run(() =>
            {
                var datas = CacheDataDic.Values.Where(t => t.Data.Value != null).Select(t => t.Data).ToList();
                if (!datas.Any())
                    CollectionResult<DataReceiveContractItem>.Failed(ResultType.NotFind, "not datas");

                return CollectionResult<DataReceiveContractItem>.Success(1, datas.Count, datas, datas.Count);
            });
        }

        public async Task<DataResult<DataReceiveContractItem>> ReadAsync(string address, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
              {
                  if (CacheDataDic == null)
                      return DataResult<DataReceiveContractItem>.Failed(ResultType.NotFind, "CacheDataDic is not init");

                  if (!CacheDataDic.ContainsKey(address))
                      return DataResult<DataReceiveContractItem>.Failed(ResultType.NotFind, $"{address} not exist");

                  if (CacheDataDic.TryGetValue(address, out CacheDataItem v) && v.Data.Value != null)
                  {
                      return DataResult<DataReceiveContractItem>.Success(v.Data);
                  }
                  return DataResult<DataReceiveContractItem>.Failed(ResultType.NotFind, $"{address} not data");
              });
        }

        public async Task<CollectionResult<DataReceiveContractItem>> ReadsAsync(IEnumerable<string> addressArray, CancellationToken cancellationToken = default)
        {
            if (CacheDataDic == null)
                return CollectionResult<DataReceiveContractItem>.Failed(ResultType.NotFind, "CacheDataDic is not init");

            return await Task.Run(() =>
            {
                var datas = CacheDataDic.Values.Where(t => t.Data.Value != null && addressArray.Any(a => a == t.Data.Address)).Select(t => t.Data).ToList();
                if (!datas.Any())
                    CollectionResult<DataReceiveContractItem>.Failed(ResultType.NotFind, "not datas");

                return CollectionResult<DataReceiveContractItem>.Success(1, datas.Count, datas, datas.Count);
            });
        }
        /// <summary>
        /// 实时读取数据
        /// </summary>
        /// <param name="addressArray"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public abstract Task<DataResult<DataReceiveContract>> DirectReadAsync(IEnumerable<DataReceiveContractItem> addressArray, CancellationToken cancellationToken = default);

        public Task StartWorkAsync(CancellationToken token)
        {
            return Task.Run(async () =>
            {
                try
                {
                    ReceiveDatas(token);
                    //避免重复调用的时候出现多个检测的task
                    if (DoTask != null && DoTask.Status == TaskStatus.Running)
                        DoTask.Dispose();
                    DoTask = WhileDoAsync(token);
                    while (!token.IsCancellationRequested)
                    {
                        if (!VerifyConnect())
                        {
                            Logger.LogWarning($"{Option.ReceiverName}({Option.OriginHost}:{Option.OriginPort})正在尝试重连...");
                            await ConnectAsync();
                        }
                        await Task.Delay(TimeSpan.FromSeconds(30));
                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex.Message, ex);
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    _ = StartWorkAsync(token);
                }
            });
        }

        public async Task StopWorkAsync()
        {
            DoTask.Dispose();
            await DisConnectAsync();
        }

        public MessageResult Subscribe(string key, IEnumerable<string> addressArray)
        {
            if (Subscribers.ContainsKey(key))
                return MessageResult.Failed(ResultType.RepetitiveData, "Subscribe Key is Repetitve Data", null);

            if (Subscribers.Count > 20)
                return MessageResult.Failed(ResultType.ParameterError, "The Subscribers of DataGatherer is too many,not allow gather 20", null);

            if (addressArray.Count() > 100)
                return MessageResult.Failed(ResultType.ParameterError, "The addressArray is too many,not allow gather 100", null);

            Subscribers.Add(key, new HashSet<string>(addressArray));
            return MessageResult.Success();
        }

        public MessageResult UnSubscrite(string key)
        {
            if (!Subscribers.ContainsKey(key))
                return MessageResult.Failed(ResultType.NotFind, "Subscribe Key not found", null);
            Subscribers.Remove(key);
            return MessageResult.Success();
        }
        /// <summary>
        /// 验证连接状态
        /// </summary>
        /// <returns></returns>
        public bool VerifyConnect()
        {
            if (Client == null)
                return false;
            return IsConnected;
        }
        /// <summary>
        /// 收到数据后进行的逻辑处理
        /// </summary>
        /// <param name="items"></param>
        protected virtual void ReceiveDatas(CancellationToken token)
        {
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await foreach (var data in MessageChannel.Reader.ReadAllAsync())
                    {
                        try
                        {
                            var changeDatas = UpdateCatch(data);

                            if (changeDatas.Datas.Count > 0)
                            {
                                //发布数据变更通知
                                DataChangedEvent?.Invoke(Option, changeDatas);

                                //订阅事件
                                if (Subscribers.Count > 0)
                                    Parallel.ForEach(Subscribers, item =>
                                    {
                                        if (item.Value.Count > 0)
                                        {
                                            var subDatas = changeDatas.Datas.Join(item.Value, d => d.Address, s => s, (d, s) => d).ToList();
                                            DataSubscribeEvent?.Invoke(item.Key, new DataReceiveContract(changeDatas.Timestamp) { Datas = subDatas });
                                        }
                                    });
                            }

                            if (DataIntervalEvent != null)
                            {
                                //_ = Task.Run(() =>
                                //  {
                                var intervalDatas = GetDataContract(data);
                                if (intervalDatas != null && intervalDatas.Datas.Count > 0)
                                    DataIntervalEvent?.Invoke(Option, intervalDatas);
                                //});
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex.ToString());
                        }
                    }
                }
            });
        }

        private DataReceiveContract GetDataContract(KeyValuePair<string, Dictionary<string, ReceiverTempDataValue>> datas)
        {
            if (datas.Value == null || datas.Value.Count == 0)
                return null;
            long now = GetTimestamp();
            DataReceiveContract data = new DataReceiveContract(now) { Key = datas.Key };

            foreach (var key in datas.Value.Keys)
            {
                if (!CacheDataDic.ContainsKey(key))
                    continue;
                if (CacheDataDic[key].Data.Value == null)
                    continue;
                if (!VerifyValue(datas.Value[key].Value, CacheDataDic[key].Data.ValueType, out object v))
                {
                    Logger.LogWarning($"{key}上报的数据类型是{datas.Value[key].Value.GetType()}与配置的类型{(TypeCode)CacheDataDic[key].Data.ValueType}不一致，数据已丢弃！");
                    continue;
                }
                data.Datas.Add(new DataReceiveContractItem()
                {
                    Address = key,
                    Value = v,
                    Timestamp = now,
                    ValueType = CacheDataDic[key].Data.ValueType
                });
            }
            return data;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static long GetTimestamp()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// 更新缓存
        /// </summary>
        /// <param name="datas"></param>
        /// <returns></returns>
        protected virtual DataReceiveContract UpdateCatch(KeyValuePair<string, Dictionary<string, ReceiverTempDataValue>> datas)
        {
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            DataReceiveContract data = new DataReceiveContract(now) { Key = datas.Key };
            foreach (var key in datas.Value.Keys)
            {
                if (!CacheDataDic.ContainsKey(key))
                    continue;

                if (datas.Value[key].Value == null)
                    continue;

                if (CacheDataDic[key].Data.Value != null && CacheDataDic[key].Data.Value.Equals(datas.Value[key].Value))
                    continue;

                if (!VerifyValue(datas.Value[key].Value, CacheDataDic[key].Data.ValueType, out object v))
                {
                    Logger.LogWarning($"{key}上报的数据类型是{datas.Value[key].Value.GetType()}与配置的类型{(TypeCode)CacheDataDic[key].Data.ValueType}不一致，数据已丢弃！");
                    continue;
                }

                CacheDataDic[key].Data.Value = v;
                CacheDataDic[key].Data.Timestamp = datas.Value[key].Timestamp;
                CacheDataDic[key].Timestamp = now;
#warning 这里有可能会发生当数据多线程时的变动
                data.Datas.Add(CacheDataDic[key].Data);
            }
            return data;
        }

        protected virtual bool VerifyValue(object value, int typeCode, out object targetValue)
        {
            if (typeCode == 3)//TypeCode.Boolean
            {
                if (value is bool b)
                {
                    targetValue = b;
                    return true;
                }
                else if (value is int || value is long || value is uint || value is ulong || value is byte || value is sbyte)
                {
                    targetValue = ((int)value == 1);
                    return ((int)value == 0) || ((int)value == 1);
                }
                else if (value is string s)
                {
                    targetValue = (s == "1") || (s == "true") || (s == "True") || (s == "TRUE");
                    return s == "1" || s == "0" || s == "false" || s == "true" || s == "False" || s == "True" || s == "FALSE" || s == "TRUE";
                }
                else
                {
                    targetValue = false;
                    return false;
                }
            }
            else if (typeCode == 9 || typeCode == 11) //TypeCode.Int32 TypeCode.Int64
            {
                if (value is int i)
                {
                    targetValue = i;
                    return true;
                }
                if (value is long l)
                {
                    targetValue = l;
                    return true;
                }
                targetValue = 0;
                return false;
            }
            else if (typeCode == 10 || typeCode == 12) //TypeCode.UInt32 TypeCode.UInt64
            {
                if (value is uint ui)
                {
                    targetValue = ui;
                    return true;
                }
                if (value is ulong ul)
                {
                    targetValue = ul;
                    return true;
                }
                targetValue = 0;
                return false;
            }
            else if (typeCode == 4 || typeCode == 16) //TypeCode.Char TypeCode.String
            {
                if (value is char c)
                {
                    targetValue = c;
                    return true;
                }
                if (value is string str)
                {
                    targetValue = str;
                    return true;
                }
                targetValue = string.Empty;
                return false;
            }
            else if (typeCode == 7 || typeCode == 8) //TypeCode.Char TypeCode.String
            {
                if (value is Int16 s)
                {
                    targetValue = s;
                    return true;
                }
                if (value is UInt16 us)
                {
                    targetValue = us;
                    return true;
                }
                targetValue = 0;
                return false;
            }
            else
            {
                try
                {
                    targetValue = Convert.ChangeType(value, (TypeCode)typeCode);
                    return true;
                }
                catch
                {
                    targetValue = null;
                    return false;
                }
            }
        }

        /// <summary>
        /// 判断是否读取的值是否等于预期值
        /// </summary>
        /// <param name="value"></param>
        /// <param name="expectation"></param>
        /// <param name="typeCode"></param>
        /// <returns></returns>
        protected virtual bool VerifyValueEqual(object value, object expectation, int typeCode)
        {
            // 1. 处理两个均为null的情况
            if (value == null || expectation == null)
            {
                return false;
            }
            if (!VerifyValue(value, typeCode, out object v))
                return false;
            if (!VerifyValue(expectation, typeCode, out object ev))
                return false;

            return object.Equals(v, ev);
        }
        /// <summary>
        /// 将消息加入线程channel中
        /// </summary>
        /// <param name="productLineName">产线名称</param>
        /// <param name="msg">字典：数据地址 --> 数据临时值（值和时间戳）</param>
        /// <returns></returns>
        protected virtual async Task ReceiveDataToMessageChannelAsync(string productLineName, Dictionary<string, ReceiverTempDataValue> msg)
        {
            if (msg == null) await Task.CompletedTask;
            if (MessageChannel.Reader.Count > maxMessageWarningCount)
            {
                Logger.LogWarning("zh-cn:读取设备点位的缓存数据已经预警，有可能出现数据丢失情况！en-us:The ThreadChannel Cache will full!");
            }
            await MessageChannel.Writer.WriteAsync(new KeyValuePair<string, Dictionary<string, ReceiverTempDataValue>>(productLineName, msg));
        }

        #region event on method
        /// <summary>
        /// 触发OnConnectionEvent
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected void OnConnectionEvent(object sender, ConnectArgs args)
        {
            ConnectionEvent?.Invoke(sender, args);
        }
        /// <summary>
        /// 触发SubDataChangeArgs
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected void OnDataSubscribeEvent(object sender, DataReceiveContract args)
        {
            DataSubscribeEvent?.Invoke(sender, args);
        }

        /// <summary>
        /// 触发 ExceptionEvent
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected void OnErrorEvent(object sender, ExceptionArgs args)
        {
            ErrorEvent?.Invoke(sender, args);
        }
        /// <summary>
        /// 触发WarnEvent
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected void OnWarnEvent(object sender, WarnArgs args)
        {
            WarnEvent?.Invoke(sender, args);
        }
        #endregion

        /// <summary>
        /// 循环开始读取数据工作
        /// </summary>
        protected abstract Task WhileDoAsync(CancellationToken tokenSource);

        /// <summary>
        /// 创建客户端
        /// </summary>
        /// <returns></returns>
        protected abstract TClient CreateClient(TOption option);

        public abstract Task<MessageResult> ConnectAsync();

        public abstract Task<MessageResult> DisConnectAsync();

        public virtual void Dispose()
        {
            DoTask.Dispose();
            Subscribers?.Clear();
            MessageChannel.Writer.Complete();
            CacheDataDic.Clear();
            ConfigNodes.Clear();
        }

        public abstract Task<MessageResult> WriteAsync(DataWriteContract data);
        public virtual async Task<MessageResult> WriteWithVerifyAsync(DataWriteContract data)
        {
            return await WriteAsync(data);
        }
        public abstract Task<MessageResult> WriteAsync(DataWriteContractItem data);

        public abstract Task<MessageResult> WriteAsync<T>(string address, T data);

    }
}
