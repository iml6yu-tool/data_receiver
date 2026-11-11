using iml6yu.Data.Core;
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TestProject.DataReceive;

[TestClass]
public class DataReceiverCoreTests
{
    private static FakeReceiver CreateReceiver(string name = "fake")
        => new(new DataReceiverOption { ReceiverName = name }, NullLogger.Instance);

    private static NodeItem Node(string address, TypeCode type = TypeCode.Int32, string group = "g")
        => new() { Address = address, FullAddress = address, ValueType = type, GroupName = group };

    [TestMethod]
    public void Constructor_ShouldInitializeDefaults()
    {
        var receiver = CreateReceiver("receiver-1");

        Assert.AreEqual("receiver-1", receiver.Name);
        Assert.AreEqual(ReceiverState.Uninitialized, receiver.State);
        Assert.IsFalse(receiver.IsConnected);
        Assert.IsFalse(receiver.VerifyConnect());
    }

    [TestMethod]
    public void LoadConfig_WithNull_ShouldFail()
    {
        var receiver = CreateReceiver();

        Assert.IsFalse(receiver.LoadConfig(null!).State);
    }

    [TestMethod]
    public void LoadConfig_ShouldBuildGroupsAndCache()
    {
        var receiver = CreateReceiver();

        var result = receiver.LoadConfig(new List<NodeItem>
        {
            Node("a"),
            Node("b", TypeCode.Double),
            Node("c", TypeCode.Boolean, "g2")
        });

        Assert.IsTrue(result.State);
        Assert.AreEqual(2, receiver.Group("g").Count);
        Assert.AreEqual(1, receiver.Group("g2").Count);
        Assert.AreEqual(3, receiver.CacheCount);
    }

    [TestMethod]
    public void LoadConfigAsync_WithMissingFile_ShouldFail()
    {
        var receiver = CreateReceiver();

        var result = receiver.LoadConfigAsync(Guid.NewGuid().ToString("N") + ".json").GetAwaiter().GetResult();

        Assert.IsFalse(result.State);
    }

    [TestMethod]
    public void ReadAsync_BeforeLoadConfig_ShouldFail()
    {
        var receiver = CreateReceiver();

        Assert.IsFalse(receiver.ReadAsync("a").GetAwaiter().GetResult().State);
    }

    [TestMethod]
    public void UpdateCatch_ShouldRefreshCacheAndReadBack()
    {
        var receiver = CreateReceiver();
        receiver.LoadConfig(new List<NodeItem> { Node("a") });

        var changed = receiver.ApplyUpdate("line", new Dictionary<string, ReceiverTempDataValue>
        {
            ["a"] = new(42, 123L)
        });

        Assert.AreEqual("line", changed.Key);
        Assert.AreEqual(1, changed.Datas.Count);
        Assert.AreEqual("a", changed.Datas[0].Address);
        Assert.AreEqual(42, changed.Datas[0].Value);
        Assert.AreEqual(123L, changed.Datas[0].Timestamp);

        var read = receiver.ReadAsync("a").GetAwaiter().GetResult();
        Assert.IsTrue(read.State);
        Assert.AreEqual(42, read.Data!.Value);
    }

    [TestMethod]
    public void UpdateCatch_ShouldIgnoreUnknownAddress()
    {
        var receiver = CreateReceiver();
        receiver.LoadConfig(new List<NodeItem> { Node("a") });

        var changed = receiver.ApplyUpdate("line", new Dictionary<string, ReceiverTempDataValue>
        {
            ["unknown"] = new(1, 1L)
        });

        Assert.AreEqual(0, changed.Datas.Count);
    }

    [TestMethod]
    public void UpdateCatch_ShouldIgnoreUnchangedValue()
    {
        var receiver = CreateReceiver();
        receiver.LoadConfig(new List<NodeItem> { Node("a") });

        receiver.ApplyUpdate("line", new Dictionary<string, ReceiverTempDataValue> { ["a"] = new(1, 1L) });
        var second = receiver.ApplyUpdate("line", new Dictionary<string, ReceiverTempDataValue> { ["a"] = new(1, 2L) });

        Assert.AreEqual(0, second.Datas.Count);
    }

