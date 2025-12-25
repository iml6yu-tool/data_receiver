using iml6yu.Data.Core.Models;
using iml6yu.DataService.Core;
using iml6yu.DataService.Core.Configs;
using iml6yu.DataService.OpcUa.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json.Linq;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using System;
using System.Numerics;

namespace iml6yu.DataService.OpcUa
{
    public class DataServiceOpcUa : IDataService
    {

        public string Name => Option.ServiceName;

        public bool IsRuning
        {
            get
            {
                try
                {
                    return OpcUaInstance != null
                   && StandardServer != null
                   && NodeManager != null
                   && StandardServer.CurrentInstance != null
                   && StandardServer.CurrentState == ServerState.Running;
                }
                catch (Exception ex)
                {
                    return false;
                }

            }
        }
        protected DataServiceOpcUaOption Option { get; }

        protected CancellationToken StopToken { get; set; }

        protected ILogger Logger { get; set; }

        protected ApplicationInstance? OpcUaInstance { get; private set; }
        protected DataServiceNodeManagerFactory NodeManagerFactory { get; private set; }
        protected DataServiceNodeManager? NodeManager => NodeManagerFactory?.NodeManager;
        protected StandardServer? StandardServer { get; private set; }
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="option">配置</param>
        public DataServiceOpcUa(DataServiceOpcUaOption option, ILogger logger)
        {
            Logger = logger;
            Option = option;
            var nodeConfig = option.Storages.ToDictionary(t => t.Id, t => t.DefaultValues.ToList());

            #region 处理心跳地址
            var allDefaultNodes = option.Storages.SelectMany(t => t.DefaultValues.ToList()).ToList();
            var heartId = $"HeartByAutoCrate{DateTime.Now.ToString("yyyyMMddHHmmss")}";
            foreach (var storage in option.Storages)
            {
                if (storage.Heart != null && !string.IsNullOrEmpty(storage.Heart.HeartAddress))
                {
                    //默认地址不包含心跳地址
                    if (!allDefaultNodes.Any(t => t.Address == storage.Heart.HeartAddress))
                    {
                        if (!nodeConfig.ContainsKey(heartId))
                            nodeConfig.Add(heartId, new List<DataServiceStorageDefaultObjectItem>());

                        nodeConfig[heartId].Add(new DataServiceStorageDefaultObjectItem()
                        {
                            Address = storage.Heart.HeartAddress,
                            ValueType = storage.Heart.HeartType == HeartType.Time ? TypeCode.DateTime : TypeCode.Int32,
                            DefaultValue = storage.Heart.HeartType == HeartType.Time ? DateTime.Now : 0
                        });
                    }
                }
            }
            #endregion 
            NodeManagerFactory = new DataServiceNodeManagerFactory(nodeConfig);
        }
        public void StartServicer(CancellationToken token)
        {
            if (StandardServer == null)
            {
                StandardServer = CreateServer(Option);
            }

            OpcUaInstance.StartAsync(StandardServer).Wait(token);
            AsyncHeartBeat();
            StopToken = token;
        }
        public void StopServicer()
        {
            try
            {
                StandardServer?.Stop();
                OpcUaInstance?.Stop();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
            }
            finally
            {
                OpcUaInstance = null;
                StandardServer = null;
            }

        }

        public async Task<List<MessageResult>> WriteAsync(DataWriteContract data)
        {
            var results = new List<MessageResult>();
            foreach (var item in data.Datas)
                results.Add(await WriteAsync(item));
            return results;
        }

        public async Task<MessageResult> WriteAsync(DataWriteContractItem data)
        {
            return await WriteAsync(data.Address, data.Value);
        }

        public async Task<MessageResult> WriteAsync<T>(string address, T data)
        {
            if (NodeManager == null)
                return MessageResult.Failed(ResultType.Failed, "还未初始化完成！Init is not Compated!");
            try
            {

                var node = NodeManager.Find(address) as BaseDataVariableState;
                if (node == null)
                    return MessageResult.Failed(ResultType.Failed, $"当前地址（{address}）不存在！The Address({address}) is not found!");
                node.Value = data;
                node.StatusCode = StatusCodes.Good;
                node.Timestamp = DateTime.Now;
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                return MessageResult.Failed(ResultType.DeviceReadError, ex.Message, ex);
            }

        }

