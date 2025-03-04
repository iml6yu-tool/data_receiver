using iml6yu.Data.Core;
using iml6yu.Result;
using Microsoft.Extensions.Logging;

namespace iml6yu.DataPublish.Core
{
    public abstract class DataPublisher<TClient, TOption, TPushContent> : IDataPublisher<TClient, TOption, TPushContent>
    where TOption : DataPublisherOption
    {
        /// <summary>
        /// 客户端
        /// </summary>
        public TClient Client { get; set; }

        /// <summary>
        /// 客户端配置
        /// </summary>
        public TOption Option { get; set; }

        public event EventHandler<ConnectArgs> ConnectionEvent;
        public event EventHandler<ExceptionArgs> ExceptionEvent;
        protected abstract bool IsConnected { get; }

        protected ILogger Logger { get; }
        /// <summary>
        /// 触发连接事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected void OnConnectionEvent(object sender, ConnectArgs args)
        {
            ConnectionEvent?.Invoke(sender, args);
        }
        /// <summary>
        /// 触发异常事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected void OnExceptionEvent(object sender, ExceptionArgs args)
        {
            ExceptionEvent?.Invoke(sender, args);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="option"></param>
        /// <param name="logger"></param>
        public DataPublisher(TOption option, ILogger logger)
        {
            Option = option;
            Logger = logger;
            CreateClient(option);
        } 
        /// <summary>
        /// 初始化
        /// </summary>
        /// <returns></returns>
        protected abstract TClient CreateClient(TOption option);

        /// <summary>
        /// 释放
        /// </summary>
        public virtual void Dispose()
        {
            DisConnectAsync().Wait();
        }

        public bool VerifyConnect()
        {
            if (Client == null)
                return false;
            return IsConnected;
        }

//#error 通过外部设置connect 和disconnect action的方式进行处理
        public abstract Task<MessageResult> ConnectAsync();

        public abstract Task<MessageResult> DisConnectAsync();

        public async Task<MessageResult> PushAsnyc(TPushContent datas)
        {
            if (Option == null || string.IsNullOrEmpty(Option.ChannelName))
                return MessageResult.Failed(ResultType.SystemConfigError, "publisher config error,option not config or channleName is null,please check it", null);

            if (!VerifyConnect())
                await ConnectAsync();

            return await PushDataAsync(Option.ChannelName, datas);
        }

        public abstract Task<MessageResult> PushDataAsync(string channelName, TPushContent data);


    }
}
