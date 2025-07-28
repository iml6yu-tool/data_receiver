using iml6yu.Data.Core.Models;
using iml6yu.DataService.Modbus.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using NModbus;
using System.Net;

namespace iml6yu.DataService.Modbus
{
    public abstract class DataServiceModbus : IDataServiceModbus
    {
        protected ModbusFactory Factory { get; set; }
        public IModbusSlaveNetwork? Network { get; set; }

        protected DataServiceModbusOption Option { get; }

        protected CancellationToken StopToken { get; set; }

        protected ILogger Logger { get; set; }
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="option">配置</param>
        protected DataServiceModbus(DataServiceModbusOption option, ILogger logger)
        {
            Logger = logger;
            Option = option;
            Factory = new ModbusFactory();
        }

        protected abstract IModbusSlaveNetwork CreateNetWork(DataServiceModbusOption option);
        private void CreateServicer(DataServiceModbusOption option)
        {
            Network = CreateNetWork(option);
            option.Slaves.ForEach(opt =>
            {
                var slave = Factory.CreateSlave(opt.Id);
                if (opt.DefaultValues != null && opt.DefaultValues.Count > 0)
                {
                    DataWriteContract defaultValues = new DataWriteContract();
                    defaultValues.Datas = opt.DefaultValues.Select(t => new DataWriteContractItem()
                    {
                        Address = t.Address,
                        Value = t.DefaultValue
                    });
                    WriteAsync(defaultValues).Wait();
                }
                ;

                //添加从站
                Network.AddSlave(slave);
            });
        }

