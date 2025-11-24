namespace iml6yu.DataService.Core.Configs
{
    public class DataServiceStorageOption<TStorageId>
    {
        /// <summary>
        /// SlaveId（byte）或者是opcua的FolderName（string)
        /// </summary>
        public TStorageId Id { get; set; }
        /// <summary>
        /// 心跳配置
        /// </summary>
        public HeartOption Heart { get; set; }
        /// <summary>
        /// 默认值
        /// </summary>
        public List<DataServiceStorageDefaultObjectItem> DefaultValues { get; set; }
    }
}
