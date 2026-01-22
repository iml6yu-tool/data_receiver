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
