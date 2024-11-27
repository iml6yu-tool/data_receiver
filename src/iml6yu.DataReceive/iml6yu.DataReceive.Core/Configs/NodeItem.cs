using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        /// ValueType 对应的code类型
        /// </summary>
        public TypeCode ValueTypeCode { get; private set; }
        private string valueType { get; set; }
        public string ValueType
        {
            get { return valueType; }
            set
            {
                valueType = value;
                ValueTypeCode = Enum.Parse<TypeCode>(value);
            }
        }
        public string Descript { get; set; }
        /// <summary>
        /// 分组信息 
        /// </summary>
        public string GroupName { get; set; }

        public object Value { get; set; }


        /// <summary>
        /// 定时查询数据(opc专用）
        /// </summary>
        public int Interval { get; set; }

        /// <summary>
        /// 是否订阅opc数据(opc专用）
        /// </summary>
        public bool IsSubscribe { get; set; }
    }
}
