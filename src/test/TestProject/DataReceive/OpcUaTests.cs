using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.OPCUA;
using iml6yu.DataReceive.OPCUA.Configs;
using Microsoft.Extensions.Logging.Abstractions;

namespace TestProject.DataReceive;

[TestClass]
public class OpcUaTests
{
    private static DataReceiverOPCUA CreateReceiver() => new(new DataReceiverOPCUAOption
    {
        ReceiverName = "opc-test",
        OriginHost = "opc.tcp://127.0.0.1:4840"
    }, NullLogger.Instance);

    [TestMethod]
    public void LoadConfig_WithNull_ShouldFail()
    {
        var receiver = CreateReceiver();

        Assert.IsFalse(receiver.LoadConfig(null!).State);
    }

    [TestMethod]
    public void LoadConfig_WithValidNodeIds_ShouldSucceed()
    {
        var receiver = CreateReceiver();

        var result = receiver.LoadConfig(new List<NodeItem>
        {
            new() { FullAddress = "ns=2;s=Demo.Tag1", Address = "tag1", GroupName = "g", Interval = 1000 },
            new() { FullAddress = "ns=2;s=Demo.Tag2", Address = "tag2", GroupName = "g", Interval = 1000 }
        });

        Assert.IsTrue(result.State);
    }

    [TestMethod]
    public void LoadConfig_WithInvalidNodeId_ShouldFail()
    {
        var receiver = CreateReceiver();

        var result = receiver.LoadConfig(new List<NodeItem>
        {
            new() { FullAddress = "invalid node id", Address = "tag", GroupName = "g" }
        });

        Assert.IsFalse(result.State);
    }

    [TestMethod]
    public void Option_ShouldHaveExpectedDefaults()
    {
        var option = new DataReceiverOPCUAOption();

        Assert.AreEqual(30000, option.ReConnectPeriod);
        Assert.AreEqual(3, option.ReConnectTimes);
        Assert.AreEqual(60000, option.ConnectTimeout);
        Assert.IsFalse(option.AutoConnect);
        Assert.IsNull(option.HeartBeat);
    }
}
