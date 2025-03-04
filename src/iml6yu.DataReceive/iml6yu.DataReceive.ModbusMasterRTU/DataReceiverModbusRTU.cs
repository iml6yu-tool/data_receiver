using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.ModbusMasterRTU.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using NModbus;
using NModbus.Serial;
using System.IO.Ports;
using System.Xml.Linq;

namespace iml6yu.DataReceive.ModbusMasterRTU
{
    public class DataReceiverModbusRTU : DataReceiver<NModbus.IModbusMaster, DataReceiverModbusOption, string>
    {
        private SerialPort serial;
        private Dictionary<int, List<ModbusReadConfig>> readNodes;
        public DataReceiverModbusRTU(DataReceiverModbusOption option, ILogger logger, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null, CancellationTokenSource tokenSource = null) : base(option, logger, isAutoLoadNodeConfig, nodes, tokenSource)
        {
            if (ConfigNodes == null)
                throw new ArgumentNullException(nameof(ConfigNodes));
            readNodes = ConvertConfigNodeToModbusReadConfig(ConfigNodes);
        }

        public override bool IsConnected => serial != null && Client != null && serial.IsOpen;

        public override async Task<MessageResult> ConnectAsync()
        {
            if (IsConnected)
                return MessageResult.Success();
            else
            {
                //await DisConnectAsync();
                if (!serial.IsOpen)
                    serial.Open();
                //CreateClient(Option);
                return MessageResult.Success();
            }
        }

        public override async Task<MessageResult> DisConnectAsync()
        {
            return await Task.Run(() =>
            {
                if (serial?.IsOpen ?? false)
                    serial?.Close();
                serial?.Dispose();
                Client.Dispose();

                return MessageResult.Success();
            });
        }

        protected override IModbusMaster CreateClient(DataReceiverModbusOption option)
        {
            serial = new SerialPort(option.ComPort, option.BaudRate, option.Parity, option.DataBits, option.StopBit);
            serial.Open();
            var factory = new ModbusFactory();
            Client = factory.CreateRtuMaster(serial);
            return Client;
        }

        protected override Task DoAsync(CancellationTokenSource tokenSource)
        {
            return Task.Run(() =>
            {
                //按照分组进行多线程并行
                Parallel.ForEach(readNodes, item =>
                {
                    while (!tokenSource.IsCancellationRequested)
                    {
                        Dictionary<string, ReceiverTempDataValue> tempDatas = new Dictionary<string, ReceiverTempDataValue>();
                        //按照modbus slaveaddress进行分组便利读取
                        Parallel.ForEach(item.Value, async readConfig =>
                        {
                            readConfig.ReadItems.ForEach(async node =>
                            {
                                if (node.ReadType == ModbusReadWriteType.Coils)
                                {
                                    var values = await Client.ReadCoilsAsync(readConfig.SlaveAddress, node.StartPoint, node.NumberOfPoint);
                                    if (values != null && values.Length > 0)
                                    {
                                        AddReceiveValue(readConfig, node, values, ref tempDatas);
                                    }
                                }
                                else if (node.ReadType == ModbusReadWriteType.Inputs)
                                {
                                    var values = await Client.ReadInputsAsync(readConfig.SlaveAddress, node.StartPoint, node.NumberOfPoint);
                                    if (values != null && values.Length > 0)
                                    {
                                        AddReceiveValue(readConfig, node, values, ref tempDatas);
                                    }
                                }
                                else if (node.ReadType == ModbusReadWriteType.HoldingRegisters)
                                {
                                    var values = await Client.ReadHoldingRegistersAsync(readConfig.SlaveAddress, node.StartPoint, node.NumberOfPoint);
                                    if (values != null && values.Length > 0)
                                    {
                                        AddReceiveValue(readConfig, node, values, ref tempDatas);
                                    }
                                }
                                else if (node.ReadType == ModbusReadWriteType.ReadInputRegisters)
                                {
                                    var values = await Client.ReadInputRegistersAsync(readConfig.SlaveAddress, node.StartPoint, node.NumberOfPoint);
                                    if (values != null && values.Length > 0)
                                    {
                                        AddReceiveValue(readConfig, node, values, ref tempDatas);
                                    }
                                }
                            });
                            await ReceiveDataToMessageChannelAsync(tempDatas);
                        });
                        Task.Delay(item.Key, tokenSource.Token).Wait();
                    }
                });

            }, tokenSource.Token);
        }

