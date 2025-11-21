using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace iml6yu.DataService.Core.Configs
{
    /// <summary>
    /// 心跳类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HeartType
    {
        /// <summary>
        /// 按照时间的时分秒组成数字类型写入
        /// </summary>
        Time,
        /// <summary>
        /// 数字递增，到达最大值后重置为最小值
        /// </summary>
        Number,
        /// <summary>
        /// 奇偶变换
        /// </summary>
        OddAndEven
    }
}
