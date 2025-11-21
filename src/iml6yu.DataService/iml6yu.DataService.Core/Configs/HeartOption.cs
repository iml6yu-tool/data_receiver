using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataService.Core.Configs
{
    public class HeartOption
    {
        /// <summary>
        /// 心跳类型
        /// </summary>
        public HeartType HeartType { get; set; }
        /// <summary>
        /// 心跳地址
        /// </summary>
        public string HeartAddress { get; set; }

        /// <summary>
        /// 心跳数据更新间隔 秒
        /// </summary>
        public int HeartInterval { get; set; }
    }
}