        private void AddReceiveValue<T>(ModbusReadConfig value, ModbusReadItem node, T[] values, ref Dictionary<string, ReceiverTempDataValue> tempDatas)
            where T : struct
        {
            if (values.Length != node.ReadNodes.Count)
            {
                Logger.LogWarning($"Read Modbus Data Error,Return Count not equals Read count. Read Node Count is {node.ReadNodes.Count},Return Value Count is {values.Length}.Read Node Details is \r\n{System.Text.Json.JsonSerializer.Serialize(node)}");
                return;
            }
            long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            for (var i = 0; i < node.ReadNodes.Count; i++)
            {
                if (tempDatas.ContainsKey(node.ReadNodes[i].Address))
                    tempDatas[node.ReadNodes[i].Address] = new ReceiverTempDataValue(values[i], timestamp);
                else
                    tempDatas.Add(node.ReadNodes[i].Address, new ReceiverTempDataValue(values[i], timestamp));
            }
        }

        private Dictionary<int, List<ModbusReadConfig>> ConvertConfigNodeToModbusReadConfig(List<NodeItem> nodes)
        {
            //按照读取间隔进行分组
            var nodeDic = nodes.Where(t => !string.IsNullOrEmpty(t.FullAddress)).GroupBy(t => t.Interval).ToDictionary(t => t.Key, t => t.ToList());
            //按照读取间隔拼装modbus读取配置信息
            var modbusReadConfigDic = new Dictionary<int, List<ModbusReadConfig>>();
            //遍历读取间隔
            foreach (var node in nodeDic)
            {
                //局部变量，用字典存储方便过滤
                //格式 dic<slaveaddress,dic<readtype,sortList<点位，实际配置地址>>>
                Dictionary<byte, Dictionary<ModbusReadWriteType, SortedList<ushort, NodeItem>>>
                    tempNode = new Dictionary<byte, Dictionary<ModbusReadWriteType, SortedList<ushort, NodeItem>>>();
                foreach (var item in node.Value)
                {
                    var array = item.FullAddress.Split(['.', '。'], StringSplitOptions.RemoveEmptyEntries);
                    if (array.Length != 3)
                    {
                        Logger.LogWarning($"node({item.FullAddress}) config error.");
                        continue;
                    }
                    if (!byte.TryParse(array[0], out byte slaveAddress))
                    {
                        Logger.LogWarning($"node({item.FullAddress}) slaveAddress config error.the first bit must byte type(0~255)");
                        continue;
                    }
                    if (!Enum.TryParse(array[1], out ModbusReadWriteType readType))
                    {
                        Logger.LogWarning($"node({item.FullAddress}) readType config error.the second bit must ModbusReadType type(Coils,Inputs,HoldingRegisters,ReadInputRegisters)");
                        continue;
                    }
                    if (!ushort.TryParse(array[2], out ushort bits))
                    {
                        Logger.LogWarning($"node({item.FullAddress}) bit config error.the third bit must ushort type");
                        continue;
                    }
                    if (!tempNode.ContainsKey(slaveAddress))
                        tempNode.Add(slaveAddress, new Dictionary<ModbusReadWriteType, SortedList<ushort, NodeItem>>());
                    if (!tempNode[slaveAddress].ContainsKey(readType))
                        tempNode[slaveAddress].Add(readType, new SortedList<ushort, NodeItem>());
                    tempNode[slaveAddress][readType].Add(bits, item);
                }

                modbusReadConfigDic.Add(node.Key, tempNode.Select(t =>
                {
                    var readconfig = new ModbusReadConfig()
                    {
                        SlaveAddress = t.Key,
                        ReadItems = new List<ModbusReadItem>(),
                    };
                    foreach (ModbusReadWriteType rt in t.Value.Keys)
                    {
                        readconfig.ReadItems.AddRange(GetModbusReadItems(rt, t.Value[rt]));
                    }
                    return readconfig;
                }).ToList());
            }

            return modbusReadConfigDic;
        }

        private List<ModbusReadItem> GetModbusReadItems(ModbusReadWriteType readType, SortedList<ushort, NodeItem> items)
        {
            List<ModbusReadItem> list = new List<ModbusReadItem>();
            if (items == null || items.Count == 0)
                return list;
            if (items.Count == 1)
            {
                list.Add(new ModbusReadItem() { ReadType = readType, StartPoint = items.Keys.First(), NumberOfPoint = 1, ReadNodes = items.Values.ToList() });
            }

            list.Add(new ModbusReadItem() { ReadType = readType, StartPoint = items.ElementAt(0).Key, NumberOfPoint = 1, ReadNodes = new List<NodeItem> { items.ElementAt(0).Value } });
            for (int i = 1; i < items.Keys.Count; i++)
            {
                // 检查当前元素是否与前一个元素连续
                if (items.Keys[i] == items.Keys[i - 1] + 1)
                {
                    list.Last().NumberOfPoint += 1;
                    list.Last().ReadNodes.Add(items.ElementAt(i).Value);
                }
                else
                {
                    list.Add(new ModbusReadItem() { ReadType = readType, StartPoint = items.ElementAt(i).Key, NumberOfPoint = 1, ReadNodes = new List<NodeItem> { items.ElementAt(i).Value } });
                }
            }
            return list;
        }

