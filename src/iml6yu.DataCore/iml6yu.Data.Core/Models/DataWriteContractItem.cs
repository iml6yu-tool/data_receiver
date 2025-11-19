using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.Data.Core.Models
{
    public class DataWriteContractItem
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
        public object? Value { get; set; }

        /// <summary>
        /// 是否是标志位
        /// <list type="bullet">
        /// <item>
        /// 标志位用于进行批量写入的时候作为最后一个验证位写入，当其他所有数据都写入成功后才最后写入这个数据位，对应的设备逻辑行为需要做适配开发才可以
        /// </item>
        /// </list>
        /// </summary>
        public bool IsFlag { get; set; } = false;

        //public static implicit operator DataReceiveContractItem(DataWriteContractItem item)
        //{
        //    return new DataReceiveContractItem()
        //    {
        //        Address = item.Address,
        //        ValueType = item.ValueType,
        //        Value = item.Value,
        //        Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
        //    };
        //}

        public static explicit operator DataWriteContractItem(DataReceiveContractItem item)
        {
            return new DataWriteContractItem
            {
                Address = item.Address,
                Value = item.Value,
                ValueType = item.ValueType,
                IsFlag = false
            };
        }
    }
}
