using iml6yu.Data.Core.Models;
using iml6yu.Result;

namespace iml6yu.DataService.Core
{
    public interface IDataService
    {
        string Name { get; }

        /// <summary>
        /// 运行状态
        /// </summary>
        bool IsRuning { get; }
        /// <summary>
        /// 启动服务
        /// </summary>
        void StartServicer(CancellationToken token);
        /// <summary>
        /// 停止服务
        /// </summary>
        void StopServicer();
        //public Dictionary<DataServiceModbusSlaveOption, Dictionary<ModbusSlaveStore, Array>> GetDatas();

        Task<List<MessageResult>> WriteAsync(DataWriteContract data);

        Task<MessageResult> WriteAsync(DataWriteContractItem data);

        public Task<MessageResult> WriteAsync<T>(string address, T data);

    }
}