        private StandardServer CreateServer(DataServiceOpcUaOption option)
        {
            OpcUaInstance = new ApplicationInstance();
            // 创建基础配置（替代加载外部XML文件）
            Opc.Ua.ApplicationConfiguration config = new Opc.Ua.ApplicationConfiguration
            {
                ApplicationName = option.ServiceName,
                ApplicationUri = Utils.Format("urn:{0}:{1}", option.IPAddress, option.ServiceName),
                ProductUri = $"urn:iml6yu:{option.ServiceName}",
                SecurityConfiguration = new SecurityConfiguration
                {
                    // 自动接受所有客户端证书（仅为演示方便，生产环境慎用）
                    AutoAcceptUntrustedCertificates = true,
                    // 设置证书存储路径
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".ua_certs/own")
                    },
                    TrustedIssuerCertificates = new CertificateTrustList()
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".ua_certs/issuer"),
                    },
                    TrustedPeerCertificates = new CertificateTrustList()
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".ua_certs/peer"),
                    },
                    TrustedUserCertificates = new CertificateTrustList()
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".ua_certs/user")
                    },
                    UserIssuerCertificates = new CertificateTrustList()
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".ua_certs/userIssuer")
                    },
                },
                ServerConfiguration = new ServerConfiguration
                {
                    // 服务器基地址，使用4840等OPC UA常见端口
                    BaseAddresses = { $"opc.tcp://{option.IPAddress}:{option.Port}/" },
                    // 安全策略：配置一个无需安全策略的端点便于测试
                    SecurityPolicies = new ServerSecurityPolicyCollection
                        {
                            new ServerSecurityPolicy {
                                SecurityMode = MessageSecurityMode.None,
                                SecurityPolicyUri = SecurityPolicies.None
                            }
                        },
                    // 用户令牌策略：允许匿名访问
                    UserTokenPolicies = new UserTokenPolicyCollection()
                    {
                        new UserTokenPolicy(UserTokenType.Anonymous)
                    }
                },
                // 传输配额等设置使用默认值
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 }
            };
            config.ValidateAsync(ApplicationType.Server).Wait();
            OpcUaInstance.ApplicationConfiguration = config;


            OpcUaInstance.CheckApplicationInstanceCertificatesAsync(false,0).AsTask().Wait();

            //if (!cert)
            //{
            //    throw new Exception("Application instance certificate invalid!");
            //}
            // 3. 创建服务器实例并启动
            StandardServer server = new StandardServer();
            server.AddNodeManager(NodeManagerFactory);
            return server;
        }

        protected void AsyncHeartBeat()
        {
            foreach (var slave in Option.Storages)
            {
                if (slave.Heart == null || string.IsNullOrEmpty(slave.Heart.HeartAddress))
                    continue;
                var heartNode = NodeManager?.Find(slave.Heart.HeartAddress) as BaseDataVariableState;
                if (heartNode == null)
                {
                    Logger.LogError($"未配置心跳节点，心跳地址({slave.Heart.HeartAddress})不存在,The hear address({slave.Heart.HeartAddress}) not exist.");
                    continue;
                }
                var ts = TimeSpan.FromMilliseconds(slave.Heart.HeartInterval);
                switch (slave.Heart.HeartType)
                {
                    case Core.Configs.HeartType.OddAndEven:
                        Task.Run(async () => { await WriteHeartDataAsync(heartNode, 0, ts, v => ((v++) % 2)); });
                        break;
                    case Core.Configs.HeartType.Number:
                        Task.Run(async () => { await WriteHeartDataAsync(heartNode, 0, ts, v => { if (v > int.MaxValue) v = 0; return ++v; }); });
                        break;
                    case Core.Configs.HeartType.Time:
                        Task.Run(async () => { await WriteHeartDataAsync(heartNode, DateTime.Now, ts, v => DateTime.Now); });
                        break;
                }
            }
        }

        protected virtual async Task WriteHeartDataAsync<T>(BaseDataVariableState heartNode, T value, TimeSpan interval, Func<T, T> funcValue) where T : struct
        {
            heartNode.Value = value;
            heartNode.StatusCode = StatusCodes.Good;
            heartNode.Timestamp = DateTime.Now;
            if (StopToken.IsCancellationRequested)
                return;
            await Task.Delay(interval);
            value = funcValue(value);
            await WriteHeartDataAsync(heartNode, value, interval, funcValue);
        }
    }
}
