using Opc.Ua;
using Opc.Ua.Client;

namespace iml6yu.DataReceive.OPCUA
{
    /// <summary>
    /// 基于 OPC Foundation 官方类库（OPCFoundation.NetStandard.Opc.Ua.Client）实现的 OPC UA 客户端
    /// </summary>
    public sealed class OpcUaClient : IDisposable
    {
        private readonly SemaphoreSlim connectLock = new SemaphoreSlim(1, 1);
        private ApplicationConfiguration? configuration;
        private Session? session;

        /// <summary>
        /// 当前会话
        /// </summary>
        public Session? Session => session;

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool Connected => session != null && session.Connected;

        /// <summary>
        /// 客户端名称，同时作为会话名称
        /// </summary>
        public string OpcUaName { get; set; } = "iml6yu.DataReceive.OPCUA";

        /// <summary>
        /// 重连间隔（ms），由上层重连逻辑使用
        /// </summary>
        public int ReconnectPeriod { get; set; } = 30000;

        /// <summary>
        /// 连接超时（ms）
        /// </summary>
        public int ConnectTimeout { get; set; } = 60000;

        /// <summary>
        /// 会话超时（ms）
        /// </summary>
        public int SessionTimeout { get; set; } = 60000;

        /// <summary>
        /// 是否使用安全连接
        /// </summary>
        public bool UseSecurity { get; set; } = false;

        /// <summary>
        /// 用户身份，默认匿名
        /// </summary>
        public UserIdentity UserIdentity { get; set; } = new UserIdentity(new AnonymousIdentityToken());

        /// <summary>
        /// 连接服务器
        /// </summary>
        /// <param name="serverUrl">服务地址，例如 opc.tcp://127.0.0.1:4840</param>
        public async Task ConnectServer(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                throw new ArgumentNullException(nameof(serverUrl));

            if (Connected)
                return;

            await connectLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Connected)
                    return;

                await DisconnectAsync().ConfigureAwait(false);

                ApplicationConfiguration appConfig = await GetConfigurationAsync().ConfigureAwait(false);
                EndpointDescription endpointDescription = await CoreClientUtils.SelectEndpointAsync(appConfig, serverUrl, UseSecurity, ConnectTimeout).ConfigureAwait(false);
                EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(appConfig);
                ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);
                string sessionName = string.IsNullOrWhiteSpace(OpcUaName) ? appConfig.ApplicationName : OpcUaName;

                ISession createdSession = await DefaultSessionFactory.Instance.CreateAsync(appConfig, endpoint, false, sessionName, (uint)SessionTimeout, UserIdentity, null).ConfigureAwait(false);
                session = (Session)createdSession;
            }
            finally
            {
                connectLock.Release();
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            Session? current = session;
            session = null;
            if (current == null)
                return;
            try
            {
                await current.CloseAsync().ConfigureAwait(false);
            }
            catch
            {
                // 断开时忽略关闭会话产生的异常
            }
            current.Dispose();
        }

        /// <summary>
        /// 批量读取节点值
        /// </summary>
        public async Task<List<DataValue>> ReadNodesAsync(NodeId[] nodeIds, CancellationToken cancellationToken = default)
        {
            Session current = GetConnectedSession();
            ReadValueIdCollection nodesToRead = new ReadValueIdCollection(nodeIds.Select(t => new ReadValueId
            {
                NodeId = t,
                AttributeId = Attributes.Value
            }));
            ReadResponse response = await current.ReadAsync(null, 0, TimestampsToReturn.Both, nodesToRead, cancellationToken).ConfigureAwait(false);
            return response.Results.ToList();
        }

        /// <summary>
        /// 批量写入节点值
        /// </summary>
        /// <returns>全部写入成功返回true</returns>
        public async Task<bool> WriteNodesAsync(string[] addresses, object?[] values, CancellationToken cancellationToken = default)
        {
            if (addresses == null || values == null || addresses.Length == 0 || addresses.Length != values.Length)
                return false;

            Session current = GetConnectedSession();
            WriteValueCollection nodesToWrite = new WriteValueCollection(addresses.Select((address, index) =>
            {
                WriteValue node = new WriteValue
                {
                    NodeId = NodeId.Parse(address),
                    AttributeId = Attributes.Value
                };
                node.Value = new DataValue { Value = values[index]! };
                return node;
            }));

            WriteResponse response = await current.WriteAsync(null, nodesToWrite, cancellationToken).ConfigureAwait(false);
            return response.Results.All(StatusCode.IsGood);
        }

        /// <summary>
        /// 写入单个节点值
        /// </summary>
        public Task<bool> WriteNodeAsync<T>(string address, T data, CancellationToken cancellationToken = default)
        {
            return WriteNodesAsync(new[] { address }, new object?[] { data }, cancellationToken);
        }

        private Session GetConnectedSession()
        {
            Session? current = session;
            if (current == null || !current.Connected)
                throw new ServiceResultException(StatusCodes.BadNotConnected, $"{OpcUaName} 尚未连接到 OPC UA 服务器");
            return current;
        }

        private async Task<ApplicationConfiguration> GetConfigurationAsync()
        {
            if (configuration != null)
                return configuration;

            string appName = string.IsNullOrWhiteSpace(OpcUaName) ? "iml6yu.DataReceive.OPCUA" : OpcUaName;

            ApplicationConfiguration appConfig = new ApplicationConfiguration
            {
                ApplicationName = appName,
                ApplicationUri = $"urn:{System.Net.Dns.GetHostName()}:{appName}",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = "X509Store",
                        StorePath = "CurrentUser\\My",
                        SubjectName = appName
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = "Directory",
                        StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities"
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = "Directory",
                        StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications"
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = "Directory",
                        StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates"
                    },
                    AutoAcceptUntrustedCertificates = true
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = ConnectTimeout },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = SessionTimeout },
                TraceConfiguration = new TraceConfiguration()
            };

            await appConfig.ValidateAsync(ApplicationType.Client).ConfigureAwait(false);
            appConfig.CertificateValidator.CertificateValidation += (sender, args) => args.Accept = true;

            configuration = appConfig;
            return appConfig;
        }

        public void Dispose()
        {
            Session? current = session;
            session = null;
            if (current != null)
            {
                try
                {
                    current.CloseAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch
                {
                    // 释放时忽略关闭会话产生的异常
                }
                current.Dispose();
            }
            connectLock.Dispose();
        }
    }
}
