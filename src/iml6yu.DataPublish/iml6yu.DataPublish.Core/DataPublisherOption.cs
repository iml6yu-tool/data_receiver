namespace iml6yu.DataPublish.Core
{
    public class DataPublisherOption
    {
        /// <summary>
        /// 数据源连接用户名
        /// </summary>
        public string OriginName { get; set; }
        /// <summary>
        /// 数据源连接密码
        /// </summary>
        public string OriginPwd { get; set; }
        /// <summary>
        /// 数据源Host
        /// </summary>
        public string OriginHost { get; set; }
        /// <summary>
        /// 数据源端口
        /// </summary>
        public int? OriginPort { get; set; }
        /// <summary>
        /// 重连间隔 ms
        /// </summary>
        public int ReConnectPeriod { get; set; } = 60000;
        /// <summary>
        /// 重连次数
        /// </summary>
        public int ReConnectTimes { get; set; } = 3;
        /// <summary>
        /// 连接超时 ms
        /// </summary>
        public int ConnectTimeout { get; set; } = 60000;

        /// <summary>
        /// 连接名称
        /// </summary>
        public string PublisherName { get; set; }

        /// <summary>
        /// 是否自动连接
        /// </summary>
        public bool AutoConnect { get; set; } = false;
        /// <summary>
        /// 是否自动工作
        /// </summary>
        public bool AutoWork { get; set; } = false;

        /// <summary>
        /// 推送频道名称（mq系列就是topic grpc就是method等）
        /// </summary>
        public string ChannelName { get; set; }
    }
}