        public override async Task<MessageResult> WriteAsync(DataWriteContract data)
        {
            await Parallel.ForEachAsync(data.Datas, async (item, token) =>
            {
                await WriteAsync(item);
            });
            return MessageResult.Success();
        }

        public override async Task<MessageResult> WriteAsync(DataWriteContractItem data)
        {
            if (!VerifyWriteAddress(data.Address, out byte slaveAddress, out ModbusReadWriteType writeType, out ushort bits))
                return MessageResult.Failed(ResultType.ParameterError, $"write address({data.Address}) is error.the right format is “slaveAddress.ReadWriteType.Bit”", null);
            await WriteAsync(slaveAddress, writeType, bits, data.Value);
            return MessageResult.Success();
        }



        public override async Task<MessageResult> WriteAsync<T>(string address, T data)
        {
            if (!VerifyWriteAddress(address, out byte slaveAddress, out ModbusReadWriteType writeType, out ushort bits))
                return MessageResult.Failed(ResultType.ParameterError, $"write address({address}) is error.the right format is “slaveAddress.ReadWriteType.Bit”", null);
            if (data is bool || data is ushort)
            {
                await WriteAsync(slaveAddress, writeType, bits, data);
                return MessageResult.Success();
            }
            else
            {
                return MessageResult.Failed(ResultType.ParameterError, $"write data({data.GetType().Name})must be bool or ushort", null);
            }

        }
        private async Task WriteAsync(byte slaveAddress, ModbusReadWriteType writeType, ushort bits, object value)
        {
            if (writeType == ModbusReadWriteType.Coils)
                await Client.WriteSingleCoilAsync(slaveAddress, bits, (bool)value);
            else
                await Client.WriteSingleRegisterAsync(slaveAddress, bits, (ushort)value);
        }

        private bool VerifyWriteAddress(string address, out byte slaveAddress, out ModbusReadWriteType writeType, out ushort bits)
        {
            slaveAddress = default;
            writeType = default;
            bits = default;
            var array = address.Split(['.', '。'], StringSplitOptions.RemoveEmptyEntries);
            if (array.Length != 3)
            {
                Logger.LogWarning($"node({address}) config error.");
                return false;
            }
            if (!byte.TryParse(array[0], out slaveAddress))
            {
                Logger.LogWarning($"node({address}) slaveAddress config error.the first bit must byte type(0~255)");
                return false;
            }
            if (!Enum.TryParse(array[1], out writeType))
            {
                Logger.LogWarning($"node({address}) wirteType config error.the second bit must ModbusReadType type(Coils,Inputs,HoldingRegisters,ReadInputRegisters)");
                return false;
            }
            else
            {
                if (writeType != ModbusReadWriteType.Coils && writeType != ModbusReadWriteType.HoldingRegisters)
                {
                    Logger.LogWarning($"node({address}) wirteType config error.the second bit must ModbusReadType type(Coils,HoldingRegisters)");
                    return false;
                }
            }

            if (!ushort.TryParse(array[2], out bits))
            {
                Logger.LogWarning($"node({address}) bit config error.the third bit must ushort type");
                return false;
            }

            return true;
        }
        /// <summary>
        /// 读取的配置信息
        /// </summary>
        private class ModbusReadConfig
        {
            public byte SlaveAddress { get; set; }
            public List<ModbusReadItem> ReadItems { get; set; }
        }
        /// <summary>
        /// 配置项
        /// </summary>
        private class ModbusReadItem
        {
            /// <summary>
            /// 读取类型
            /// </summary>
            public ModbusReadWriteType ReadType { get; set; }
            /// <summary>
            /// 起始点位
            /// </summary>
            public ushort StartPoint { get; set; }
            /// <summary>
            /// 读取的点位个数
            /// </summary>
            public ushort NumberOfPoint { get; set; }

            /// <summary>
            /// 真正需要读取的配置节点
            /// </summary>
            public List<NodeItem> ReadNodes { get; set; }
        }

        public enum ModbusReadWriteType
        {
            /// <summary>
            /// 用于读取和控制远程设备的开关状态，通常用于控制继电器等开关设备, Reads from 1 to 2000 contiguous coils status.
            /// </summary>
            Coils,
            /// <summary>
            /// 用于读取远程设备的输入状态，通常用于读取传感器等输入设备的状态, Reads from 1 to 2000 contiguous discrete input status.
            /// </summary>
            Inputs,
            /// <summary>
            /// 用于存储和读取远程设备的数据，通常用于存储控制参数、设备状态等信息, Reads contiguous block of holding registers.
            /// </summary>
            HoldingRegisters,
            /// <summary>
            /// 用于存储远程设备的输入数据，通常用于存储传感器等输入设备的数据, Reads contiguous block of input registers.
            /// </summary>
            ReadInputRegisters

        }
    }
}
