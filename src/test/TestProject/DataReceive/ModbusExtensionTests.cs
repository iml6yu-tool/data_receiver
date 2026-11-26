using iml6yu.DataReceive.ModbusMaster;

namespace TestProject.DataReceive;

[TestClass]
public class ModbusExtensionTests
{
    [TestMethod]
    public void GetNumberOfPoint_ShouldReturnExpectedRegisterCount()
    {
        Assert.AreEqual((ushort)1, ModbusReadWriteType.Coils.GetNumberOfPoint());
        Assert.AreEqual((ushort)1, ModbusReadWriteType.Inputs.GetNumberOfPoint());
        Assert.AreEqual((ushort)1, ModbusReadWriteType.HoldingRegisters.GetNumberOfPoint());
        Assert.AreEqual((ushort)1, ModbusReadWriteType.ReadInputRegisters.GetNumberOfPoint());

        Assert.AreEqual((ushort)2, ModbusReadWriteType.HoldingRegisters2.GetNumberOfPoint());
        Assert.AreEqual((ushort)2, ModbusReadWriteType.HoldingRegisters2ByteSwap.GetNumberOfPoint());
        Assert.AreEqual((ushort)2, ModbusReadWriteType.HoldingRegistersFloat.GetNumberOfPoint());
        Assert.AreEqual((ushort)2, ModbusReadWriteType.HoldingRegistersFloatLittleEndian.GetNumberOfPoint());
        Assert.AreEqual((ushort)2, ModbusReadWriteType.ReadInputRegisters2.GetNumberOfPoint());
        Assert.AreEqual((ushort)2, ModbusReadWriteType.ReadInputRegistersFloat.GetNumberOfPoint());

        Assert.AreEqual((ushort)4, ModbusReadWriteType.HoldingRegisters4.GetNumberOfPoint());
        Assert.AreEqual((ushort)4, ModbusReadWriteType.HoldingRegisters4ByteSwap.GetNumberOfPoint());
        Assert.AreEqual((ushort)4, ModbusReadWriteType.HoldingRegistersDouble.GetNumberOfPoint());
        Assert.AreEqual((ushort)4, ModbusReadWriteType.HoldingRegistersLittleEndian4.GetNumberOfPoint());
        Assert.AreEqual((ushort)4, ModbusReadWriteType.ReadInputRegistersDouble.GetNumberOfPoint());
    }

    [TestMethod]
    public void ToModbusBooleanValues_ShouldMapToOneWhenValueIsOne()
    {
        CollectionAssert.AreEqual(new[] { true }, true.ToModbusBooleanValues());
        CollectionAssert.AreEqual(new[] { true }, ((byte)1).ToModbusBooleanValues());
        CollectionAssert.AreEqual(new[] { false }, ((byte)2).ToModbusBooleanValues());
        CollectionAssert.AreEqual(new[] { true }, 1.ToModbusBooleanValues());
        CollectionAssert.AreEqual(new[] { false }, 0.ToModbusBooleanValues());
        CollectionAssert.AreEqual(new[] { true }, "1".ToModbusBooleanValues());
        CollectionAssert.AreEqual(new[] { false }, "0".ToModbusBooleanValues());
    }

    [TestMethod]
    public void ToModbusUShortValues_BigEndian_ShouldSplitIntoRegisters()
    {
        CollectionAssert.AreEqual(new ushort[] { 0x1234, 0x5678 }, 0x12345678.ToModbusUShortValues());
        CollectionAssert.AreEqual(new ushort[] { 0x3F80, 0x0000 }, 1.0f.ToModbusUShortValues());
        CollectionAssert.AreEqual(new ushort[] { 0xABCD }, ((ushort)0xABCD).ToModbusUShortValues());
        CollectionAssert.AreEqual(new ushort[] { 0x0005 }, ((byte)5).ToModbusUShortValues());
    }

    [TestMethod]
    public void ToModbusUShortBSValues_ShouldSwapBytesInEachRegister()
    {
        CollectionAssert.AreEqual(new ushort[] { 0x3412, 0x7856 }, 0x12345678.ToModbusUShortBSValues());
    }

    [TestMethod]
    public void ToModbusUShortLEValues_ShouldReturnLittleEndianRegisterOrder()
    {
        CollectionAssert.AreEqual(new ushort[] { 0x7856, 0x3412 }, 0x12345678.ToModbusUShortLEValues());
    }

    [TestMethod]
    public void ToModbusUShortLEBSValues_ShouldReturnLittleEndianAndSwappedBytes()
    {
        CollectionAssert.AreEqual(new ushort[] { 0x5678, 0x1234 }, 0x12345678.ToModbusUShortLEBSValues());
    }

    [TestMethod]
    public void ToModbusUShortValues_WithCustomLength_ShouldResizeArray()
    {
        CollectionAssert.AreEqual(new ushort[] { 0, 0, 0, 1 }, 1.ToModbusUShortValues(8));
    }

    [TestMethod]
    public void ToModbusUShortValues_WithNull_ShouldThrowArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ((object)null!).ToModbusUShortValues());
    }

    [TestMethod]
    public void ToModbusUShortValues_WithUnsupportedType_ShouldThrowNotSupportedException()
    {
        Assert.ThrowsException<NotSupportedException>(() => "abc".ToModbusUShortValues());
    }
}
