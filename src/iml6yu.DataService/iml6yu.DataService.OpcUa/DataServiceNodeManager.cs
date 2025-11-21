using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataService.OpcUa
{
    public class DataServiceNodeManager : CustomNodeManager2
    {
        /// <summary>
        /// 目录根节点
        /// </summary>
        public FolderState BaseNodeFolder { get; private set; }
        public DataServiceNodeManager(IServerInternal server, params string[] namespaceUris) : base(server, namespaceUris)
        {
        }

        public DataServiceNodeManager(IServerInternal server, ApplicationConfiguration configuration, params string[] namespaceUris) : base(server, configuration, namespaceUris)
        {
        }

        public DataServiceNodeManager(IServerInternal server, ApplicationConfiguration configuration, bool useSamplingGroups, params string[] namespaceUris) : base(server, configuration, useSamplingGroups, namespaceUris)
        {
        }

        protected override NodeStateCollection LoadPredefinedNodes(ISystemContext context)
        {

            NodeStateCollection predefinedNodes = new NodeStateCollection();
            // 在Objects文件夹下创建一个名为"DemoData"的文件夹
            BaseNodeFolder = new FolderState(null)
            {
                NodeId = new NodeId("DataService", NamespaceIndex), // 节点ID：ns=2;s=DemoData
                BrowseName = new QualifiedName("DataService", NamespaceIndex),
                DisplayName = new LocalizedText("DataService"),
                TypeDefinitionId = ObjectTypeIds.FolderType
            };
            // 将该文件夹链接到OPC UA地址空间的"Objects"根文件夹下
            BaseNodeFolder.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
            predefinedNodes.Add(BaseNodeFolder);

            // 在"BaseNodeFolder"文件夹下创建一个可读写的温度变量节点
            BaseDataVariableState testData = new BaseDataVariableState(BaseNodeFolder)
            {
                NodeId = new NodeId("TestData", NamespaceIndex), // 节点ID：ns=2;s=Temperature
                BrowseName = new QualifiedName("TestData", NamespaceIndex),
                DisplayName = new LocalizedText("TestData"),
                DataType = DataTypeIds.Boolean,
                ValueRank = ValueRanks.Scalar,
                AccessLevel = AccessLevels.CurrentRead | AccessLevels.CurrentWrite,
                UserAccessLevel = AccessLevels.CurrentRead | AccessLevels.CurrentWrite,
                Value = true, // 初始值
                StatusCode = StatusCodes.Good,
                Timestamp = DateTime.Now
            };
            BaseNodeFolder.AddChild(testData);
            predefinedNodes.Add(testData);

            return predefinedNodes;
        }
    }
}
