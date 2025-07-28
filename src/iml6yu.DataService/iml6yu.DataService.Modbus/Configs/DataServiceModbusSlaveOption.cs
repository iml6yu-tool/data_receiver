using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataService.Modbus.Configs
{
    public class DataServiceModbusSlaveOption
    {
        public byte Id { get; set; }
        public HeartOption Heart { get; set; }
        /// <summary>
        /// 默认值
        /// </summary>
        public List<DataServiceStoreDefaultObjectItem> DefaultValues { get; set; }
    }
}