    [TestMethod]
    public void UpdateCatch_ShouldIgnoreTypeMismatch()
    {
        var receiver = CreateReceiver();
        receiver.LoadConfig(new List<NodeItem> { Node("a", TypeCode.Int32) });

        var changed = receiver.ApplyUpdate("line", new Dictionary<string, ReceiverTempDataValue>
        {
            ["a"] = new("not-a-number", 1L)
        });

        Assert.AreEqual(0, changed.Datas.Count);
    }

    [TestMethod]
    public void Subscribe_ShouldSupportAddAndRemove()
    {
        var receiver = CreateReceiver();

        Assert.IsTrue(receiver.Subscribe("key1", new[] { "a", "b" }).State);
        Assert.IsFalse(receiver.Subscribe("key1", new[] { "c" }).State);
        Assert.IsTrue(receiver.UnSubscrite("key1").State);
        Assert.IsFalse(receiver.UnSubscrite("key1").State);
    }

    [TestMethod]
    public void Subscribe_WhenAddressesMoreThanOneHundred_ShouldFail()
    {
        var receiver = CreateReceiver();

        var addresses = Enumerable.Range(0, 101).Select(i => "a" + i);

        Assert.IsFalse(receiver.Subscribe("key", addresses).State);
    }

    [TestMethod]
    public void Subscribe_WhenSubscribersMoreThanTwenty_ShouldFail()
    {
        var receiver = CreateReceiver();

        for (var i = 0; i <= 21; i++)
        {
            if (!receiver.Subscribe("key" + i, new[] { "a" }).State)
            {
                Assert.AreEqual(21, i);
                return;
            }
        }

        Assert.Fail("允许的订阅数量超过预期");
    }

    [TestMethod]
    public void HeartBeatHealthy_WithoutConfig_ShouldBeUnknown()
    {
        var receiver = CreateReceiver();

        Assert.AreEqual(HeartBeatState.UnKnown, receiver.HeartBeatHealthy().GetAwaiter().GetResult());
    }

    [TestMethod]
    public void HeartBeatHealthy_WhenNotConnected_ShouldBeUnavailable()
    {
        var receiver = CreateReceiver();
        receiver.Option.HeartBeat = new DataReadItem { Address = "beat" };

        Assert.AreEqual(HeartBeatState.Unavailable, receiver.HeartBeatHealthy().GetAwaiter().GetResult());
    }

    [TestMethod]
    public void VerifyValue_Boolean_ShouldConvertSupportedInputs()
    {
        var receiver = CreateReceiver();

        Assert.IsTrue(receiver.TryVerifyValue(true, TypeCode.Boolean, out var boolTarget));
        Assert.AreEqual(true, boolTarget);

        Assert.IsTrue(receiver.TryVerifyValue(1, TypeCode.Boolean, out var intTarget));
        Assert.AreEqual(true, intTarget);

        Assert.IsFalse(receiver.TryVerifyValue(2, TypeCode.Boolean, out _));

        Assert.IsTrue(receiver.TryVerifyValue("TRUE", TypeCode.Boolean, out var stringTarget));
        Assert.AreEqual(true, stringTarget);

        Assert.IsFalse(receiver.TryVerifyValue("yes", TypeCode.Boolean, out _));
        Assert.IsFalse(receiver.TryVerifyValue(null!, TypeCode.Boolean, out _));
    }

    [TestMethod]
    public void VerifyValue_IntegerTypes_ShouldKeepValue()
    {
        var receiver = CreateReceiver();

        Assert.IsTrue(receiver.TryVerifyValue(12, TypeCode.Int32, out var intValue));
        Assert.AreEqual(12, intValue);

        Assert.IsTrue(receiver.TryVerifyValue(12L, TypeCode.Int64, out var longValue));
        Assert.AreEqual(12L, longValue);

        Assert.IsTrue(receiver.TryVerifyValue(12u, TypeCode.UInt32, out var uintValue));
        Assert.AreEqual(12u, uintValue);

        Assert.IsTrue(receiver.TryVerifyValue(12ul, TypeCode.UInt64, out var ulongValue));
        Assert.AreEqual(12ul, ulongValue);

        Assert.IsFalse(receiver.TryVerifyValue("12", TypeCode.Int32, out _));
    }

