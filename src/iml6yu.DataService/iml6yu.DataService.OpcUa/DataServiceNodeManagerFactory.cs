using iml6yu.DataService.Core.Configs;
using Opc.Ua;
using Opc.Ua.Server;

namespace iml6yu.DataService.OpcUa
{
    public class DataServiceNodeManagerFactory : INodeManagerFactory
    {
        private Dictionary<string, List<DataServiceStorageDefaultObjectItem>> defaultNodes;
        private object nodeManagerLock = new object();

        public DataServiceNodeManagerFactory(Dictionary<string, List<DataServiceStorageDefaultObjectItem>> baseNodes)
        {
            this.defaultNodes = baseNodes;
        }

        public StringCollection NamespacesUris => new string[] { "http://iml6yu-dataservice-server" };

        public DataServiceNodeManager NodeManager { get; private set; }
        public INodeManager Create(IServerInternal server, ApplicationConfiguration configuration)
        {
            lock (nodeManagerLock)
            {
                if (NodeManager == null)
                    NodeManager = new DataServiceNodeManager(server, configuration, defaultNodes, NamespacesUris.ToArray());
            }
            return NodeManager;
        }
    }
}
