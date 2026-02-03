using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.Data.Core.Models
{
    /// <summary>
    /// 报警
    /// </summary>
    public class WarnContract
    {
        /// <summary>
        /// 唯一ID 重传判定
        /// </summary>
        public long Id { get; set; }
        /// <summary>
        /// 关键字，业务使用
        /// </summary>
        public string Key { get; set; }
        /// <summary>
        /// 时间戳 毫秒级别
        /// </summary>
        public long Timestamp { get; set; }
        /// <summary>
        /// 数据
        /// </summary>
        public IEnumerable<WarnContractItem> Datas { get; set; }
    }

    /// <summary>
    /// 报警条目
    /// </summary>
    public class WarnContractItem
    {
        /// <summary>
        /// 地址
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 值
        /// </summary>
        public string Value { get; set; }

        public bool IsOpen { get; set; } = true;
        public string? FaultCode { get; set; }
        public string? Message { get; set; }
        public int FaultLevel { get; set; }

        public string LineName { get; set; }

        public string CreateTime { get; set; }
    }
}
