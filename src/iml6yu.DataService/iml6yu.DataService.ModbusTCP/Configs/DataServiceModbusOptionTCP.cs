using iml6yu.DataService.Modbus.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataService.ModbusTCP.Configs
{
    public class DataServiceModbusOptionTCP: DataServiceModbusOption
    {
        #region TCP
        public string IPAddress { get; set; }
        public int Port { get; set; } = 502;
        #endregion
    }
}
