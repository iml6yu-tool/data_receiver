using iml6yu.Data.Core;
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.SerialCommunicate.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text;

namespace iml6yu.DataReceive.SerialCommunicate
{
    public abstract class DataReceiverSerialCommunicate : DataReceiver<SerialPort, DataReceiverSerialCommunicateOption>
    {
        protected readonly object _lockObj = new object();
        /// <summary>
        /// 接收到的数据缓冲区，使用ConcurrentQueue保证线程安全，串口数据接收事件和数据处理可能在不同的线程中执行
        /// </summary>
        protected ConcurrentQueue<byte> receiveBufferQueue = new ConcurrentQueue<byte>();
        /// <summary>
        /// 半双工串口通信时，发送数据后需要等待一段时间才能读取到数据，不同的波特率对应不同的等待时间，单位ms，默认值如下，可以根据实际情况调整
        /// </summary>
        protected Dictionary<int, int> BaudRateSleepTimeConfig { get; set; } = new Dictionary<int, int>()
        {
            { 9600, 100 },
            { 19200, 80 },
            { 38400, 60 },
            { 57600, 40 },
            { 115200, 20 }
        };
        /// <summary>
        /// 字符串数据转换器
        /// </summary>
        new protected Func<byte[], Dictionary<string, ReceiverTempDataValue>> DataParse { get; set; }
        public DataReceiverSerialCommunicate(DataReceiverSerialCommunicateOption option, ILogger logger, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null) : base(option, logger, isAutoLoadNodeConfig, nodes)
        {
            SetDataParse(DefaultDataParse);
        }


