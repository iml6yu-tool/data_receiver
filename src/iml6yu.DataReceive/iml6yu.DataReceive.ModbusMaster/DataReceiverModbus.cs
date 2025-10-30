﻿using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.ModbusMaster.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using NModbus;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace iml6yu.DataReceive.ModbusMaster
{
    public abstract class DataReceiverModbus<TOption> : DataReceiver<NModbus.IModbusMaster, TOption>
        where TOption : DataReceiverModbusOption, new()
    {

        /// <summary>
        ///分组读取节点
        ///<list type="bullet">
        ///<item>Key:GroupName</item>
        ///<item>Value:配置节点</item>
        ///<list type="bullet">
        ///<item>key:读取间隔</item> 
        ///<item>value:ModbusReadConfig 类型</item>
        ///</list>
        ///</list>  
        /// </summary>
        protected Dictionary<string, Dictionary<int, List<ModbusReadConfig>>> readNodes;

        protected ModbusFactory factory;

        public DataReceiverModbus(TOption option, ILogger logger, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null) : base(option, logger, isAutoLoadNodeConfig, nodes)
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
                readNodes = ConvertConfigNodeToModbusReadConfig(ConfigNodes);
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                return MessageResult.Failed(ResultType.ParameterError, ex.Message, ex);
            }
        }

        protected override Task WhileDoAsync(CancellationToken token)
        {
            return Task.Run(() =>
            {
                //按照分组进行多线程并行
                Parallel.ForEach(readNodes, readNode =>
                {
                    Parallel.ForEach(readNode.Value, item =>
                    {
                        while (!token.IsCancellationRequested)
                        {
                            if (VerifyConnect())
                            {
                                //按照modbus slaveaddress进行分组便利读取
                                Parallel.ForEach(item.Value, async readConfig =>
                                {
                                    Dictionary<string, ReceiverTempDataValue> tempDatas = new Dictionary<string, ReceiverTempDataValue>();
                                    readConfig.ReadItems.ForEach(node =>
                                                                         {
                                                                             if (node.ReadType == ModbusReadWriteType.Coils)
                                                                             {
                                                                                 try
                                                                                 {
                                                                                     var values = Client.ReadCoils(readConfig.SlaveId, node.StartPoint, node.NumberOfPoint);
                                                                                     if (values != null && values.Length > 0)
                                                                                     {
                                                                                         AddReceiveValue(readConfig, node, values, ref tempDatas);
                                                                                     }
                                                                                 }
                                                                                 catch (Exception ex)
                                                                                 {

                                                                                     Logger.LogError("read coils error.\r\n{0}", ex.Message);
                                                                                 }
                                                                             }
                                                                             else if (node.ReadType == ModbusReadWriteType.Inputs)
                                                                             {
                                                                                 try
                                                                                 {
                                                                                     var values = Client.ReadInputs(readConfig.SlaveId, node.StartPoint, node.NumberOfPoint);
                                                                                     if (values != null && values.Length > 0)
                                                                                     {
                                                                                         AddReceiveValue(readConfig, node, values, ref tempDatas);
                                                                                     }
                                                                                 }
                                                                                 catch (Exception ex)
                                                                                 {

                                                                                     Logger.LogError("read inputs error.\r\n{0}", ex.Message);
                                                                                 }
                                                                             }
                                                                             else if (node.ReadType == ModbusReadWriteType.HoldingRegisters
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegisters2
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegisters2ByteSwap
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegistersFloat
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegistersFloatByteSwap
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegisters4
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegisters4ByteSwap
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegistersDouble
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegistersDoubleByteSwap
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegistersLittleEndian2
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegistersFloatLittleEndian
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegistersFloatLittleEndianByteSwap
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegistersLittleEndian2ByteSwap
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegistersLittleEndian4
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegistersLittleEndian4ByteSwap
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegistersDoubleLittleEndian
                                                                             || node.ReadType == ModbusReadWriteType.HoldingRegistersDoubleLittleEndianByteSwap)
                                                                             {
                                                                                 tempDatas = ReadHoldingRegisters(readConfig, node, tempDatas);
                                                                             }
                                                                             else if (node.ReadType == ModbusReadWriteType.ReadInputRegisters
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegisters2
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegisters2ByteSwap
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegistersFloat
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegistersFloatByteSwap
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegisters4
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegisters4ByteSwap
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegistersDouble
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegistersDoubleByteSwap
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegistersLittleEndian2
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegistersLittleEndian2ByteSwap
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegistersFloatLittleEndian
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegistersFloatLittleEndianByteSwap
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegistersLittleEndian4
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegistersLittleEndian4ByteSwap
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegistersDoubleLittleEndian
                                                                             || node.ReadType == ModbusReadWriteType.ReadInputRegistersDoubleLittleEndianByteSwap)
                                                                             {
                                                                                 tempDatas = ReadInputRegisters(readConfig, node, tempDatas);
                                                                             }
                                                                         });
                                    await ReceiveDataToMessageChannelAsync(Option.ProductLineName, tempDatas);
                                });
                            }
                            Task.Delay(item.Key == 0 ? 500 : item.Key, token).Wait(token);
                        }
                    });
                });
            }, token);
        }

        private Dictionary<string, ReceiverTempDataValue> ReadInputRegisters(ModbusReadConfig readConfig, ModbusReadItem node, Dictionary<string, ReceiverTempDataValue> tempDatas)
        {
            try
            {
                var values = Client.ReadInputRegisters(readConfig.SlaveId, node.StartPoint, node.NumberOfPoint);
                if (values != null && values.Length > 0)
                {
                    AddReceiveValue(readConfig, node, values, ref tempDatas);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("read input registers error.\r\n{0}", ex.Message);
            }

            return tempDatas;
        }

        private Dictionary<string, ReceiverTempDataValue> ReadHoldingRegisters(ModbusReadConfig readConfig, ModbusReadItem node, Dictionary<string, ReceiverTempDataValue> tempDatas)
        {
            try
            {
                var values = Client.ReadHoldingRegisters(readConfig.SlaveId, node.StartPoint, node.NumberOfPoint);
                if (values != null && values.Length > 0)
                {
                    AddReceiveValue(readConfig, node, values, ref tempDatas);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("read holding registers error.\r\n{0}", ex.Message);
            }

            return tempDatas;
        }

        private void AddReceiveValue<T>(ModbusReadConfig value, ModbusReadItem node, T[] values, ref Dictionary<string, ReceiverTempDataValue> tempDatas)
            where T : struct
        {
            if (values.Length != node.NumberOfPoint)
            {
                Logger.LogWarning($"Read Modbus Data Error,Return Count not equals Read count. Read Node Count is {node.ReadNodes.Count},Return Value Count is {values.Length}.Read Node Details is \r\n{System.Text.Json.JsonSerializer.Serialize(node)}");
                return;
            }
            long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            /**
             * 可以将这两个放在外面的原因是每次读取的类型长度都是一样的。不一样长度会分开读取，
             * 所以在循环外面声明可以减少内存的申请频率
             */
            var numberOfPoint = node.ReadType.GetNumberOfPoint();
            ushort[] arr = new ushort[numberOfPoint];
            for (var i = 0; i < node.ReadNodes.Count; i++)
            {
                //var numberOfPoint = GetNodeItemNumberOfPoint(node.ReadType);
                object currentValue;
                if (numberOfPoint == 1)
                    currentValue = values[i];
                else
                {
                    Array.Copy(values, i * numberOfPoint, arr, 0, numberOfPoint);
                    currentValue = GetNodeItemCurrentValue(node.ReadType, node.ReadNodes[i].ValueTypeCode, arr);
                }
                if (tempDatas.ContainsKey(node.ReadNodes[i].Address))
                    tempDatas[node.ReadNodes[i].Address] = new ReceiverTempDataValue(currentValue, timestamp);
                else
                    tempDatas.Add(node.ReadNodes[i].Address, new ReceiverTempDataValue(currentValue, timestamp));
            }
        }

        private object GetNodeItemCurrentValue(ModbusReadWriteType readType, TypeCode valueTypeCode, params ushort[] values)
        {

            if (readType == ModbusReadWriteType.HoldingRegistersLittleEndian2
                || readType == ModbusReadWriteType.ReadInputRegistersLittleEndian2
                || readType == ModbusReadWriteType.ReadInputRegistersFloatLittleEndian
                || readType == ModbusReadWriteType.HoldingRegistersFloatLittleEndian)
            {
                var bytes = values.SelectMany(t => BitConverter.GetBytes(t).Reverse()).ToArray();
                return Get32BitValue(valueTypeCode, bytes);
            }
            if (readType == ModbusReadWriteType.HoldingRegistersLittleEndian2ByteSwap
               || readType == ModbusReadWriteType.ReadInputRegistersLittleEndian2ByteSwap
               || readType == ModbusReadWriteType.ReadInputRegistersFloatLittleEndianByteSwap
               || readType == ModbusReadWriteType.HoldingRegistersFloatLittleEndianByteSwap)
            {
                //CD AB 32bit 小端byte交换
                var bytes = new byte[4]
                {
                    (byte)values[0],
                    (byte)(values[0]>>8),
                    (byte)values[1],
                    (byte)(values[1]>>8)
                };
                return Get32BitValue(valueTypeCode, bytes);
            }
            else if (readType == ModbusReadWriteType.HoldingRegistersLittleEndian4
               || readType == ModbusReadWriteType.ReadInputRegistersLittleEndian4
               || readType == ModbusReadWriteType.ReadInputRegistersDoubleLittleEndian
               || readType == ModbusReadWriteType.HoldingRegistersDoubleLittleEndian)
            {
                var bytes = values.SelectMany(t => BitConverter.GetBytes(t).Reverse()).ToArray();
                return Get64BitValue(valueTypeCode, bytes);
            }

            else if (readType == ModbusReadWriteType.HoldingRegistersLittleEndian4ByteSwap
              || readType == ModbusReadWriteType.ReadInputRegistersLittleEndian4ByteSwap
              || readType == ModbusReadWriteType.ReadInputRegistersDoubleLittleEndianByteSwap
              || readType == ModbusReadWriteType.HoldingRegistersDoubleLittleEndianByteSwap)
            {
                //GH EF  CD AB 64bit 小端byte交换 
                var bytes = new byte[8]
                {
                    (byte)values[0],
                    (byte)(values[0]>>8),
                    (byte)values[1],
                    (byte)(values[1]>>8),
                    (byte)values[2],
                    (byte)(values[2]>>8),
                    (byte)values[3],
                    (byte)(values[3]>>8)
                };
                return Get64BitValue(valueTypeCode, bytes);
            }

            else if (readType == ModbusReadWriteType.HoldingRegisters2
                || readType == ModbusReadWriteType.ReadInputRegisters2
                || readType == ModbusReadWriteType.ReadInputRegistersFloat
                || readType == ModbusReadWriteType.HoldingRegistersFloat)
            {
                var bytes = values.Reverse().SelectMany(t => BitConverter.GetBytes(t)).ToArray();
                return Get32BitValue(valueTypeCode, bytes);
            }

            else if (readType == ModbusReadWriteType.HoldingRegisters2ByteSwap
               || readType == ModbusReadWriteType.ReadInputRegisters2ByteSwap
               || readType == ModbusReadWriteType.ReadInputRegistersFloatByteSwap
               || readType == ModbusReadWriteType.HoldingRegistersFloatByteSwap)
            {
                //BA DC 32bit 大端byte交换 
                var bytes = new byte[4]
                {
                    (byte)(values[1]>>8),
                    (byte)values[1],
                    (byte)(values[0]>>8),
                    (byte)values[0]
                };
                return Get32BitValue(valueTypeCode, bytes);
            }
            else if (readType == ModbusReadWriteType.HoldingRegisters4
                || readType == ModbusReadWriteType.ReadInputRegisters4
                || readType == ModbusReadWriteType.ReadInputRegistersDouble
                || readType == ModbusReadWriteType.HoldingRegistersDouble)
            {
                var bytes = values.Reverse().SelectMany(t => BitConverter.GetBytes(t)).ToArray();
                return Get64BitValue(valueTypeCode, bytes);
            }
            else if (readType == ModbusReadWriteType.HoldingRegisters4ByteSwap
                || readType == ModbusReadWriteType.ReadInputRegisters4ByteSwap
                || readType == ModbusReadWriteType.ReadInputRegistersDoubleByteSwap
                || readType == ModbusReadWriteType.HoldingRegistersDoubleByteSwap)
            {
                //BA DC FE HG 64bit 大端byte交换
                var bytes = new byte[8]
                {
                    (byte)(values[3]>>8),
                    (byte)values[3],
                    (byte)(values[2]>>8),
                    (byte)values[2],
                    (byte)(values[1]>>8),
                    (byte)values[1],
                    (byte)(values[0]>>8),
                    (byte)values[0]
                };
                return Get64BitValue(valueTypeCode, bytes);
            }

            else
                return 0;
        }

        /// <summary>
        /// 通过数组获取当前32bit值
        /// </summary>
        /// <param name="valueTypeCode"></param>
        /// <param name="bytes"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object Get32BitValue(TypeCode valueTypeCode, byte[] bytes)
        {
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            if (valueTypeCode == TypeCode.Int32)
                return BitConverter.ToInt32(bytes, 0);
            else if (valueTypeCode == TypeCode.UInt32)
                return BitConverter.ToUInt32(bytes, 0);
            else if (valueTypeCode == TypeCode.Single)
                return BitConverter.ToSingle(bytes, 0);
            return 0;
        }
        /// <summary>
        /// 通过数组获取当前值
        /// </summary>
        /// <param name="valueTypeCode"></param>
        /// <param name="bytes"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object Get64BitValue(TypeCode valueTypeCode, byte[] bytes)
        {
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            if (valueTypeCode == TypeCode.Int64)
                return BitConverter.ToInt64(bytes, 0);
            else if (valueTypeCode == TypeCode.UInt64)
                return BitConverter.ToUInt64(bytes, 0);
            else if (valueTypeCode == TypeCode.Double)
                return BitConverter.ToDouble(bytes, 0);
            return 0;
        }

        protected Dictionary<string, Dictionary<int, List<ModbusReadConfig>>> ConvertConfigNodeToModbusReadConfig(Dictionary<string, List<NodeItem>> nodes)
        {
            Dictionary<string, Dictionary<int, List<ModbusReadConfig>>> result = new Dictionary<string, Dictionary<int, List<ModbusReadConfig>>>();
            foreach (var key in nodes.Keys)
            {
                //按照读取间隔进行分组
                var nodeDic = nodes[key].Where(t => !string.IsNullOrEmpty(t.FullAddress)).GroupBy(t => t.Interval).ToDictionary(t => t.Key, t => t.ToList());
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
                            Logger.LogError($"node({item.FullAddress}) config error,Must be 2 '.' spilt it");
                            continue;
                        }
                        if (!byte.TryParse(array[0], out byte slaveAddress))
                        {
                            Logger.LogError($"node({item.FullAddress}) slaveAddress config error.the first bit must byte type(0~255)");
                            continue;
                        }
                        if (!Enum.TryParse(array[1], out ModbusReadWriteType readType))
                        {
                            Logger.LogError($"node({item.FullAddress}) readType config error.the second bit not in MudbusReadType enmu");
                            continue;
                        }
                        if (!ushort.TryParse(array[2], out ushort bits))
                        {
                            Logger.LogError($"node({item.FullAddress}) bit config error.the third bit must ushort type");
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
                            SlaveId = t.Key,
                            ReadItems = new List<ModbusReadItem>(),
                        };
                        foreach (ModbusReadWriteType rt in t.Value.Keys)
                        {
                            readconfig.ReadItems.AddRange(GetModbusReadItems(rt, t.Value[rt]));
                        }
                        return readconfig;
                    }).ToList());
                }
                result.Add(key, modbusReadConfigDic);
            }
            return result;
        }

        private List<ModbusReadItem> GetModbusReadItems(ModbusReadWriteType readType, SortedList<ushort, NodeItem> items)
        {
            List<ModbusReadItem> list = new List<ModbusReadItem>();
            if (items == null || items.Count == 0)
                return list;
            var numberOfPoint = readType.GetNumberOfPoint();
            if (items.Count == 1)
            {
                list.Add(new ModbusReadItem() { ReadType = readType, StartPoint = items.Keys.First(), NumberOfPoint = numberOfPoint, ReadNodes = items.Values.ToList() });
                return list;
            }

            list.Add(new ModbusReadItem() { ReadType = readType, StartPoint = items.ElementAt(0).Key, NumberOfPoint = numberOfPoint, ReadNodes = new List<NodeItem> { items.ElementAt(0).Value } });
            for (int i = 1; i < items.Keys.Count; i++)
            {
                if (items.Keys[i] < items.Keys[i - 1] + numberOfPoint)
                {
                    Logger.LogError($"the address({items.Keys[i]}) of node is configuration error. The start address less than previous node(lenght:{numberOfPoint}) end address.");
                    continue;
                }
                // 检查当前元素是否与前一个元素连续
                if (items.Keys[i] == items.Keys[i - 1] + numberOfPoint)
                {
                    list.Last().NumberOfPoint += numberOfPoint;
                    list.Last().ReadNodes.Add(items.ElementAt(i).Value);
                }
                else
                {
                    list.Add(new ModbusReadItem() { ReadType = readType, StartPoint = items.ElementAt(i).Key, NumberOfPoint = numberOfPoint, ReadNodes = new List<NodeItem> { items.ElementAt(i).Value } });
                }
            }
            return list;
        }

        public override async Task<MessageResult> WriteAsync(DataWriteContract data)
        {
            if (!IsConnected)
                return MessageResult.Failed(ResultType.ServerDoApiError, $"the dirver({Option.OriginHost}) not connect");

            List<string> errorAddresses = new List<string>();
            foreach (var item in data.Datas)
            {
                if (!(await WriteAsync(item)).State)
                    errorAddresses.Add(item.Address);
            } 
            if (errorAddresses.Count > 0)
                return MessageResult.Failed(ResultType.DeviceWriteError, $"write some address error:{string.Join(",", errorAddresses)}");
            return MessageResult.Success();
        }

        public override async Task<MessageResult> WriteAsync(DataWriteContractItem data)
        {
            if (!IsConnected)
                return MessageResult.Failed(ResultType.ServerDoApiError, $"the dirver({Option.OriginHost}) not connect");

            if (!VerifyWriteAddress(data.Address, out byte slaveAddress, out ModbusReadWriteType writeType, out ushort bits))
                return MessageResult.Failed(ResultType.ParameterError, $"write address({data.Address}) is error.the right format is “slaveAddress.ReadWriteType.Bit”", null);
            return await WriteAsync(slaveAddress, writeType, bits, data.Value);

        }

        public override async Task<MessageResult> WriteAsync<T>(string address, T data)
        {
            if (!IsConnected)
                return MessageResult.Failed(ResultType.ServerDoApiError, $"the dirver({Option.OriginHost}) not connect");

            if (!VerifyWriteAddress(address, out byte slaveAddress, out ModbusReadWriteType writeType, out ushort bits))
                return MessageResult.Failed(ResultType.ParameterError, $"write address({address}) is error.the right format is “slaveAddress.ReadWriteType.Bit”", null);

            return await WriteAsync(slaveAddress, writeType, bits, data);
        }
        private async Task<MessageResult> WriteAsync(byte slaveAddress, ModbusReadWriteType writeType, ushort bits, object value)
        {
            try
            {
                if (writeType == ModbusReadWriteType.Coils)
                    await Client.WriteSingleCoilAsync(slaveAddress, bits, (bool)value);
                else
                    await Client.WriteSingleRegisterAsync(slaveAddress, bits, (ushort)value);
                switch (writeType)
                {
                    case ModbusReadWriteType.Coils:
                        if (value is bool b)
                            await Client.WriteSingleCoilAsync(slaveAddress, bits, b);
                        else
                            await Client.WriteMultipleCoilsAsync(slaveAddress, bits, value.ToModbusBooleanValues());
                        break;
                    case ModbusReadWriteType.Inputs:
                        return MessageResult.Failed(ResultType.ParameterError, "Inputs read-only, cannot write.", null);
                    case ModbusReadWriteType.HoldingRegisters:
                        if (value is ushort us)
                            await Client.WriteSingleRegisterAsync(slaveAddress, bits, us);
                        else
                            await Client.WriteMultipleRegistersAsync(slaveAddress, bits, value.ToModbusUShortValues());
                        break;
                    case ModbusReadWriteType.HoldingRegisters2:
                    case ModbusReadWriteType.HoldingRegistersFloat:
                    case ModbusReadWriteType.HoldingRegisters4:
                    case ModbusReadWriteType.HoldingRegistersDouble:
                        await Client.WriteMultipleRegistersAsync(slaveAddress, bits, value.ToModbusUShortValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.HoldingRegisters2ByteSwap:
                    case ModbusReadWriteType.HoldingRegistersFloatByteSwap:
                    case ModbusReadWriteType.HoldingRegisters4ByteSwap:
                    case ModbusReadWriteType.HoldingRegistersDoubleByteSwap:
                        await Client.WriteMultipleRegistersAsync(slaveAddress, bits, value.ToModbusUShortBSValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.HoldingRegistersLittleEndian2:
                    case ModbusReadWriteType.HoldingRegistersFloatLittleEndian:
                    case ModbusReadWriteType.HoldingRegistersLittleEndian4:
                    case ModbusReadWriteType.HoldingRegistersDoubleLittleEndian:
                        await Client.WriteMultipleRegistersAsync(slaveAddress, bits, value.ToModbusUShortLEValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.HoldingRegistersLittleEndian2ByteSwap:
                    case ModbusReadWriteType.HoldingRegistersFloatLittleEndianByteSwap:
                    case ModbusReadWriteType.HoldingRegistersLittleEndian4ByteSwap:
                    case ModbusReadWriteType.HoldingRegistersDoubleLittleEndianByteSwap:
                        await Client.WriteMultipleRegistersAsync(slaveAddress, bits, value.ToModbusUShortLEBSValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.ReadInputRegisters:
                    case ModbusReadWriteType.ReadInputRegisters2:
                    case ModbusReadWriteType.ReadInputRegistersFloat:
                    case ModbusReadWriteType.ReadInputRegisters4:
                    case ModbusReadWriteType.ReadInputRegistersDouble:
                    case ModbusReadWriteType.ReadInputRegisters2ByteSwap:
                    case ModbusReadWriteType.ReadInputRegistersFloatByteSwap:
                    case ModbusReadWriteType.ReadInputRegisters4ByteSwap:
                    case ModbusReadWriteType.ReadInputRegistersDoubleByteSwap:
                    case ModbusReadWriteType.ReadInputRegistersLittleEndian2:
                    case ModbusReadWriteType.ReadInputRegistersFloatLittleEndian:
                    case ModbusReadWriteType.ReadInputRegistersLittleEndian4:
                    case ModbusReadWriteType.ReadInputRegistersDoubleLittleEndian:
                    case ModbusReadWriteType.ReadInputRegistersLittleEndian2ByteSwap:
                    case ModbusReadWriteType.ReadInputRegistersFloatLittleEndianByteSwap:
                    case ModbusReadWriteType.ReadInputRegistersLittleEndian4ByteSwap:
                    case ModbusReadWriteType.ReadInputRegistersDoubleLittleEndianByteSwap:
                        return MessageResult.Failed(ResultType.ParameterError, "ReadInputRegisters read-only, cannot write.", null);
                }
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                return MessageResult.Failed(ResultType.DeviceWriteError, $"write modbus data error.\r\n{ex.Message}", null);
            }
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
    }
}
