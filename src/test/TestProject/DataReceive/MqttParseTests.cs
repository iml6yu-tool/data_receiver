using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.Mqtt;
using iml6yu.DataReceive.Mqtt.Configs;
using iml6yu.DataReceive.Mqtt.Default;
using iml6yu.DataReceive.Mqtt.InPlant;
using Microsoft.Extensions.Logging.Abstractions;

namespace TestProject.DataReceive;

[TestClass]
public class MqttParseTests
{
    private static DataReceiverMqttOption Option() => new() { ReceiverName = "mqtt-test" };

    [TestMethod]
    public void BaseReceiver_ShouldParseDataReceiveContractJson()
    {
        using var receiver = new TestableMqtt();
        receiver.LoadConfig(new List<NodeItem>());

        var result = receiver.Parse(
            "{\"Id\":1,\"Key\":\"line\",\"Timestamp\":1700000000000,\"Datas\":[{\"Address\":\"a\",\"ValueType\":9,\"Value\":42,\"Timestamp\":1700000000000}]}");

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result!.Count);
        Assert.AreEqual(42L, Convert.ToInt64(result["a"].Value));
        Assert.AreEqual(1700000000000L, result["a"].Timestamp);
    }

    [TestMethod]
    public void BaseReceiver_WithInvalidJson_ShouldReturnNull()
    {
        using var receiver = new TestableMqtt();
        receiver.LoadConfig(new List<NodeItem>());

        Assert.IsNull(receiver.Parse("not a json"));
        Assert.IsNull(receiver.Parse("{\"Datas\":[]}"));
    }

    [TestMethod]
    public void BaseReceiver_LoadConfig_ShouldMapFullAddressToGroup()
    {
        using var receiver = new TestableMqtt();

        var result = receiver.LoadConfig(new List<NodeItem>
        {
            new() { FullAddress = "fa", Address = "a", GroupName = "g" },
            new() { FullAddress = "fa", Address = "duplicate", GroupName = "g2" }
        });

        Assert.IsTrue(result.State);
        Assert.AreEqual(1, receiver.AddressMap.Count);
        Assert.AreEqual(("g", "a"), receiver.AddressMap["fa"]);
    }

    [TestMethod]
    public void DefaultReceiver_ShouldParseAndSkipDuplicateAndNullValues()
    {
        using var receiver = new TestableMqttDefault();
        receiver.LoadConfig(new List<NodeItem>());

        var result = receiver.Parse(
            "{\"Key\":\"line\",\"Timestamp\":1000,\"Datas\":[" +
            "{\"Address\":\"a\",\"ValueType\":9,\"Value\":1}," +
            "{\"Address\":\"a\",\"ValueType\":9,\"Value\":2}," +
            "{\"Address\":\"b\",\"ValueType\":9,\"Value\":null}]}");

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result!.Count);
        Assert.AreEqual(1L, Convert.ToInt64(result["a"].Value));
        Assert.AreEqual(1000L, result["a"].Timestamp);
    }

    [TestMethod]
    public void InPlantReceiver_ShouldParseRtValue()
    {
        using var receiver = new TestableMqttInPlant();
        receiver.LoadConfig(new List<NodeItem>());

        var result = receiver.Parse(
            "{\"RTValue\":[{\"Name\":\"a\",\"Value\":1,\"Timestamp\":1700000000}," +
            "{\"Name\":\"b\",\"Value\":null,\"Timestamp\":1700000000}]}");

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result!.Count);
        Assert.AreEqual(1L, Convert.ToInt64(result["a"].Value));
        Assert.AreEqual(1700000000000L, result["a"].Timestamp);
    }

    private sealed class TestableMqtt : DataReceiverMqtt
    {
        public TestableMqtt() : base(Option(), NullLogger.Instance)
        {
        }

        public Dictionary<string, (string, string)> AddressMap => AddressRefGroupName;

        public Dictionary<string, ReceiverTempDataValue>? Parse(string json) => JsonDataPrase(json);
    }

    private sealed class TestableMqttDefault : DataReceiverMqttDefault
    {
        public TestableMqttDefault() : base(Option(), NullLogger.Instance)
        {
        }

        public Dictionary<string, ReceiverTempDataValue>? Parse(string json) => JsonDataPrase(json);
    }

    private sealed class TestableMqttInPlant : DataReceiverMqttInPlant
    {
        public TestableMqttInPlant() : base(Option(), NullLogger.Instance)
        {
        }

        public Dictionary<string, ReceiverTempDataValue>? Parse(string json) => JsonDataPrase(json);
    }
}
