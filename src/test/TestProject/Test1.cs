using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestProject
{
    [TestClass]
    public sealed class Test1
    {
        [TestMethod]
        public void TestMethod1()
        {

            byte a = 8;
            double b = 8.00;
            float c = 8.00f;
            int d = 8;
            uint e = 8u;
            long f = 8L;

            Assert.IsTrue(a == b && b == c && c == d && d == e && e == f);
        }

        [TestMethod]
        public void MyTestMethodBox2Box()
        {
            byte a = 8;
            object b = a; // Boxing
            double c = (byte)b; // Unboxing and converting to double

            Assert.AreEqual(a, c);
        }
    }
}
