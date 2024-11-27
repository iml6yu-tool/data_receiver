using iml6yu.Data.Core.Models;

namespace iml6yu.DataCenter.Contracts.EArgs
{
    public class DataArgs : EventArgs
    { 
        public List<DataReceiveContract> Datas { get; set; } 
    }
}
