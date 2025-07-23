using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataReceive.ModbusMaster
{
    /// <summary>
    /// 读取的配置信息
    /// </summary>
    public class ModbusReadConfig
    {
        public byte SlaveId { get; set; }
        public List<ModbusReadItem> ReadItems { get; set; }
    }

}
