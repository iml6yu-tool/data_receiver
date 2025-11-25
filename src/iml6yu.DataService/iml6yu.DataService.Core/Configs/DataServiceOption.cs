namespace iml6yu.DataService.Core.Configs
{
    public class DataServiceOption
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// 服务的存储点位
        /// </summary>
        public List<DataServiceStorageOption> Storages { get; set; }
         
    }
}