        public string Name { get => Option.ServiceName; }
        /// <summary>
        /// 运行状态
        /// </summary>
        public virtual bool IsRuning
        {
            get
            {
                return Network != null;
            }
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        public void StartServicer(CancellationToken token)
        {
            StopToken = token;
            CreateServicer(Option);
            Network?.ListenAsync();
            AsyncHeartBeat();
        }
        public virtual void StopServicer()
        {
            Network?.Dispose();
        }

        public void AsyncHeartBeat()
        {
            foreach (var slave in Option.Slaves)
            {
                if (slave.Heart == null || string.IsNullOrEmpty(slave.Heart.HeartAddress))
                    continue;

                string msg = string.Empty;
                if (slave.Heart.HeartAddress.VerifyWriteAddress(out byte slaveId, out ModbusReadWriteType writetype, out ushort startAddress, ref msg))
                {
                    if (writetype != ModbusReadWriteType.ReadInputRegisters && writetype != ModbusReadWriteType.HoldingRegisters)
                        throw new Exception("心跳地址配置错误，只支持ReadInputRegisters 或者是 HoldingRegisters地址！");
                    switch (slave.Heart.HeartType)
                    {
                        case HeartType.OddAndEven:
                            Task.Run(() =>
                            {
                                WriteHeartData(Network?.GetSlave(slave.Id), writetype, startAddress, 1, TimeSpan.FromSeconds(slave.Heart.HeartInterval), v => (ushort)((v + 1) % 2));
                            });
                            break;
                        case HeartType.Number:
                            Task.Run(() =>
                            {
                                WriteHeartData(Network?.GetSlave(slave.Id), writetype, startAddress, 1, TimeSpan.FromSeconds(slave.Heart.HeartInterval), v => { if (v == ushort.MaxValue) v = 0; return v++; });
                            });
                            break;
                        case HeartType.Time:
                            Task.Run(() =>
                            {
                                WriteHeartData(Network?.GetSlave(slave.Id), writetype, startAddress, ushort.Parse(DateTime.Now.ToString("mmss")), TimeSpan.FromSeconds(slave.Heart.HeartInterval), v => { return ushort.Parse(DateTime.Now.ToString("mmss")); });
                            });
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    throw new Exception("心跳地址配置错误，无法进行心跳处理！");
                }
            }
            ;
        }

        //public Dictionary<DataServiceModbusSlaveOption, Dictionary<ModbusSlaveStore, Array>> GetDatas()
        //{

        //    Dictionary<DataServiceModbusSlaveOption, Dictionary<ModbusSlaveStore, Array>> values = new Dictionary<DataServiceModbusSlaveOption, Dictionary<ModbusSlaveStore, Array>>();
        //    //if (Network == null)
        //    //    return values;
        //    //foreach (var item in Option.Slaves)
        //    //{
        //    //    values.Add(item, new Dictionary<ModbusSlaveStore, Array>());
        //    //    var slave = Network.GetSlave(item.Id);
        //    //    foreach (var store in item.Stores)
        //    //    {
        //    //        if (store.StoreType == ModbusReadWriteType.HoldingRegisters)
        //    //        {
        //    //            var datas = slave.DataStore.HoldingRegisters.ReadPoints(store.StartAddress, store.NumberOfPoint);
        //    //            values[item].Add(store, datas);
        //    //        }
        //    //        else if (store.StoreType == ModbusReadWriteType.ReadInputRegisters)
        //    //        {
        //    //            var datas = slave.DataStore.InputRegisters.ReadPoints(store.StartAddress, store.NumberOfPoint);
        //    //            values[item].Add(store, datas);
        //    //        }
        //    //        else if (store.StoreType == ModbusReadWriteType.Coils)
        //    //        {
        //    //            var datas = slave.DataStore.CoilDiscretes.ReadPoints(store.StartAddress, store.NumberOfPoint);
        //    //            values[item].Add(store, datas);
        //    //        }
        //    //        else if (store.StoreType == ModbusReadWriteType.Inputs)
        //    //        {
        //    //            var datas = slave.DataStore.CoilInputs.ReadPoints(store.StartAddress, store.NumberOfPoint);
        //    //            values[item].Add(store, datas);
        //    //        }

        //    //    }
        //    //}
        //    return values;
        //}
        protected virtual void WriteHeartData(IModbusSlave? slave, ModbusReadWriteType writeType, ushort startAddress, ushort value, TimeSpan interval, Func<ushort, ushort> funcValue)
        {
            if (slave == null) return;
            if (slave.DataStore == null) return;

            if (writeType == ModbusReadWriteType.HoldingRegisters)
                slave.DataStore.HoldingRegisters.WritePoints(startAddress, [value]);

            if (writeType == ModbusReadWriteType.ReadInputRegisters)
                slave.DataStore.InputRegisters.WritePoints(startAddress, [value]);

            if (StopToken.IsCancellationRequested)
                return;
            Task.Delay(interval).Wait();
            value = funcValue(value);
            WriteHeartData(slave, writeType, startAddress, value, interval, funcValue);
        }

        public async Task<List<MessageResult>> WriteAsync(DataWriteContract data)
        {
            var results = new List<MessageResult>();
            foreach (var item in data.Datas)
                results.Add(await WriteAsync(item));
            return results;
        }

        public async Task<MessageResult> WriteAsync(DataWriteContractItem data)
        {
            try
            {
                if (Network == null)
                    return MessageResult.Failed(ResultType.DeviceWriteError, $"数据服务还未初始化(DataService not init)");

                byte slaveId;
                ModbusReadWriteType writeType;
                ushort startAddress;
                string msg = string.Empty;
                if (!data.Address.VerifyWriteAddress(out slaveId, out writeType, out startAddress, ref msg))
                    return MessageResult.Failed(ResultType.DeviceWriteError, msg);

                var slave = Network.GetSlave(slaveId);
                if (slave == null)
                    return MessageResult.Failed(ResultType.DeviceWriteError, $"地址{data.Address}对应的SlaveID({slaveId})不存在！");
                if (data.Value == null)
                    return MessageResult.Failed(ResultType.DeviceWriteError, $"当前需要写入的数值内容是null(current value is null)");

                switch (writeType)
                {
                    case ModbusReadWriteType.Coils:
                        slave.DataStore.CoilDiscretes.WritePoints(startAddress, data.Value.ToModbusBooleanValues());
                        break;
                    case ModbusReadWriteType.Inputs:
                        slave.DataStore.CoilInputs.WritePoints(startAddress, data.Value.ToModbusBooleanValues());
                        break;
                    case ModbusReadWriteType.HoldingRegisters:
                    case ModbusReadWriteType.HoldingRegisters2:
                    case ModbusReadWriteType.HoldingRegisters4:
                        slave.DataStore.HoldingRegisters.WritePoints(startAddress, data.Value.ToModbusUShortValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.HoldingRegisters2ByteSwap:
                    case ModbusReadWriteType.HoldingRegisters4ByteSwap:
                        slave.DataStore.HoldingRegisters.WritePoints(startAddress, data.Value.ToModbusUShortBSValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.HoldingRegistersLittleEndian2:
                    case ModbusReadWriteType.HoldingRegistersLittleEndian4:
                        slave.DataStore.HoldingRegisters.WritePoints(startAddress, data.Value.ToModbusUShortLEValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.HoldingRegistersLittleEndian2ByteSwap:
                    case ModbusReadWriteType.HoldingRegistersLittleEndian4ByteSwap:
                        slave.DataStore.HoldingRegisters.WritePoints(startAddress, data.Value.ToModbusUShortLEBSValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.ReadInputRegisters:
                    case ModbusReadWriteType.ReadInputRegisters2:
                    case ModbusReadWriteType.ReadInputRegisters4:
                        slave.DataStore.InputRegisters.WritePoints(startAddress, data.Value.ToModbusUShortValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.ReadInputRegisters2ByteSwap:
                    case ModbusReadWriteType.ReadInputRegisters4ByteSwap:
                        slave.DataStore.InputRegisters.WritePoints(startAddress, data.Value.ToModbusUShortBSValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.ReadInputRegistersLittleEndian2:
                    case ModbusReadWriteType.ReadInputRegistersLittleEndian4:
                        slave.DataStore.InputRegisters.WritePoints(startAddress, data.Value.ToModbusUShortLEValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.ReadInputRegistersLittleEndian2ByteSwap:
                    case ModbusReadWriteType.ReadInputRegistersLittleEndian4ByteSwap:
                        slave.DataStore.InputRegisters.WritePoints(startAddress, data.Value.ToModbusUShortLEBSValues(writeType.GetNumberOfPoint() * 2));
                        break;
                }
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError($"写数据错误{0}", ex.Message);
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }


        }

        public async Task<MessageResult> WriteAsync<T>(string address, T data)
        {
            return await WriteAsync(new DataWriteContractItem() { Address = address, Value = data });
        }
    }
}