    [TestMethod]
    public void VerifyValue_FloatAndDouble_ShouldConvertBetweenThem()
    {
        var receiver = CreateReceiver();

        Assert.IsTrue(receiver.TryVerifyValue(1.5d, TypeCode.Single, out var single));
        Assert.AreEqual(1.5f, single);

        Assert.IsTrue(receiver.TryVerifyValue(1.5f, TypeCode.Double, out var dbl));
        Assert.AreEqual(1.5d, dbl);
    }

    [TestMethod]
    public void VerifyValue_DateTimeStringAndChar_ShouldWork()
    {
        var receiver = CreateReceiver();

        Assert.IsTrue(receiver.TryVerifyValue("2024-01-02", TypeCode.DateTime, out var date));
        Assert.AreEqual(new DateTime(2024, 1, 2), date);

        Assert.IsTrue(receiver.TryVerifyValue("abc", TypeCode.String, out var str));
        Assert.AreEqual("abc", str);

        Assert.IsTrue(receiver.TryVerifyValue('a', TypeCode.Char, out var ch));
        Assert.AreEqual('a', ch);

        Assert.IsTrue(receiver.TryVerifyValue((ushort)65535, TypeCode.UInt16, out var ushortValue));
        Assert.AreEqual((ushort)65535, ushortValue);

        Assert.IsTrue(receiver.TryVerifyValue("1.5", TypeCode.Decimal, out var dec));
        Assert.AreEqual(1.5m, dec);
    }

    [TestMethod]
    public void VerifyValueEqual_ShouldCompareUsingConfiguredType()
    {
        var receiver = CreateReceiver();

        Assert.IsTrue(receiver.TryVerifyValueEqual(1, "true", TypeCode.Boolean));
        Assert.IsTrue(receiver.TryVerifyValueEqual(5, 5, TypeCode.Int32));
        Assert.IsFalse(receiver.TryVerifyValueEqual(5, 5L, TypeCode.Int32));
        Assert.IsFalse(receiver.TryVerifyValueEqual(null!, 5, TypeCode.Int32));
    }

    private sealed class FakeReceiver : DataReceiver<object, DataReceiverOption>
    {
        public FakeReceiver(DataReceiverOption option, ILogger logger) : base(option, logger)
        {
        }

        public override bool IsConnected => false;

        public int CacheCount => CacheDataDic?.Count ?? 0;

        public IReadOnlyList<NodeItem> Group(string name) => ConfigNodes[name];

        public DataReceiveContract ApplyUpdate(string key, Dictionary<string, ReceiverTempDataValue> values)
            => UpdateCatch(new KeyValuePair<string, Dictionary<string, ReceiverTempDataValue>>(key, values));

        public bool TryVerifyValue(object value, TypeCode type, out object target) => VerifyValue(value, (int)type, out target);

        public bool TryVerifyValueEqual(object value, object expectation, TypeCode type) => VerifyValueEqual(value, expectation, (int)type);

        protected override object CreateClient(DataReceiverOption option) => new();

        protected override Task WhileDoAsync(CancellationToken tokenSource) => Task.CompletedTask;

        public override Task<MessageResult> ConnectAsync() => Task.FromResult(MessageResult.Success());

        public override Task<MessageResult> DisConnectAsync() => Task.FromResult(MessageResult.Success());

        public override Task<DataResult<DataReceiveContract>> DirectReadAsync(IEnumerable<DataReadItem> addressArray, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        protected override Task<MessageResult> WriteBatchAsync(DataWriteContract data) => throw new NotSupportedException();

        public override Task<MessageResult> WriteOneAsync(DataWriteContractItem data) => throw new NotSupportedException();

        public override Task<MessageResult> WriteAsync<T>(string address, T data) => throw new NotSupportedException();
    }
}
