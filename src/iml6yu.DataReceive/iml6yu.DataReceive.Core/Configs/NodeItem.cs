using System.Text.Json.Serialization;

namespace iml6yu.DataReceive.Core.Configs
{
    /// <summary>
    /// 设备节点配置
    /// </summary> 
    public class NodeItem
    {
        public string FullAddress { get; set; }
        public string Address { get; set; }
        /// <summary>
        /// 值类型 对应TypeCode枚举类型 
        /// <list type="bullet">
        /// <item>Empty</item>  
        /// <item>Object</item>  
        /// <item>DBNull</item>  
        /// <item>Boolean</item>  
        /// <item>Char</item>  
        /// <item>SByte</item>  
        /// <item>Byte</item>  
        /// <item>Int16</item>  
        /// <item>UInt16</item>  
        /// <item>Int32</item>  
        /// <item>UInt32</item>  
        /// <item>Int64</item>  
        /// <item>UInt64</item>  
        /// <item>Single</item>  
        /// <item>Double</item>  
        /// <item>Decimal</item>  
        /// <item>DateTime</item>  
        /// <item>String</item>  
        /// </list>
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))] 
        public TypeCode ValueType { get; set; }
        public string Descript { get; set; }
        /// <summary>
        /// 分组信息 
        /// </summary>
        public string GroupName { get; set; }

        public object Value { get; set; }


        /// <summary>
        /// 定时查询数据 主动查询数据专用
        /// </summary>
        public int Interval { get; set; }

        /// <summary>
        /// 读取数据长度 
        /// </summary>
        public int Count { get; set; } = 1;
        ///// <summary>
        ///// 是否订阅opc数据(opc专用）
        ///// </summary>
        //public bool IsSubscribe { get; set; }
    }
}
