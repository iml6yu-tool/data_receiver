namespace iml6yu.DataReceive.Core.Configs
{
    /// <summary>
    /// 接收器Options
    /// </summary>
    public class DataReceiverOption
    {
        /// <summary>
        /// 业务关联的产线
        /// </summary>
        public string ProductLineName { get; set; }
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
        public string ReceiverName { get; set; }

        /// <summary>
        /// 节点路径
        /// </summary>
        public string NodeFile { get; set; }
        /// <summary>
        /// 是否自动连接
        /// </summary>
        public bool AutoConnect { get; set; } = false;

    }
}
