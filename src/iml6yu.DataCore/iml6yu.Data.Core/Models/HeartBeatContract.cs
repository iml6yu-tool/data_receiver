using System.Text.Json.Serialization;

namespace iml6yu.Data.Core.Models
{
    /// <summary>
    /// 心跳协议
    /// </summary>
    public class HeartBeatContract
    { /// <summary>
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
        public List<HeartBeatContractItem> Datas { get; set; }
    }
    /// <summary>
    /// 心跳协议数据项
    /// </summary>
    public class HeartBeatContractItem
    {
        /// <summary>
        /// 地址
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 描述信息
        /// </summary>
        public string Message { get; set; }

        public HeartBeatState State { get; set; } = HeartBeatState.UnKnown;
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
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
        UnKnown = 100
    }
}
