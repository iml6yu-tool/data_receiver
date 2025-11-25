namespace iml6yu.DataService.OpcUa.Configs
{
    public class DataServiceOpcUaOption : Core.Configs.DataServiceOption
    {
        /// <summary>
        /// 地址 默认localhost
        /// </summary>
        public string IPAddress { get; set; } = "localhost";
        /// <summary>
        /// 默认端口4840
        /// </summary>
        public int Port { get; set; } = 4840;
    }
}
