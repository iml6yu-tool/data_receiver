using iml6yu.Data.Core;
using iml6yu.Result;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.DataContracts;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataPublish.Core
{
    /// <summary>
    /// 推送类
    /// </summary>
    /// <typeparam name="TClient">推送客户端</typeparam>
    /// <typeparam name="TOption">推送客户端配置参数</typeparam>
    /// <typeparam name="TPushContent">推送内容</typeparam>
    public interface IDataPublisher<TClient, TOption, TPushContent> : IDisposable
        where TOption : DataPublisherOption
    { 
        /// <summary>
        /// 客户端
        /// </summary>
        TClient Client { get; set; }

        /// <summary>
        /// 客户端配置
        /// </summary>
        TOption Option { get; set; }
        /// <summary>
        /// 连接事件
        /// </summary>
        event EventHandler<ConnectArgs> ConnectionEvent;

        /// <summary>
        /// 设备异常事件
        /// </summary>
        event EventHandler<ExceptionArgs> ExceptionEvent;

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
        /// 推送数据
        /// </summary>
        /// <param name="datas">推送的数据</param>
        /// <returns></returns>
        Task<MessageResult> PushAsnyc(TPushContent datas);
    }
}
