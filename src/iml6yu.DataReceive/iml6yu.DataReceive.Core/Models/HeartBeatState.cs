using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataReceive.Core.Models
{
    public enum HeartBeatState
    {
        /// <summary>
        /// 健康的
        /// </summary>
        Healthy = 0,
        /// <summary>
        /// 降级 / 异常
        /// </summary>
        Degraded = 2,
        /// <summary>
        /// 不可用
        /// </summary>
        Unavailable = 5,

        /// <summary>
        /// 不知道当前心跳状态
        /// </summary>
        UnKnown=100



    }
}
