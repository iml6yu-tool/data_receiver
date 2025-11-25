using iml6yu.Data.Core.Models;
using iml6yu.DataService.Core.Configs;
using iml6yu.DataService.Modbus.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using NModbus;
using System.Net;
using System.Threading.Tasks;

namespace iml6yu.DataService.Modbus
{
    public abstract class DataServiceModbus<TOption> : IDataServiceModbus where TOption : DataServiceModbusOption
    {
        protected ModbusFactory Factory { get; set; }
        public IModbusSlaveNetwork? Network { get; set; }

        protected TOption Option { get; }

        protected CancellationToken StopToken { get; set; }

        protected ILogger Logger { get; set; }
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="option">配置</param>
        public DataServiceModbus(TOption option, ILogger logger)
        {
            Logger = logger;
            Option = option;
            Factory = new ModbusFactory();
        }

        protected abstract IModbusSlaveNetwork CreateNetWork(TOption option);
        private void CreateServicer(TOption option)
        {
            Network = CreateNetWork(option);
            option.Storages.ForEach(opt =>
            {
                var slave = Factory.CreateSlave(byte.Parse(opt.Id));
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

        protected void AsyncHeartBeat()
        {
            foreach (var slave in Option.Storages)
            {
                if (slave.Heart == null || string.IsNullOrEmpty(slave.Heart.HeartAddress))
                    continue;

                string msg = string.Empty;
                if (slave.Heart.HeartAddress.VerifyWriteAddress(out byte slaveId, out ModbusReadWriteType writetype, out ushort startAddress, ref msg))
                {
                    if (writetype != ModbusReadWriteType.ReadInputRegisters && writetype != ModbusReadWriteType.HoldingRegisters)
                        throw new Exception("心跳地址配置错误，只支持ReadInputRegisters 或者是 HoldingRegisters地址！");
                    var slaveService = Network?.GetSlave(byte.Parse(slave.Id));
                    if (slaveService == null)
                        throw new Exception($"当前Slave(SlaveId:{slave.Id})服务不存在。");
                    switch (slave.Heart.HeartType)
                    {
                        case HeartType.OddAndEven:
                            Task.Run(async () =>
                            {
                                await WriteHeartDataAsync(slaveService, writetype, startAddress, 1, TimeSpan.FromSeconds(slave.Heart.HeartInterval), v => (ushort)((v + 1) % 2));
                            });
                            break;
                        case HeartType.Number:
                            Task.Run(async () =>
                            {
                                await WriteHeartDataAsync(slaveService, writetype, startAddress, 1, TimeSpan.FromSeconds(slave.Heart.HeartInterval), v => { if (v == ushort.MaxValue) v = 0; return ++v; });
                            });
                            break;
                        case HeartType.Time:
                            Task.Run(async () =>
                            {
                                await WriteHeartDataAsync(slaveService, writetype, startAddress, ushort.Parse(DateTime.Now.ToString("mmss")), TimeSpan.FromSeconds(slave.Heart.HeartInterval), v => { return ushort.Parse(DateTime.Now.ToString("mmss")); });
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
        }
        protected virtual async Task WriteHeartDataAsync(IModbusSlave? slave, ModbusReadWriteType writeType, ushort startAddress, ushort value, TimeSpan interval, Func<ushort, ushort> funcValue)
        {
            if (slave == null) return;
            if (slave.DataStore == null) return;

            if (writeType == ModbusReadWriteType.HoldingRegisters)
                slave.DataStore.HoldingRegisters.WritePoints(startAddress, [value]);

            if (writeType == ModbusReadWriteType.ReadInputRegisters)
                slave.DataStore.InputRegisters.WritePoints(startAddress, [value]);

            if (StopToken.IsCancellationRequested)
                return;
            await Task.Delay(interval);
            value = funcValue(value);
            await WriteHeartDataAsync(slave, writeType, startAddress, value, interval, funcValue);
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
            return await WriteAsync(data.Address, data.Value);
        }

        public async Task<MessageResult> WriteAsync<T>(string address, T data)
        {
            try
            {
                if (Network == null)
                    return MessageResult.Failed(ResultType.DeviceWriteError, $"数据服务还未初始化(DataService not init)");

                byte slaveId;
                ModbusReadWriteType writeType;
                ushort startAddress;
                string msg = string.Empty;
                if (!address.VerifyWriteAddress(out slaveId, out writeType, out startAddress, ref msg))
                    return MessageResult.Failed(ResultType.DeviceWriteError, msg);

                var slave = Network.GetSlave(slaveId);
                if (slave == null)
                    return MessageResult.Failed(ResultType.DeviceWriteError, $"地址{address}对应的SlaveID({slaveId})不存在！");
                if (data == null)
                    return MessageResult.Failed(ResultType.DeviceWriteError, $"当前需要写入的数值内容是null(current value is null)");

                switch (writeType)
                {
                    case ModbusReadWriteType.Coils:
                        slave.DataStore.CoilDiscretes.WritePoints(startAddress, data.ToModbusBooleanValues());
                        break;
                    case ModbusReadWriteType.Inputs:
                        slave.DataStore.CoilInputs.WritePoints(startAddress, data.ToModbusBooleanValues());
                        break;
                    case ModbusReadWriteType.HoldingRegisters:
                    case ModbusReadWriteType.HoldingRegisters2:
                    case ModbusReadWriteType.HoldingRegistersFloat:
                    case ModbusReadWriteType.HoldingRegisters4:
                    case ModbusReadWriteType.HoldingRegistersDouble:
                        slave.DataStore.HoldingRegisters.WritePoints(startAddress, data.ToModbusUShortValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.HoldingRegisters2ByteSwap:
                    case ModbusReadWriteType.HoldingRegistersFloatByteSwap:
                    case ModbusReadWriteType.HoldingRegisters4ByteSwap:
                    case ModbusReadWriteType.HoldingRegistersDoubleByteSwap:
                        slave.DataStore.HoldingRegisters.WritePoints(startAddress, data.ToModbusUShortBSValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.HoldingRegistersLittleEndian2:
                    case ModbusReadWriteType.HoldingRegistersFloatLittleEndian:
                    case ModbusReadWriteType.HoldingRegistersLittleEndian4:
                    case ModbusReadWriteType.HoldingRegistersDoubleLittleEndian:
                        slave.DataStore.HoldingRegisters.WritePoints(startAddress, data.ToModbusUShortLEValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.HoldingRegistersLittleEndian2ByteSwap:
                    case ModbusReadWriteType.HoldingRegistersFloatLittleEndianByteSwap:
                    case ModbusReadWriteType.HoldingRegistersLittleEndian4ByteSwap:
                    case ModbusReadWriteType.HoldingRegistersDoubleLittleEndianByteSwap:
                        slave.DataStore.HoldingRegisters.WritePoints(startAddress, data.ToModbusUShortLEBSValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.ReadInputRegisters:
                    case ModbusReadWriteType.ReadInputRegisters2:
                    case ModbusReadWriteType.ReadInputRegistersFloat:
                    case ModbusReadWriteType.ReadInputRegisters4:
                    case ModbusReadWriteType.ReadInputRegistersDouble:
                        slave.DataStore.InputRegisters.WritePoints(startAddress, data.ToModbusUShortValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.ReadInputRegisters2ByteSwap:
                    case ModbusReadWriteType.ReadInputRegistersFloatByteSwap:
                    case ModbusReadWriteType.ReadInputRegisters4ByteSwap:
                    case ModbusReadWriteType.ReadInputRegistersDoubleByteSwap:
                        slave.DataStore.InputRegisters.WritePoints(startAddress, data.ToModbusUShortBSValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.ReadInputRegistersLittleEndian2:
                    case ModbusReadWriteType.ReadInputRegistersFloatLittleEndian:
                    case ModbusReadWriteType.ReadInputRegistersLittleEndian4:
                    case ModbusReadWriteType.ReadInputRegistersDoubleLittleEndian:
                        slave.DataStore.InputRegisters.WritePoints(startAddress, data.ToModbusUShortLEValues(writeType.GetNumberOfPoint() * 2));
                        break;
                    case ModbusReadWriteType.ReadInputRegistersLittleEndian2ByteSwap:
                    case ModbusReadWriteType.ReadInputRegistersFloatLittleEndianByteSwap:
                    case ModbusReadWriteType.ReadInputRegistersLittleEndian4ByteSwap:
                    case ModbusReadWriteType.ReadInputRegistersDoubleLittleEndianByteSwap:
                        slave.DataStore.InputRegisters.WritePoints(startAddress, data.ToModbusUShortLEBSValues(writeType.GetNumberOfPoint() * 2));
                        break;
                }
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError($"写数据错误{ex.Message}");
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }
    }
}
