using iml6yu.DataReceive.Core.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataReceive.ModbusMaster
{
    /// <summary>
    /// 配置项
    /// </summary>
    public class ModbusReadItem
    {
        /// <summary>
        /// 读取类型
        /// </summary>
        public ModbusReadWriteType ReadType { get; set; }
        /// <summary>
        /// 起始点位
        /// </summary>
        public ushort StartPoint { get; set; }
        /// <summary>
        /// 读取的点位个数
        /// </summary>
        public ushort NumberOfPoint { get; set; }

        /// <summary>
        /// 真正需要读取的配置节点
        /// </summary>
        public List<NodeItem> ReadNodes { get; set; }
    }
}
