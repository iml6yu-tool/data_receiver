using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.Data.Core.Models
{
    public class DataReceiveContractItem
    {
        /// <summary>
        /// 地址
        /// </summary>
        public string Address { get; set; }
        /// <summary>
        /// 值类型 对应TypeCode枚举类型 
        /// <list type="bullet">
        /// <item>0 Empty</item>  
        /// <item>1 Object</item>  
        /// <item>2 DBNull</item>  
        /// <item>3 Boolean</item>  
        /// <item>4 Char</item>  
        /// <item>5 SByte</item>  
        /// <item>6 Byte</item>  
        /// <item>7 Int16</item>  
        /// <item>8 UInt16</item>  
        /// <item>9 Int32</item>  
        /// <item>10 UInt32</item>  
        /// <item>11 Int64</item>  
        /// <item>12 UInt64</item>  
        /// <item>13 Single</item>  
        /// <item>14 Double</item>  
        /// <item>15 Decimal</item>  
        /// <item>16 DateTime</item>  
        /// <item>18 String</item>  
        /// </list>
        /// </summary>
        public int ValueType { get; set; }
        /// <summary>
        /// 值
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// 时间戳 从设备中读取的时间戳
        /// </summary>
        public long? Timestamp { get; set; }
    }
}
