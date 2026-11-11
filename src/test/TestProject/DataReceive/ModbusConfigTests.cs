using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.ModbusMaster;
using iml6yu.DataReceive.ModbusMaster.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging.Abstractions;
using NModbus;

namespace TestProject.DataReceive;

[TestClass]
public class ModbusConfigTests
{
    private static NodeItem Node(string fullAddress, int interval = 0, string group = "g")
        => new() { FullAddress = fullAddress, Address = fullAddress, GroupName = group, Interval = interval };

    private static ModbusReceiverFake CreateReceiver() => new();

    [TestMethod]
    public void LoadConfig_WithValidNodes_ShouldSucceed()
    {
        var receiver = CreateReceiver();

        var result = receiver.LoadConfig(new List<NodeItem> { Node("1.HoldingRegisters.100") });

        Assert.IsTrue(result.State);
        Assert.AreEqual(1, receiver.ReadNodes!.Count);
    }

    [TestMethod]
    public void LoadConfig_WithoutGroupName_ShouldFail()
    {
        var receiver = CreateReceiver();

        var result = receiver.LoadConfig(new List<NodeItem>
        {
            new() { FullAddress = "1.HoldingRegisters.100", Address = "a" }
        });

        Assert.IsFalse(result.State);
    }

    [TestMethod]
    public void ConvertToReadConfig_SingleNode_ShouldCreateOneItem()
    {
        var configs = CreateReceiver().BuildReadConfig(new List<NodeItem> { Node("1.HoldingRegisters.100") });

        Assert.AreEqual(1, configs.Count);
        Assert.AreEqual((byte)1, configs[0].SlaveId);
        Assert.AreEqual(1, configs[0].ReadItems.Count);

        var item = configs[0].ReadItems[0];
        Assert.AreEqual(ModbusReadWriteType.HoldingRegisters, item.ReadType);
        Assert.AreEqual((ushort)100, item.StartPoint);
        Assert.AreEqual((ushort)1, item.NumberOfPoint);
        Assert.AreEqual(1, item.ReadNodes.Count);
    }

    [TestMethod]
    public void ConvertToReadConfig_ContiguousNodes_ShouldMergeIntoOneItem()
    {
        var configs = CreateReceiver().BuildReadConfig(new List<NodeItem>
        {
            Node("1.HoldingRegisters.100"),
            Node("1.HoldingRegisters.101"),
            Node("1.HoldingRegisters.102")
        });

        var item = configs.Single().ReadItems.Single();
        Assert.AreEqual((ushort)100, item.StartPoint);
        Assert.AreEqual((ushort)3, item.NumberOfPoint);
        Assert.AreEqual(3, item.ReadNodes.Count);
    }

    [TestMethod]
    public void ConvertToReadConfig_WithGap_ShouldCreateTwoItems()
    {
        var configs = CreateReceiver().BuildReadConfig(new List<NodeItem>
        {
            Node("1.HoldingRegisters.100"),
            Node("1.HoldingRegisters.105")
        });

        var items = configs.Single().ReadItems;
        Assert.AreEqual(2, items.Count);
        Assert.AreEqual((ushort)100, items[0].StartPoint);
        Assert.AreEqual((ushort)105, items[1].StartPoint);
    }

    [TestMethod]
    public void ConvertToReadConfig_WithOverlap_ShouldSkipOverlappedNode()
    {
        var configs = CreateReceiver().BuildReadConfig(new List<NodeItem>
        {
            Node("1.HoldingRegisters2.100"),
            Node("1.HoldingRegisters2.101")
        });

        var item = configs.Single().ReadItems.Single();
        Assert.AreEqual((ushort)100, item.StartPoint);
        Assert.AreEqual((ushort)2, item.NumberOfPoint);
        Assert.AreEqual(1, item.ReadNodes.Count);
    }

    [TestMethod]
    public void ConvertToReadConfig_WithDifferentSlaveAndType_ShouldSplit()
    {
        var configs = CreateReceiver().BuildReadConfig(new List<NodeItem>
        {
            Node("1.HoldingRegisters.100"),
            Node("1.Coils.100"),
            Node("2.HoldingRegisters.100")
        });

        Assert.AreEqual(2, configs.Count);
        Assert.AreEqual(2, configs.Single(c => c.SlaveId == 1).ReadItems.Count);
        Assert.AreEqual(1, configs.Single(c => c.SlaveId == 2).ReadItems.Count);
    }

    [TestMethod]
    public void ConvertToReadConfig_ShouldSupportChineseDot()
    {
        var configs = CreateReceiver().BuildReadConfig(new List<NodeItem> { Node("1。HoldingRegisters。7") });

        var item = configs.Single().ReadItems.Single();
        Assert.AreEqual((ushort)7, item.StartPoint);
    }

    [TestMethod]
    public void ConvertToReadConfig_WithInvalidAddress_ShouldBeIgnored()
    {
        var configs = CreateReceiver().BuildReadConfig(new List<NodeItem>
        {
            Node("abc.HoldingRegisters.1"),
            Node("1.NotExistsType.1"),
            Node("1.HoldingRegisters.abc")
        });

        Assert.AreEqual(0, configs.Count);
    }

    [TestMethod]
    public void ConvertToReadConfig_WithDuplicateAddress_ShouldThrow()
    {
        var receiver = CreateReceiver();

        Assert.ThrowsException<ArgumentException>(() => receiver.BuildReadConfig(new List<NodeItem>
        {
            Node("1.HoldingRegisters.100"),
            Node("1.HoldingRegisters.100")
        }));
    }

    [TestMethod]
    public void ConvertConfigToReadConfig_ShouldGroupByInterval()
    {
        var grouped = CreateReceiver().BuildGroupedReadConfig(new Dictionary<string, List<NodeItem>>
        {
            ["g1"] = new()
            {
                Node("1.HoldingRegisters.100", 1000),
                Node("1.HoldingRegisters.101", 1000),
                Node("1.HoldingRegisters.200", 2000)
            }
        });

        Assert.AreEqual(1, grouped.Count);
        Assert.AreEqual(2, grouped["g1"].Count);
        Assert.AreEqual(2, grouped["g1"][1000].Single().ReadItems.Single().ReadNodes.Count);
        Assert.AreEqual(1, grouped["g1"][2000].Single().ReadItems.Count);
    }

    private sealed class ModbusReceiverFake : DataReceiverModbus<DataReceiverModbusOption>
    {
        public ModbusReceiverFake()
            : base(new DataReceiverModbusOption { ReceiverName = "modbus-fake" }, NullLogger.Instance)
        {
        }

        public Dictionary<string, Dictionary<int, List<ModbusReadConfig>>>? ReadNodes => readNodes;

        public override bool IsConnected => false;

        public List<ModbusReadConfig> BuildReadConfig(List<NodeItem> nodes) => ConvertToModbusReadConfig(nodes);

        public Dictionary<string, Dictionary<int, List<ModbusReadConfig>>> BuildGroupedReadConfig(Dictionary<string, List<NodeItem>> nodes)
            => ConvertConfigNodeToModbusReadConfig(nodes);

        protected override IModbusMaster CreateClient(DataReceiverModbusOption option) => null!;

        protected override Task WhileDoAsync(CancellationToken tokenSource) => Task.CompletedTask;

        public override Task<MessageResult> ConnectAsync() => Task.FromResult(MessageResult.Success());

        public override Task<MessageResult> DisConnectAsync() => Task.FromResult(MessageResult.Success());
    }
}
