﻿using iml6yu.Data.Core;
using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.Result;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace iml6yu.DataReceive.Core
{
    public interface IDataReceiver : IDisposable
    { 
        /// <summary>
        /// 状态
        /// </summary>
        ReceiverState State { get; }
        /// <summary>
        /// 名字
        /// </summary>
        string Name { get; }

        bool IsConnected { get; }
        /// <summary>
        /// 连接事件，当连接状态发生变化时触发
        /// </summary>

        event EventHandler<ConnectArgs> ConnectionEvent;
        /// <summary>
        /// 错误事件，当类库内发生错误时触发
        /// </summary>

        event EventHandler<ExceptionArgs> ErrorEvent;
        /// <summary>
        /// 警告事件，当类库内发生警告时触发
        /// </summary>

        event EventHandler<WarnArgs> WarnEvent;

        /// <summary>
        /// 当数据发生变动时触发
        /// </summary>

        event EventHandler<DataReceiveContract> DataChangedEvent;
        /// <summary>
        /// 数据定时读取出发事件
        /// </summary>
        event EventHandler<DataReceiveContract> DataIntervalEvent;

        /// <summary>
        /// 当订阅数据发生变动时触发
        /// </summary>

        event EventHandler<DataReceiveContract> DataSubscribeEvent;

     
        /// <summary>
        /// 加载配置
        /// </summary>
        /// <returns></returns>
        MessageResult LoadConfig(List<NodeItem> nodes);
        /// <summary>
        /// 加载配置
        /// </summary>
        /// <returns></returns>
        Task<MessageResult> LoadConfigAsync(string optionNodeFile);
        /// <summary>
        /// 验证连接状态
        /// </summary>
        /// <returns></returns>
        bool VerifyConnect();

        /// <summary>
        /// 连接
        /// </summary>
        /// <returns></returns>
        Task<MessageResult> ConnectAsync();
        /// <summary>
        /// 断开连接
        /// </summary>
        /// <returns></returns>
        Task<MessageResult> DisConnectAsync();

        /// <summary>
        /// 读取全部数据
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<CollectionResult<DataReceiveContractItem>> ReadAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="addressArray"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<CollectionResult<DataReceiveContractItem>> ReadsAsync(IEnumerable<string> addressArray, CancellationToken cancellationToken = default);

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<DataResult<DataReceiveContractItem>> ReadAsync(string address, CancellationToken cancellationToken = default);

        /// <summary>
        /// 写数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        Task<MessageResult> WriteAsync(DataWriteContract data);
        /// <summary>
        /// 写单条数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        Task<MessageResult> WriteAsync(DataWriteContractItem data);

        /// <summary>
        /// 写单条数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Task<MessageResult> WriteAsync<T>(string address, T data);
        /// <summary>
        /// 订阅某些Node的值
        /// </summary>
        /// <param name="key">当前订阅的key</param>
        /// <param name="addressArray"></param>
        /// <returns></returns>
        MessageResult Subscribe(string key, IEnumerable<string> addressArray);
        /// <summary>
        /// 取消订阅某个key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        MessageResult UnSubscrite(string key);

        /// <summary>
        /// 开始轮询读取
        /// </summary>
        Task StartWorkAsync(CancellationToken tokenSource);

        /// <summary>
        /// 停止轮询读取操作
        /// </summary>
        /// <returns></returns>
        Task StopWorkAsync();

        /// <summary>
        /// 设置转换器
        /// </summary>
        /// <param name="dataParse"></param>
        void SetDataParse(Func<string, Dictionary<string, ReceiverTempDataValue>> dataParse);
    }
    /// <summary>
    /// 数据采集
    /// </summary>
    /// <typeparam name="TClient">客户端</typeparam>
    /// <typeparam name="TOption">参数</typeparam>
    public interface IDataReceiver<TClient, TOption> : IDataReceiver
        where TClient : class
        where TOption : DataReceiverOption
    {
        /// <summary>
        /// 客户端
        /// </summary>
        TClient Client { get; set; }

        /// <summary>
        /// 客户端配置
        /// </summary>
        TOption Option { get; set; } 
    }
}
