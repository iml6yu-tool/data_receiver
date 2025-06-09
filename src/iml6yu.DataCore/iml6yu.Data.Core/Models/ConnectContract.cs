using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.Data.Core.Models
{
    public class ConnectContract
    {  /// <summary>
       /// 唯一ID 重传判定
       /// </summary>
        public long Id { get; set; }
        /// <summary>
        /// 产线名称
        /// </summary>
        public string Key { get; set; }
        /// <summary>
        /// 时间戳 毫秒级别
        /// </summary>
        public long Timestamp { get; set; }
        /// <summary>
        /// 数据
        /// </summary>
        public List<ConnectContractItem> Datas { get; set; }
    }


    public class ConnectContractItem
    {
        /// <summary>
        /// 地址
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 描述信息
        /// </summary>
        public string Message { get; set; }

        public bool IsConnected { get; set; } = true;
    }
}
