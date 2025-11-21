namespace iml6yu.DataService.Core.Configs
{
    public class DataServiceStorageOption<TStorageId>
    {
        public TStorageId Id { get; set; }
        public HeartOption Heart { get; set; }
        /// <summary>
        /// 默认值
        /// </summary>
        public List<DataServiceStorageDefaultObjectItem> DefaultValues { get; set; }
    }
}
