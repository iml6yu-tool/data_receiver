using iml6yu.Data.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataReceive.Core.Models
{
    public record CacheDataItem
    {
        public long Timestamp { get; set; }
        public DataReceiveContractItem Data { get; set; }
        public CacheDataItem(long ts, DataReceiveContractItem data)
        {
            Timestamp = ts;
            Data = data;
        }
    }
}
