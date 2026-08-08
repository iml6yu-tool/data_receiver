using iml6yu.DataReceive.SerialCommunicate.Configs;
using System;

namespace iml6yu.DataReceive.SerialCommunicate
{
    /// <summary>
    /// 数据校验辅助类
    /// 提供对多种校验类型的支持：校验和(CheckSum)、CRC8、CRC16、CRC32
    /// </summary>
    internal class CheckHelper
    {
        #region CRC查表法预计算表

        /// <summary>
        /// CRC8 预计算表
        /// 多项式: 0x31 (x^8 + x^5 + x^4 + 1)
        /// </summary>
        private static readonly byte[] Crc8Table = GenerateCrc8Table();

        /// <summary>
        /// CRC16 预计算表
        /// 多项式: 0x1021 (CCITT标准)
        /// </summary>
        private static readonly ushort[] Crc16Table = GenerateCrc16Table();

        /// <summary>
        /// CRC32 预计算表
        /// 多项式: 0xEDB88320 (IEEE 802.3标准)
        /// </summary>
        private static readonly uint[] Crc32Table = GenerateCrc32Table();

        #endregion

        /// <summary>
        /// 校验数据的完整性
        /// </summary>
        /// <param name="dataWithoutCrc">待校验的数据（不含校验位）</param>
        /// <param name="checkBytes">接收到的校验位字节数组</param>
        /// <param name="checkType">校验类型</param>
        /// <returns>校验是否通过</returns>
        internal static bool CheckData(byte[] dataWithoutCrc, byte[] checkBytes, DataCheckType checkType)
        {
            if (dataWithoutCrc == null || dataWithoutCrc.Length == 0)
                return false;

            if (checkBytes == null || checkBytes.Length == 0)
                return false;

            byte[] calculatedCheckBytes = null;

            switch (checkType)
            {
                case DataCheckType.None:
                    return true;

                case DataCheckType.CheckSum:
                    calculatedCheckBytes = CalculateCheckSum(dataWithoutCrc);
                    break;

                case DataCheckType.CRC8:
                    calculatedCheckBytes = CalculateCRC8(dataWithoutCrc);
                    break;

                case DataCheckType.CRC16:
                    calculatedCheckBytes = CalculateCRC16(dataWithoutCrc);
                    break;

                case DataCheckType.CRC32:
                    calculatedCheckBytes = CalculateCRC32(dataWithoutCrc);
                    break;

                case DataCheckType.Custom:
                default:
                    return false;
            }

            if (calculatedCheckBytes == null)
                return false;

            if (calculatedCheckBytes.Length != checkBytes.Length)
                return false;

            for (int i = 0; i < calculatedCheckBytes.Length; i++)
            {
                if (calculatedCheckBytes[i] != checkBytes[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 计算校验和（CheckSum）
        /// 将所有字节累加，返回2字节结果（大端序）
        /// </summary>
        /// <param name="data">待计算的数据</param>
        /// <returns>2字节校验和结果</returns>
        internal static byte[] CalculateCheckSum(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            ushort sum = 0;
            foreach (byte b in data)
            {
                sum += b;
            }

            byte[] result = BitConverter.GetBytes(sum);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);

            return result;
        }

        /// <summary>
        /// 计算CRC8校验值（查表法）
        /// 使用多项式 0x31 (x^8 + x^5 + x^4 + 1)
        /// 初始值: 0x00
        /// </summary>
        /// <param name="data">待计算的数据</param>
        /// <returns>1字节CRC8结果</returns>
        internal static byte[] CalculateCRC8(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            byte crc = 0x00;

            foreach (byte b in data)
            {
                // 使用预计算表进行快速CRC计算
                crc = Crc8Table[crc ^ b];
            }

            return new byte[] { crc };
        }

        /// <summary>
        /// 计算CRC16校验值（查表法，CCITT标准）
        /// 使用多项式 0x1021
        /// 初始值: 0xFFFF
        /// </summary>
        /// <param name="data">待计算的数据</param>
        /// <returns>2字节CRC16结果（大端序）</returns>
        internal static byte[] CalculateCRC16(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            ushort crc = 0xFFFF;

            foreach (byte b in data)
            {
                // 使用预计算表进行快速CRC计算
                crc = (ushort)((crc << 8) ^ Crc16Table[(crc >> 8) ^ b]);
            }

            byte[] result = BitConverter.GetBytes(crc);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);

            return result;
        }

        /// <summary>
        /// 计算CRC32校验值（查表法，IEEE 802.3标准）
        /// 使用多项式 0xEDB88320
        /// 初始值: 0xFFFFFFFF
        /// 最终异或值: 0xFFFFFFFF
        /// </summary>
        /// <param name="data">待计算的数据</param>
        /// <returns>4字节CRC32结果（大端序）</returns>
        internal static byte[] CalculateCRC32(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            uint crc = 0xFFFFFFFF;

            foreach (byte b in data)
            {
                // 使用预计算表进行快速CRC计算
                crc = (crc >> 8) ^ Crc32Table[(crc & 0xFF) ^ b];
            }

            crc ^= 0xFFFFFFFF;

            byte[] result = BitConverter.GetBytes(crc);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);

            return result;
        }

        #region 预计算表生成方法

        /// <summary>
        /// 生成CRC8预计算表
        /// </summary>
        /// <returns>256字节的CRC8查找表</returns>
        private static byte[] GenerateCrc8Table()
        {
            byte[] table = new byte[256];
            byte polynomial = 0x31;

            for (int i = 0; i < 256; i++)
            {
                byte crc = (byte)i;
                for (int j = 0; j < 8; j++)
                {
                    crc = (byte)((crc & 0x80) != 0 ? ((crc << 1) ^ polynomial) : (crc << 1));
                }
                table[i] = crc;
            }

            return table;
        }

        /// <summary>
        /// 生成CRC16预计算表（CCITT标准）
        /// </summary>
        /// <returns>256个元素的CRC16查找表</returns>
        private static ushort[] GenerateCrc16Table()
        {
            ushort[] table = new ushort[256];
            ushort polynomial = 0x1021;

            for (int i = 0; i < 256; i++)
            {
                ushort crc = (ushort)(i << 8);
                for (int j = 0; j < 8; j++)
                {
                    crc = (ushort)((crc & 0x8000) != 0 ? ((crc << 1) ^ polynomial) : (crc << 1));
                }
                table[i] = crc;
            }

            return table;
        }

        /// <summary>
        /// 生成CRC32预计算表（IEEE 802.3标准）
        /// </summary>
        /// <returns>256个元素的CRC32查找表</returns>
        private static uint[] GenerateCrc32Table()
        {
            uint[] table = new uint[256];
            uint polynomial = 0xEDB88320;

            for (int i = 0; i < 256; i++)
            {
                uint crc = (uint)i;
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 1) != 0 ? ((crc >> 1) ^ polynomial) : (crc >> 1);
                }
                table[i] = crc;
            }

            return table;
        }

        #endregion
    }
}