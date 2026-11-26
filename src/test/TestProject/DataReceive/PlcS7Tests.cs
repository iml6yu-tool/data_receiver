using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.PLCSiemens;
using iml6yu.DataReceive.PLCSiemens.Configs;
using Microsoft.Extensions.Logging.Abstractions;
using S7.Net;
using S7.Net.Types;

namespace TestProject.DataReceive;

[TestClass]
public class PlcS7Tests
{
    private static DataReceiverPlcS7 CreateReceiver() => new(new DataReceiverPlcS7Option
    {
        ReceiverName = "plc-test",
        OriginHost = "127.0.0.1",
        OriginPort = 102
    }, NullLogger.Instance);

    private static NodeItem Node(string fullAddress, string group = "g", int count = 1)
        => new() { FullAddress = fullAddress, Address = fullAddress, GroupName = group, Count = count };

    [TestMethod]
    public void DataItem_FromAddress_ShouldParseDbWord()
    {
        var item = DataItem.FromAddress("DB1.DBW4");

        Assert.AreEqual(DataType.DataBlock, item.DataType);
        Assert.AreEqual(1, item.DB);
        Assert.AreEqual(4, item.StartByteAdr);
        Assert.AreEqual(VarType.Word, item.VarType);
        Assert.AreEqual((byte)0, item.BitAdr);
        Assert.AreEqual(1, item.Count);
    }

    [TestMethod]
    public void DataItem_FromAddress_ShouldParseBitAddress()
    {
        var item = DataItem.FromAddress("DB2.DBX0.3");

        Assert.AreEqual(DataType.DataBlock, item.DataType);
        Assert.AreEqual(2, item.DB);
        Assert.AreEqual(0, item.StartByteAdr);
        Assert.AreEqual(VarType.Bit, item.VarType);
        Assert.AreEqual((byte)3, item.BitAdr);
    }

    [TestMethod]
    public void DataItem_FromAddress_ShouldParseOtherTypes()
    {
        Assert.AreEqual(VarType.DWord, DataItem.FromAddress("DB1.DBD8").VarType);
        Assert.AreEqual(VarType.Byte, DataItem.FromAddress("DB1.DBB10").VarType);
        Assert.AreEqual(VarType.Real, DataItem.FromAddress("DB1.SRL12").VarType);
        Assert.AreEqual(VarType.DateTimeLong, DataItem.FromAddress("DB1.DTL16").VarType);

        var input = DataItem.FromAddress("IB5");
        Assert.AreEqual(DataType.Input, input.DataType);
        Assert.AreEqual(5, input.StartByteAdr);
        Assert.AreEqual(VarType.Byte, input.VarType);
    }

    [TestMethod]
    public void DataItem_FromAddress_WithCount_ShouldSetCount()
    {
        Assert.AreEqual(10, DataItem.FromAddress("DB1.DBB0", 10).Count);
    }

    [TestMethod]
    public void DataItem_FromAddress_WithInvalidBit_ShouldThrow()
    {
        Assert.ThrowsException<InvalidAddressException>(() => DataItem.FromAddress("DB1.DBX0.9"));
    }

    [TestMethod]
    public void LoadConfig_WithValidNodes_ShouldSucceed()
    {
        var receiver = CreateReceiver();

        var result = receiver.LoadConfig(new List<NodeItem>
        {
            Node("DB1.DBW4"),
            Node("DB1.DBD8")
        });

        Assert.IsTrue(result.State);
    }

    [TestMethod]
    public void LoadConfig_WithInvalidAddress_ShouldFail()
    {
        var receiver = CreateReceiver();

        var result = receiver.LoadConfig(new List<NodeItem> { Node("NOT-AN-ADDRESS") });

        Assert.IsFalse(result.State);
    }

    [TestMethod]
    public void Option_ShouldHaveExpectedDefaults()
    {
        var option = new DataReceiverPlcS7Option();

        Assert.AreEqual(CpuType.S71200, option.CpuType);
        Assert.AreEqual(0, option.Rack);
        Assert.AreEqual(2, option.Slot);
        Assert.AreEqual(30000, option.ReConnectPeriod);
        Assert.AreEqual(3, option.ReConnectTimes);
        Assert.AreEqual(60000, option.ConnectTimeout);
        Assert.IsFalse(option.AutoConnect);
    }
}
