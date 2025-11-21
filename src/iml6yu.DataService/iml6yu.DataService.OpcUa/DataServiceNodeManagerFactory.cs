using Opc.Ua;
using Opc.Ua.Server;

namespace iml6yu.DataService.OpcUa
{
    public class DataServiceNodeManagerFactory : INodeManagerFactory
    {
        private object nodeManagerLock = new object();
        public StringCollection NamespacesUris => new string[] { "http://iml6yu-dataservice-server" };

        public DataServiceNodeManager NodeManager { get; private set; }
        public INodeManager Create(IServerInternal server, ApplicationConfiguration configuration)
        {
            lock (nodeManagerLock)
            {
                if (NodeManager == null)
                    NodeManager = new DataServiceNodeManager(server, configuration, NamespacesUris.ToArray());
            }
            return NodeManager;
        }
    }
}
