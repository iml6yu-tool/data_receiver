namespace iml6yu.Data.Core.Models
{
    /// <summary>
    /// 数据传输协议
    /// </summary>
    public class DataReceiveContract
    {
        public DataReceiveContract()
        {
            Id = Fingerprint.GetId();
            Datas = new List<DataReceiveContractItem>();
        }

        public DataReceiveContract(long ts) : this()
        {
            Timestamp = ts;
        }
        /// <summary>
        /// 唯一ID 重传判定
        /// </summary>
        public long Id { get; set; }
        /// <summary>
        /// 关键字，业务使用
        /// </summary>
        public string Key { get; set; }
        /// <summary>
        /// 时间戳 毫秒级别
        /// </summary>
        public long Timestamp { get; set; }
        /// <summary>
        /// 数据
        /// </summary>
        public List<DataReceiveContractItem> Datas { get; set; }
    }
}
