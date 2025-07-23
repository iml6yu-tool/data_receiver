using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataService.Modbus.Configs
{
    public class ModbusSlaveStore
    {
        /// <summary>
        /// Store的类型 modbus默认的四个类型 
        /// </summary>
        public ModbusReadWriteType StoreType { get; set; }
        /// <summary>
        /// Store的申请长度
        /// </summary>
        public ushort NumberOfPoint { get; set; }
        /// <summary>
        /// 起始位  默认是0
        /// </summary>
        public ushort StartAddress { get; set; } = 0;

        /// <summary>
        /// 默认值 （待优化）
        /// </summary>
        public DataServiceStoreDefaultObjectItem[] DefaultValues { get; set; }
    }
}
