using iml6yu.Data.Core.Models;
using iml6yu.DataService.Core;
using iml6yu.DataService.OpcUa.Configs;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json.Linq;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

namespace iml6yu.DataService.OpcUa
{
    public class DataServiceOpcUa : IDataService
    {

        public string Name => Option.ServiceName;

        public bool IsRuning
        {
            get
            {
                return OpcUaInstance != null
                    && StandardServer != null
                    && NodeManager != null
                    && StandardServer.CurrentState == ServerState.Running;  
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
            NodeManagerFactory = new DataServiceNodeManagerFactory();
            StandardServer = CreateServer(option);
        }
        public void StartServicer(CancellationToken token)
        {
            StopToken = token;
            OpcUaInstance.StartAsync(StandardServer).Wait();
            InitDataServiceStorages(Option);
            AsyncHeartBeat();
        }

        private void InitDataServiceStorages(DataServiceOpcUaOption option)
        {
            // 初始化默认值
            option.Storages?.ForEach(storageOption =>
            {
                storageOption.DefaultValues.ForEach(item =>
                {
                    NodeManager.BaseNodeFolder.AddChild(new BaseDataVariableState(NodeManager.BaseNodeFolder)
                    {
                        NodeId = new NodeId(item.Address, NodeManager.NamespaceIndex),
                        BrowseName = new QualifiedName(item.Address, NodeManager.NamespaceIndex),
                        DisplayName = new LocalizedText(item.Address),
                        DataType = GetDataTypeId(item.ValueType),
                        ValueRank = ValueRanks.Scalar,
                        AccessLevel = AccessLevels.CurrentRead | AccessLevels.CurrentWrite,
                        UserAccessLevel = AccessLevels.CurrentRead | AccessLevels.CurrentWrite,
                        Value = GetDataValue(item.DefaultValue, item.ValueType),
                        StatusCode = StatusCodes.Good,
                        Timestamp = DateTime.Now
                    });
                });
            });
        }

        private object GetDataValue(object defaultValue, TypeCode? valueType)
        {
            if (!valueType.HasValue)
                return defaultValue;
            return Convert.ChangeType(defaultValue, valueType.Value);
        }

        private NodeId GetDataTypeId(TypeCode? valueType)
        {
            if (!valueType.HasValue)
                throw new ArgumentException("未配置ValueType,无法初始化。ValueType is required!");

            switch (valueType.Value)
            {
                case TypeCode.Boolean:
                    return DataTypeIds.Boolean;
                case TypeCode.Byte:
                    return DataTypeIds.Byte;
                case TypeCode.SByte:
                    return DataTypeIds.SByte;
                case TypeCode.String:
                    return DataTypeIds.String;
                case TypeCode.Char:
                    return DataTypeIds.String;
                case TypeCode.DateTime:
                    return DataTypeIds.DateTime;
                case TypeCode.Decimal:
                    return DataTypeIds.Decimal;
                case TypeCode.Double:
                    return DataTypeIds.Double;
                case TypeCode.Int16:
                    return DataTypeIds.Int16;
                case TypeCode.Int32:
                    return DataTypeIds.Int32;
                case TypeCode.Int64:
                    return DataTypeIds.Int64;
                case TypeCode.Single:
                    return DataTypeIds.Float;
                case TypeCode.UInt16:
                    return DataTypeIds.UInt16;
                case TypeCode.UInt32:
                    return DataTypeIds.UInt32;
                case TypeCode.UInt64:
                    return DataTypeIds.UInt64;

                default:
                    throw new ArgumentException($"ValueType的值 {valueType} 不支持！The {valueType} is not NotImplemented");
            }
        }

        public void StopServicer()
        {
            StandardServer?.Stop();
            OpcUaInstance?.Stop();
            OpcUaInstance = null;
            StandardServer = null;
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
            try
            {
                var node = NodeManager.Find(address) as BaseDataVariableState;
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
                        StorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "own")
                    },
                    TrustedIssuerCertificates = new CertificateTrustList()
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "issuer"),
                    },
                    TrustedPeerCertificates = new CertificateTrustList()
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "peer"),
                    },
                    TrustedUserCertificates = new CertificateTrustList()
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user")
                    },
                    UserIssuerCertificates = new CertificateTrustList()
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "userIssuer")
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


            bool cert = OpcUaInstance.CheckApplicationInstanceCertificatesAsync(false).AsTask().Result;

            if (!cert)
            {
                throw new Exception("Application instance certificate invalid!");
            }
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
                var heartNode = NodeManager.Find(slave.Heart.HeartAddress) as BaseDataVariableState;
                if (heartNode == null)
                {
                    Logger.LogError("配置的心跳地址节点不存在，请在初始化节点中配置心跳地址！heart node is not exist,please config it in DefaultValues.");
                    continue;
                }
                string msg = string.Empty;
                switch (slave.Heart.HeartType)
                {
                    case Core.Configs.HeartType.OddAndEven:
                        Task.Run(async () => { await WriteHeartDataAsync(heartNode, 0, TimeSpan.FromSeconds(slave.Heart.HeartInterval), v => ((v++) % 2)); });
                        break;
                    case Core.Configs.HeartType.Number:
                        Task.Run(async () => { await WriteHeartDataAsync(heartNode, 0, TimeSpan.FromSeconds(slave.Heart.HeartInterval), v => { if (v > int.MaxValue) v = 0; return ++v; }); });
                        break;
                    case Core.Configs.HeartType.Time:
                        Task.Run(async () => { await WriteHeartDataAsync(heartNode, DateTime.Now, TimeSpan.FromSeconds(slave.Heart.HeartInterval), v => DateTime.Now); });
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
