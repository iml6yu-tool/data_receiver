using iml6yu.DataReceive.Core.Configs;
using System.IO.Ports;
using System.Text;
using System.Text.Json.Serialization;

namespace iml6yu.DataReceive.SerialCommunicate.Configs
{
    public class DataReceiverSerialCommunicateOption : DataReceiverOption
    {
        /// <summary>
        /// 串口名称
        /// </summary>
        public string PortName { get; set; }

        /// <summary>
        /// 波特率
        /// </summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>
        /// 数据位
        /// </summary>
        public int DataBits { get; set; } = 8;

        /// <summary>
        /// 奇偶校验
        /// </summary>
        public Parity Parity { get; set; } = Parity.None;

        /// <summary>
        /// 停止位
        /// </summary>
        public StopBits StopBits { get; set; } = StopBits.One;

        /// <summary>
        /// 握手协议
        /// </summary>
        public Handshake Handshake { get; set; } = Handshake.None;

        /// <summary>
        /// 读取超时时间(毫秒)
        /// </summary>
        public int ReadTimeout { get; set; } = 500;

        /// <summary>
        /// 写入超时时间(毫秒)
        /// </summary>
        public int WriteTimeout { get; set; } = 500;

        /// <summary>
        /// 接收缓冲区大小
        /// </summary>
        public int ReadBufferSize { get; set; } = 4096;

        /// <summary>
        /// 发送缓冲区大小
        /// </summary>
        public int WriteBufferSize { get; set; } = 2048;

        /// <summary>
        /// 是否启用RTS控制
        /// </summary>
        public bool RtsEnable { get; set; } = false;

        /// <summary>
        /// 是否启用DTR控制
        /// </summary>
        public bool DtrEnable { get; set; } = false;
        public int ReceivedBytesThreshold { get; set; } = 1;
        public Encoding Encoding { get; set; } = Encoding.Default;

        /// <summary>
        /// 通讯方式 单工 双工 半双工
        /// </summary>
        public SerialCommunicateTransferType TransferType { get; set; } = SerialCommunicateTransferType.FullDuplex;

        /// <summary>
        /// 数据包配置
        /// </summary>
        public DataReceiverSerialCommunicateDataPackageOption DataPackageOption { get; set; }
    }

    /// <summary>
    /// 传输方式
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SerialCommunicateTransferType
    {
        /// <summary>
        /// 单工通讯
        /// </summary>
        Simplex,
        /// <summary>
        /// 半双工通讯
        /// </summary>
        HalfDuplex,
        /// <summary>
        /// 全双工通讯
        /// </summary>
        FullDuplex
    }
}