        public override bool IsConnected
        {
            get
            {
                return Client != null && Client.IsOpen;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataParse"></param>
        new public virtual void SetDataParse(Func<byte[], Dictionary<string, ReceiverTempDataValue>> dataParse)
        {
            DataParse = dataParse;
        }
        public override async Task<MessageResult> ConnectAsync()
        {
            try
            {
                if (IsConnected)
                    return MessageResult.Success();

                lock (_lockObj)
                {
                    if (Client == null)
                    {
                        Client = CreateClient(Option);
                    }

                    if (!Client.IsOpen)
                    {
                        Client.Open();
                    }
                }

                OnConnectionEvent(Option, new ConnectArgs()
                {
                    IsConntion = true,
                    Message = "success"
                });

                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                OnConnectionEvent(Option, new ConnectArgs()
                {
                    IsConntion = false,
                    Message = ex.Message
                });
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override async Task<MessageResult> DisConnectAsync()
        {
            return await Task.Run(() =>
              {
                  try
                  {
                      lock (_lockObj)
                      {
                          if (Client != null)
                          {
                              if (Client.IsOpen)
                                  Client.Close();
                              Client.Dispose();
                              Client = null;
                          }
                      }

                      OnConnectionEvent(Option, new ConnectArgs()
                      {
                          IsConntion = false,
                          Message = "Disconnected"
                      });

                      return MessageResult.Success();
                  }
                  catch (Exception ex)
                  {
                      Logger.LogError(ex.ToString());
                      return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
                  }
              });
        }

        protected override SerialPort CreateClient(DataReceiverSerialCommunicateOption option)
        {
            var serialPort = new SerialPort
            {
                PortName = option.PortName,
                BaudRate = option.BaudRate,
                DataBits = option.DataBits,
                Parity = option.Parity,
                StopBits = option.StopBits,
                Handshake = option.Handshake,
                ReadTimeout = option.ReadTimeout,
                WriteTimeout = option.WriteTimeout,
                ReadBufferSize = option.ReadBufferSize,
                WriteBufferSize = option.WriteBufferSize,
                RtsEnable = option.RtsEnable,
                DtrEnable = option.DtrEnable,
                Encoding = option.Encoding,
                ReceivedBytesThreshold = option.ReceivedBytesThreshold,

            };

            serialPort.DataReceived += SerialPort_DataReceived;
            serialPort.ErrorReceived += SerialPort_ErrorReceived;

            return serialPort;
        }

        protected override Task WhileDoAsync(CancellationToken token)
        {
            if (!IsConnected)
                ConnectAsync().Wait();

            return Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (!IsConnected)
                    {
                        var r = await ConnectAsync();
                        //如果连接失败，记录日志并触发断开事件 
                        if (!r.State)
                        {
                            Logger.LogError(r.Message);
                            OnConnectionEvent(Option, new ConnectArgs()
                            {
                                IsConntion = false,
                                Message = r.Message
                            });
                            //因为串口连接失败原因众多，而且重新open成功概率不高，所以直接断开连接，等待下一次WhileDo时重试连接即可
                            await DisConnectAsync();
                        }

                    }
                    await Task.Delay(2000);
                }
            });
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var serialPort = sender as SerialPort;
                if (serialPort == null || !serialPort.IsOpen)
                    return;

                int bytesToRead = serialPort.BytesToRead;
                if (bytesToRead <= 0)
                    return;

                byte[] buffer = new byte[bytesToRead];
                int bytesRead = serialPort.Read(buffer, 0, bytesToRead);

                if (bytesRead < 0)
                    return;

                if (receiveBufferQueue.Count == 0 //只有在接收缓冲区为空时才判断包头，避免每次接收数据都判断包头导致性能问题
                    && Option.DataPackageOption.Header != null && Option.DataPackageOption.Header.Length > 0 //如果配置了包头，则判断接收数据的前几个字节是否与包头一致，如果不一致则丢弃本次接收的数据，等待下一次接收时重新判断包头
                    && !StartHeader(buffer, Option.DataPackageOption.Header) //判断接收数据的前几个字节是否与包头一致，如果不一致则丢弃本次接收的数据，等待下一次接收时重新判断包头 
                    )
                    return;

                //CRC16校验
                if (Option.DataPackageOption.CheckType != DataCheckType.None)
                {
                    if (buffer.Length < Option.DataPackageOption.CheckLength)
                        return;

                    byte[] dataWithoutCrc = buffer.Take(buffer.Length - Option.DataPackageOption.CheckLength).ToArray();
                    byte[] checkBytes = buffer.Skip(buffer.Length - Option.DataPackageOption.CheckLength).Take(Option.DataPackageOption.CheckLength).ToArray();

                    if (!CheckHelper.CheckData(dataWithoutCrc, checkBytes, Option.DataPackageOption.CheckType))
                        return;
                    //将校验通过的数据加入到_receiveBuffer中，等待ProcessReceivedData方法处理
                    for (var i = 0; i < dataWithoutCrc.Length; i++)
                    {
                        receiveBufferQueue.Enqueue(dataWithoutCrc[i]);
                    }
                    ProcessReceivedData();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Serial port data received error");
                OnErrorEvent(this, new ExceptionArgs() { Ex = ex, Message = "Serial port data received error" });
            }
        }

        protected bool StartHeader(byte[] datas, byte[] header)
        {
            if (datas.Length < header.Length)
                return false;
            for (int i = 0; i < header.Length; i++)
            {
                //判断接收数据的前几个字节是否与配置的包头一致，如果不一致则丢弃本次接收的数据，等待下一次接收时重新判断包头
                if (datas[i] != header[i])
                    return false;
            }
            return true;
        }

        protected bool EndTail(byte[] datas, byte[] tail)
        {
            if (datas.Length < tail.Length)
                return false;
            for (int i = 0; i < tail.Length; i++)
            {
                //判断接收数据的后几个字节是否与配置的包尾一致，如果不一致则丢弃本次接收的数据，等待下一次接收时重新判断包头
                if (datas[datas.Length - tail.Length + i] != tail[i])
                    return false;
            }
            return true;
        }

        protected virtual void ProcessReceivedData()
        {
            try
            {
                if (receiveBufferQueue.Count < Option.DataPackageOption.Length)
                    return;

                if (Option.DataPackageOption.Header != null && Option.DataPackageOption.Header.Length > 0)
                    //取出包头失败后，不做任何处理，等待下一次接收数据时继续取出包头，直到取出包头成功为止，避免每次接收数据都判断包头导致性能问题
                    if (!DequeueHeader(receiveBufferQueue, Option.DataPackageOption.Header))
                        return;


                byte[] dataBytes = DequeueData(receiveBufferQueue, Option.DataPackageOption.Length - (Option.DataPackageOption.Header?.Length ?? 0) - (Option.DataPackageOption.Tail?.Length ?? 0), Option.DataPackageOption.Tail);
                if (dataBytes == null)
                    return;
                var result = DataParse?.Invoke(dataBytes);

                if (result != null && result.Count > 0)
                {
                    ReceiveDataToMessageChannelAsync(Option.ProductLineName, result).Wait();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Process received data error");
                OnErrorEvent(this, new ExceptionArgs() { Ex = ex, Message = "Process received data error" });
            }
        }

        /// <summary>
        /// 从接收缓冲区中出队数据，数据长度由Option.DataPackageOption.Length决定，如果配置了包尾，则出队后剩余的数据需要判断包尾是否与配置的包尾一致，如果不一致则丢弃本次接收的数据，等待下一次接收时重新判断包头，直到取出包头成功为止，避免每次接收数据都判断包头导致性能问题
        /// </summary>
        /// <param name="datas">缓冲队列</param>
        /// <param name="dataLength">数据长度（不包含header and tail）</param>
        /// <param name="tail">数据尾</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        protected virtual byte[] DequeueData(ConcurrentQueue<byte> datas, int dataLength, byte[] tail)
        {
            if (datas.Count < dataLength + (tail?.Length ?? 0))
                return null;
            List<byte> data = new List<byte>(dataLength);
            //如果没有Tail，直接出数据长度 
            if (tail == null || tail.Length == 0)
            {
                for (int i = 0; i < dataLength; i++)
                {
                    if (datas.TryDequeue(out byte b))
                        data.Add(b);
                    else
                        return null;
                }
                return data.ToArray();
            }
            else
            {
                var index = 0;
                //否则就Dequue数据直到遇到Tial,在判断数据长度与定义长度是否相同
                while (datas.Count > 0)
                {
                    if (datas.TryDequeue(out byte b))
                    {
                        data.Add(b);
                        //如果index+1大于等于tail的长度，就判断取出来的数据是否与配置的包尾一致
                        if (index >= tail.Length)
                            //判断取出来的数据是否与尾一致
                            if (EndTail(data.Take(index).ToArray(), tail))
                                if (data.Count == dataLength)
                                    return data.ToArray();
                                else
                                    return null;
                    }
                    else
                        return null;
                    index++;
                }
            }
            return null;
        }

        /// <summary>
        /// 从接收缓冲区中出队包头，包头长度由Option.DataPackageOption.Header.Length决定，出队后剩余的数据就是有效数据，等待ProcessReceivedData方法处理
        /// </summary>
        /// <param name="datas"></param>
        /// <param name="header"></param>
        /// <exception cref="NotImplementedException"></exception>
        protected virtual bool DequeueHeader(ConcurrentQueue<byte> datas, byte[] header)
        {
            //循环队列，取出包头，如果一直取不到，直到队列为空
            while (datas.Count >= header.Length)
            {
                for (int i = 0; i < header.Length; i++)
                {
                    if (datas.TryDequeue(out byte b))
                    {
                        //如果第一个字节就不匹配包头，则直接丢弃本次接收的数据，等待下一次接收时重新判断包头，直到取出包头成功为止，避免每次接收数据都判断包头导致性能问题
                        if (b != header[i])
                            return DequeueHeader(datas, header);
                    }
                    else
                    {
                        return DequeueHeader(datas, header);
                    }
                }
                //成功取出包头，返回true，剩余的数据就是有效数据，等待ProcessReceivedData方法处理
                return true;
            }
            return false;
        }

        protected virtual void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            Logger.LogError($"Serial port error: {e.EventType}");
            OnErrorEvent(this, new ExceptionArgs()
            {
                Message = $"Serial port error: {e.EventType}",
                Ex = new IOException($"Serial port error: {e.EventType}")
            });
        }

        protected abstract Dictionary<string, ReceiverTempDataValue> DefaultDataParse(byte[] data);

        public override void Dispose()
        {
            try
            {
                Client?.Close();
                Client?.Dispose();
            }
            finally
            {
                base.Dispose();
            }
        }

        public override async Task<MessageResult> WriteAsync(DataWriteContract data)
        {
            return await WriteAsync(data.Key, data);
        }

        public override async Task<MessageResult> WriteAsync(DataWriteContractItem data)
        {
            try
            {
                if (!(data.Value is string))
                    return MessageResult.Failed(ResultType.ParameterError, $"Only string type data is supported for serial port writing, but the actual type is {data.Value.GetType().Name}", null);

                if (Option.TransferType == SerialCommunicateTransferType.Simplex)
                    return MessageResult.Failed(ResultType.Failed, "Serial port is configured for simplex transfer, cannot write data", null);

                if (!IsConnected)
                    return MessageResult.Failed(ResultType.ServerNetworkError, $"Serial port({Option.PortName}) is not connected", null);

                string value = data.Value?.ToString() ?? string.Empty;
                byte[] bytes = Encoding.Default.GetBytes(value);

                if (Option.TransferType == SerialCommunicateTransferType.HalfDuplex)
                {
                    Client.RtsEnable = true;
                    //半双工模式，发送数据后需要等待一段时间才能读取到数据，不同的波特率对应不同的等待时间，单位ms，默认值如下，可以根据实际情况调整
                    int sleepTime = BaudRateSleepTimeConfig.ContainsKey(Option.BaudRate) ? BaudRateSleepTimeConfig[Option.BaudRate] : 100;
                    await Task.Delay(sleepTime);
                }

                lock (_lockObj)
                {
                    Client.Write(bytes, 0, bytes.Length);
                }


                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Serial port write error");
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
            finally
            {
                Client.RtsEnable = false;
            }
        }

        public override async Task<MessageResult> WriteAsync<T>(string address, T data)
        {
            try
            {
                if (!(data is string))
                    return MessageResult.Failed(ResultType.ParameterError, $"Only string type data is supported for serial port writing, but the actual type is {typeof(T).FullName}", null);

                if (Option.TransferType == SerialCommunicateTransferType.Simplex)
                    return MessageResult.Failed(ResultType.Failed, "Serial port is configured for simplex transfer, cannot write data", null);

                if (!IsConnected)
                    return MessageResult.Failed(ResultType.ServerNetworkError, $"Serial port({Option.PortName}) is not connected", null);

                string value = data?.ToString() ?? string.Empty;
                byte[] bytes = Encoding.Default.GetBytes(value);
                if (Option.TransferType == SerialCommunicateTransferType.HalfDuplex)
                {
                    Client.RtsEnable = true;
                    //半双工模式，发送数据后需要等待一段时间才能读取到数据，不同的波特率对应不同的等待时间，单位ms，默认值如下，可以根据实际情况调整
                    int sleepTime = BaudRateSleepTimeConfig.ContainsKey(Option.BaudRate) ? BaudRateSleepTimeConfig[Option.BaudRate] : 100;
                    await Task.Delay(sleepTime);
                }
                lock (_lockObj)
                {
                    Client.Write(bytes, 0, bytes.Length);
                }

                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Serial port write error");
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
            finally
            {
                Client.RtsEnable = false;
            }
        }
        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public virtual async Task<MessageResult> Write(string data)
        {
            try
            {
                if (Option.TransferType == SerialCommunicateTransferType.Simplex)
                    return MessageResult.Failed(ResultType.Failed, "Serial port is configured for simplex transfer, cannot write data", null);

                if (!IsConnected)
                    return MessageResult.Failed(ResultType.ServerNetworkError, $"Serial port({Option.PortName}) is not connected", null);

                string value = data?.ToString() ?? string.Empty;
                byte[] bytes = Encoding.Default.GetBytes(value);
                if (Option.TransferType == SerialCommunicateTransferType.HalfDuplex)
                {
                    Client.RtsEnable = true;
                    //半双工模式，发送数据后需要等待一段时间才能读取到数据，不同的波特率对应不同的等待时间，单位ms，默认值如下，可以根据实际情况调整
                    int sleepTime = BaudRateSleepTimeConfig.ContainsKey(Option.BaudRate) ? BaudRateSleepTimeConfig[Option.BaudRate] : 100;
                    await Task.Delay(sleepTime);
                }
                lock (_lockObj)
                {
                    Client.Write(bytes, 0, bytes.Length);
                }

                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Serial port write error");
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
            finally
            {
                Client.RtsEnable = false;
            }
        }
        public override async Task<DataResult<DataReceiveContract>> DirectReadAsync(IEnumerable<DataReceiveContractItem> addressArray, CancellationToken cancellationToken = default)
        {
//#error 有错误啊
//            if (addressArray == null)
//                return DataResult<DataReceiveContract>.Failed(ResultType.ParameterError, $"参数为null,the addressArray parameter is null.");
//            if (addressArray.Count() == 0)
//                return DataResult<DataReceiveContract>.Failed(ResultType.ParameterError, $"参数为空,the addressArray length is 0.");

//            var allDatas = await ReadAllAsync(cancellationToken);
//            if (!allDatas.State)
//                return DataResult<DataReceiveContract>.Failed(allDatas.Code, allDatas.Message, allDatas.Error);

//            DataReceiveContract data = new DataReceiveContract()
//            {
//                Id = iml6yu.Fingerprint.GetId(),
//                Key = Option.ProductLineName,
//                Timestamp = GetTimestamp(),
//                Datas = new List<DataReceiveContractItem>()
//            };

//            foreach (var address in addressArray)
//            {
//                var item = allDatas.Data?.FirstOrDefault(t => t.Address == address.Address);
//                if (item != null)
//                    data.Datas.Add(item);
//            }

            return DataResult<DataReceiveContract>.Failed(ResultType.NotImplemented, "DirectReadAsync is not supported for serial communication, please use ReadAllAsync to read all data and then filter the data you need", null);
        }
    }
}