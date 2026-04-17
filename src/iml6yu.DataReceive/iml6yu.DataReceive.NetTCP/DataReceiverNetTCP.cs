using iml6yu.Data.Core.Models;
using iml6yu.DataReceive.Core;
using iml6yu.DataReceive.Core.Configs;
using iml6yu.DataReceive.Core.Models;
using iml6yu.DataReceive.NetTCP.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Sockets;

namespace iml6yu.DataReceive.NetTCP
{
    public class DataReceiverNetTCP : DataReceiver<TcpClient, DataReceiverNetTCPOption>
    {
        public DataReceiverNetTCP(DataReceiverNetTCPOption option, ILogger logger, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null) : base(option, logger, isAutoLoadNodeConfig, nodes)
        {
        }

        public DataReceiverNetTCP(DataReceiverNetTCPOption option, ILogger logger, Func<string, Dictionary<string, ReceiverTempDataValue>> dataParse, bool isAutoLoadNodeConfig = false, List<NodeItem> nodes = null) : base(option, logger, dataParse, isAutoLoadNodeConfig, nodes)
        {
        }

        public override bool IsConnected => throw new NotImplementedException();

        public override Task<MessageResult> ConnectAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<DataResult<DataReceiveContract>> DirectReadAsync(IEnumerable<DataReceiveContractItem> addressArray, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task<MessageResult> DisConnectAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<MessageResult> WriteAsync(DataWriteContract data)
        {
            throw new NotImplementedException();
        }

        public override Task<MessageResult> WriteAsync(DataWriteContractItem data)
        {
            throw new NotImplementedException();
        }

        public override Task<MessageResult> WriteAsync<T>(string address, T data)
        {
            throw new NotImplementedException();
        }

        protected override TcpClient CreateClient(DataReceiverNetTCPOption option)
        {
            throw new NotImplementedException();
        }

        protected override Task WhileDoAsync(CancellationToken tokenSource)
        {
            throw new NotImplementedException();
        }
    }
}
