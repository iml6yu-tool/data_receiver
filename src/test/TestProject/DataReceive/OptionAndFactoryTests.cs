using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.ModbusMasterRTU.Configs;
using iml6yu.DataReceive.ModbusMasterTCP.Configs;
using iml6yu.DataReceive.Mqtt.Configs;
using iml6yu.DataReceive.Mqtt.Default;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Ports;

namespace TestProject.DataReceive;

[TestClass]
public class OptionAndFactoryTests
{
    [TestMethod]
    public void DataReceiverOption_ShouldHaveExpectedDefaults()
    {
        var option = new DataReceiverOption();

        Assert.AreEqual(30000, option.ReConnectPeriod);
        Assert.AreEqual(3, option.ReConnectTimes);
        Assert.AreEqual(60000, option.ConnectTimeout);
        Assert.IsFalse(option.AutoConnect);
        Assert.IsNull(option.HeartBeat);
        Assert.IsNull(option.RefLineNames);
        Assert.IsNull(option.NodeFile);
    }

    [TestMethod]
    public void ModbusTcpOption_ShouldBeDataReceiverOption()
    {
        var option = new DataReceiverModbusTCPOption();

        Assert.IsInstanceOfType<DataReceiverOption>(option);
        Assert.AreEqual(30000, option.ReConnectPeriod);
        Assert.IsFalse(option.AutoConnect);
    }

    [TestMethod]
    public void ModbusRtuOption_ShouldHaveExpectedSerialDefaults()
    {
        var option = new DataReceiverModbusRTUOption();

        Assert.AreEqual(9600, option.BaudRate);
        Assert.AreEqual(8, option.DataBits);
        Assert.AreEqual(Parity.None, option.Parity);
        Assert.AreEqual(StopBits.One, option.StopBit);
        Assert.IsNull(option.ComPort);
    }

    [TestMethod]
    public void MqttOption_ShouldHaveEmptyTopicsByDefault()
    {
        var option = new DataReceiverMqttOption();

        Assert.IsNull(option.DataInputTopics);
        Assert.AreEqual(30000, option.ReConnectPeriod);
    }

    [TestMethod]
    public void Factory_WithInvalidReceiverType_ShouldThrow()
    {
        var optionProvider = new DataReceiverFactory.ProviderConfig(
            string.Empty,
            typeof(DataReceiverMqttOption).AssemblyQualifiedName!);
        var receiverProvider = new DataReceiverFactory.ProviderConfig(
            string.Empty,
            "Not.Exists.Type");

        Assert.ThrowsException<ArgumentNullException>(
            () => DataReceiverFactory.Create(null!, NullLogger.Instance, optionProvider, receiverProvider, out _));
    }
}
