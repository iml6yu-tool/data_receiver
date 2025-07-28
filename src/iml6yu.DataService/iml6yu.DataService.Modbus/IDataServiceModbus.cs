using iml6yu.Data.Core.Models;
using iml6yu.DataService.Modbus.Configs;
using iml6yu.Result;
using Microsoft.VisualBasic.FileIO;
using NModbus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataService.Modbus
{
    public interface IDataServiceModbus
    {
        string Name { get; }
        IModbusSlaveNetwork? Network { get; set; }

        /// <summary>
        /// 运行状态
        /// </summary>
        public bool IsRuning { get; }
        /// <summary>
        /// 启动服务
        /// </summary>
        public void StartServicer(CancellationToken token);
        /// <summary>
        /// 停止服务
        /// </summary>
        public void StopServicer();
        //public Dictionary<DataServiceModbusSlaveOption, Dictionary<ModbusSlaveStore, Array>> GetDatas();

        public Task<List<MessageResult>> WriteAsync(DataWriteContract data);

        public Task<MessageResult> WriteAsync(DataWriteContractItem data);

        public Task<MessageResult> WriteAsync<T>(string address, T data);
    }
}
