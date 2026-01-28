using Microsoft.VisualStudio.TestTools.UnitTesting;
using iml6yu.Data.Core.JsonConverts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.Data.Core.JsonConverts.Tests
{
    [TestClass()]
    public class JsonAndObjectConverterTests
    {
        [TestMethod()]
        public void ObjectToJsonTest()
        {
            SampleClass a = new SampleClass() { Id = 1, Value = DateTime.Now };
            string json = a.ObjectToJson("yyyy年MM月dd日 hh:mm:ss");
            Console.WriteLine(json);
            Assert.IsTrue(!string.IsNullOrEmpty(json));
        }

        [TestMethod()]
        public void JsonToObjectTest()
        {
            var json = "{\"Id\":1,\"Value\":\"2024年06月15日 10:30:45\"}";
            var obj = json.JsonToObject<SampleClass>();
            Assert.IsTrue(obj != null && obj.Id == 1);
        }

        [TestMethod()]
        public void JsonToObjectTest1()
        {
            var json = "{\"Id\":1,\"Value\":\"2024年06月15日\"}";
            var obj = json.JsonToObject<SampleClass>();
            Assert.IsTrue(obj != null && obj.Id == 1);
        }

        [TestMethod()]
        public void JsonToObjectTest2()
        {
            var json = "{\"Id\":1,\"Value\":2}";
            var obj = json.JsonToObject<SampleClass>();
            Assert.IsTrue(obj != null && obj.Id == 1);
        }
    }

    public class SampleClass
    {
        public int Id { get; set; }
        public object Value { get; set; }
    }
}