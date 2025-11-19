using iml6yu.Data.Core;
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.ModbusMaster;
using iml6yu.DataReceive.ModbusMasterRTU.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using NModbus;
using NModbus.Serial;
using System.IO.Ports;
using System.Xml.Linq;

namespace iml6yu.DataReceive.ModbusMasterRTU
{
    public class DataReceiverModbusRTU : DataReceiverModbus<DataReceiverModbusRTUOption>
    {
        private SerialPort serial;
        public override bool IsConnected => serial != null && Client != null && serial.IsOpen;
        public DataReceiverModbusRTU(DataReceiverModbusRTUOption option, ILogger logger, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null) : base(option, logger, isAutoLoadNodeConfig, nodes)
        {

        }
        public override async Task<MessageResult> ConnectAsync()
        {
            if (IsConnected)
                return MessageResult.Success();
            else
            {
                try
                {
                    if (!serial.IsOpen)
                        serial.Open();
                    OnConnectionEvent(this.Option, new ConnectArgs(true));
                    return MessageResult.Success();

                }
                catch (Exception ex)
                {
                    OnConnectionEvent(this.Option, new ConnectArgs(false, ex.Message));
                    CreateClient(Option);
                    return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
                }
            }
        }

        public override async Task<MessageResult> DisConnectAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (serial?.IsOpen ?? false)
                        serial?.Close();
                    serial?.Dispose();
                    Client.Dispose();

                    return MessageResult.Success();
                }
                catch (Exception ex)
                {
                    return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
                }
            });
        }

        protected override IModbusMaster CreateClient(DataReceiverModbusRTUOption option)
        {
            serial = new SerialPort(option.ComPort, option.BaudRate, option.Parity, option.DataBits, option.StopBit);
            serial.Open();
            if (factory == null)
                factory = new ModbusFactory();
            Client = factory.CreateRtuMaster(serial);
            return Client;
        }

        protected override Task WhileDoAsync(CancellationToken token)
        {
            //此方法中的两次释放CPU是必要的释放，避免方法执行时间过长导致CPU占用。
            return Task.Run(async () =>
            {
                //取时间间隔最小且不为0的间隔（串口通信无法按照每一组进行间隔等待，因为串口不能多线程并发读取）
                var delayTime = TimeSpan.FromMilliseconds(readNodes.Values.SelectMany(t => t.Keys).Where(t => t > 0).Min(t => t));
                while (!token.IsCancellationRequested)
                {
                    if (VerifyConnect())
                    {
                        //按照分组循环
                        foreach (var readNode in readNodes)
                        {
                            //按照间隔进行循环
                            foreach (var item in readNode.Value)
                            {
                                Dictionary<string, ReceiverTempDataValue> tempDatas = new Dictionary<string, ReceiverTempDataValue>();
                                //按照slaveid循环
                                foreach (var readConfig in item.Value)
                                {
                                    //按照读取节点进行循环
                                    foreach (var node in readConfig.ReadItems)
                                    {
                                        tempDatas = ReadModbusNodeItem(readConfig, node, tempDatas);
                                    }
                                    await ReceiveDataToMessageChannelAsync(Option.ProductLineName, tempDatas);
                                }
                                //释放一次CPU
                                await Task.Delay(0);
                            }
                            //释放一次CPU
                            await Task.Delay(0);
                        }
                    }
                    //延迟等待
                    Task.Delay(delayTime, token).Wait(token);
                }
            }, token);
        }
    }
}
