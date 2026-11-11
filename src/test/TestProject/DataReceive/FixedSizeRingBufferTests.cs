using iml6yu.DataReceive.Core;

namespace TestProject.DataReceive;

[TestClass]
public class FixedSizeRingBufferTests
{
    [TestMethod]
    public void Add_ShouldKeepMaxCapacity()
    {
        var buffer = new FixedSizeRingBuffer<int>(3);

        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4);

        Assert.AreEqual(3, buffer.MaxCapacity);
        Assert.AreEqual(3, buffer.Count);
    }

    [TestMethod]
    public void HasAnyDifferent_WhenEmpty_ShouldReturnFalse()
    {
        var buffer = new FixedSizeRingBuffer<string>(5);

        var result = buffer.HasAnyDifferent("a", out var diffIndex);

        Assert.IsFalse(result);
        Assert.AreEqual(-1, diffIndex);
    }

    [TestMethod]
    public void HasAnyDifferent_WhenLatestDifferent_ShouldReturnZeroIndex()
    {
        var buffer = new FixedSizeRingBuffer<int>(5);
        buffer.Add(1);
        buffer.Add(1);
        buffer.Add(2);

        var result = buffer.HasAnyDifferent(3, out var diffIndex);

        Assert.IsTrue(result);
        Assert.AreEqual(0, diffIndex);
    }

    [TestMethod]
    public void HasAnyDifferent_WhenOlderDifferent_ShouldReturnItsIndex()
    {
        var buffer = new FixedSizeRingBuffer<int>(5);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(2);

        var result = buffer.HasAnyDifferent(2, out var diffIndex);

        Assert.IsTrue(result);
        Assert.AreEqual(2, diffIndex);
    }

    [TestMethod]
    public void HasAnyDifferent_WhenAllSameButJustWritten_ShouldTreatAsChanging()
    {
        var buffer = new FixedSizeRingBuffer<int>(5);
        buffer.Add(7);

        var result = buffer.HasAnyDifferent(7, out var diffIndex);

        Assert.IsTrue(result);
        Assert.AreEqual(-1, diffIndex);
    }

    [TestMethod]
    public void Clear_ShouldResetBuffer()
    {
        var buffer = new FixedSizeRingBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);

        buffer.Clear();

        Assert.AreEqual(0, buffer.Count);
        Assert.IsFalse(buffer.HasAnyDifferent(1, out var diffIndex));
        Assert.AreEqual(-1, diffIndex);
    }

    [TestMethod]
    public void Add_ShouldBeThreadSafe()
    {
        var buffer = new FixedSizeRingBuffer<int>(5);

        Parallel.For(0, 20_000, i => buffer.Add(i));

        Assert.AreEqual(5, buffer.Count);
    }
}
