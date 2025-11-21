namespace iml6yu.DataService.Core.Configs
{
    public class DataServiceOption<TStorageId>
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// 服务的存储点位
        /// </summary>
        public List<DataServiceStorageOption<TStorageId>> Storages { get; set; }
         
    }
}
