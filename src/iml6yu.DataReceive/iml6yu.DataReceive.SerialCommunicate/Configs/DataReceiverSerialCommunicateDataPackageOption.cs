using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace iml6yu.DataReceive.SerialCommunicate.Configs
{
    /// <summary>
    /// 数据包配置
    /// </summary>
    public class DataReceiverSerialCommunicateDataPackageOption
    {

        /// <summary>
        /// 数据头
        /// </summary>
        public byte[] Header { get; set; }
        /// <summary>
        /// 数据尾
        /// </summary>
        public byte[] Tail { get; set; }
        /// <summary>
        /// 数据包长度(包含数据头和数据尾)
        /// </summary>
        public int Length { get; set; }

        public DataCheckType CheckType { get; set; } = DataCheckType.None;
        /// <summary>
        /// 校验位的长度(单位:字节)，如果CheckType为Custom，则CheckLength表示自定义校验位的长度
        /// </summary>
        public int CheckLength { get; set; } = 0;

    }

    /// <summary>
    /// 数据校验类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DataCheckType
    {
        /// <summary>
        /// 无校验
        /// </summary>
        None = 0,
        /// <summary>
        /// 校验和
        /// </summary>
        CheckSum,
        /// <summary>
        /// CRC8
        /// </summary>
        CRC8,
        /// <summary>
        /// CRC16
        /// </summary>
        CRC16,
        /// <summary>
        /// CRC32
        /// </summary>
        CRC32,
        /// <summary>
        /// 自定义
        /// </summary>
        Custom = 100
    }
}
