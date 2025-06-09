using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataReceive.Core.Models
{
    public enum ReceiverState
    {
        /// <summary>
        /// 准备中
        /// </summary>
        Reading=0,
        /// <summary>
        /// 准备就绪
        /// </summary>
        Ready = 1,
        /// <summary>
        /// 工作中
        /// </summary>
        Working = 2,
        /// <summary>
        /// 已停止
        /// </summary>
        Stoped = 3,
        /// <summary>
        /// 发生错误
        /// </summary>
        Error = 4,

        /// <summary>
        /// 未初始化
        /// </summary>
        Uninitialized=9999,
    }
}
