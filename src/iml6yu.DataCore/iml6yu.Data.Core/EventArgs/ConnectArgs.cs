namespace iml6yu.Data.Core
{
    /// <summary>
    /// 连接状态参数
    /// </summary>
    public class ConnectArgs : EventArgs
    {
        public ConnectArgs()
        {
        }

        public ConnectArgs(bool isConntion, string message = null)
        {
            IsConntion = isConntion;
            Message = message;
        }


        /// <summary>
        /// 是否是连接状态
        /// </summary>
        public bool IsConntion { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string? Message { get; set; }
    }
}
