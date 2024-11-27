using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.Data.Core.Models
{
    /// <summary>
    /// 写入协议
    /// </summary>
    public class DataWriteContract
    {
        public long Id { get; set; }
        /// <summary>
        /// 关键字，业务使用
        /// </summary>
        public string? Key { get; set; }

        public IEnumerable<DataWriteContractItem> Datas { get; set; }

    }
}
