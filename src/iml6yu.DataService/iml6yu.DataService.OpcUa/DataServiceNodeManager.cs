using iml6yu.DataService.Core.Configs;
using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace iml6yu.DataService.OpcUa
{
    public class DataServiceNodeManager : CustomNodeManager2
    {
        private Dictionary<string, List<DataServiceStorageDefaultObjectItem>> defaultNodes;
        ///// <summary>
        ///// 目录根节点
        ///// </summary>
        //public List<FolderState> BaseNodeFolders { get; private set; } = new List<FolderState>();

        public DataServiceNodeManager(IServerInternal server, Dictionary<string, List<DataServiceStorageDefaultObjectItem>> folderNodeNames, params string[] namespaceUris) : base(server, namespaceUris)
        {
            this.defaultNodes = folderNodeNames;
        }

        public DataServiceNodeManager(IServerInternal server, ApplicationConfiguration configuration, Dictionary<string, List<DataServiceStorageDefaultObjectItem>> folderNodeNames, params string[] namespaceUris) : base(server, configuration, namespaceUris)
        {
            this.defaultNodes = folderNodeNames;
        }

        public DataServiceNodeManager(IServerInternal server, ApplicationConfiguration configuration, bool useSamplingGroups, Dictionary<string, List<DataServiceStorageDefaultObjectItem>> folderNodeNames, params string[] namespaceUris) : base(server, configuration, useSamplingGroups, namespaceUris)
        {
            this.defaultNodes = folderNodeNames;
        }

        protected override NodeStateCollection LoadPredefinedNodes(ISystemContext context)
        {
            NodeStateCollection predefinedNodes = new NodeStateCollection();
            foreach (var name in this.defaultNodes.Keys)
            {
                var node = AddBaseFolderNode(name);
                predefinedNodes.Add(node);
                foreach (var item in this.defaultNodes[name])
                {
                    if (item == null) continue;
                    BaseDataVariableState? dataNode = CreateBaseDataVariableState(item, node);
                    if (dataNode != null)
                    {
                        node.AddChild(dataNode);
                        predefinedNodes.Add(dataNode);
                    }
                }
            }

            return predefinedNodes;
        }
        public object GetDataValue(object defaultValue, TypeCode? valueType)
        {
            if (!valueType.HasValue)
                return defaultValue;
            return Convert.ChangeType(defaultValue, valueType.Value);
        }

        public NodeId GetDataTypeId(TypeCode? valueType)
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
        public FolderState AddBaseFolderNode(string name)
        {

            // 1. 检查并创建 BaseNodeFolder（根文件夹）
            NodeId baseFolderNodeId = new NodeId(name, NamespaceIndex);

            // 关键步骤：尝试查找节点是否已存在
            var BaseNodeFolder = Find(baseFolderNodeId) as FolderState;

            if (BaseNodeFolder == null)
            {
                // 在Objects文件夹下创建一个名为"DataService"的文件夹
                BaseNodeFolder = new FolderState(null)
                {
                    NodeId = baseFolderNodeId, // 节点ID：ns=2;s=DemoData
                    BrowseName = new QualifiedName(name, NamespaceIndex),
                    DisplayName = new LocalizedText(name),
                    TypeDefinitionId = ObjectTypeIds.FolderType
                };
            }
            // 将该文件夹链接到OPC UA地址空间的"Objects"根文件夹下
            BaseNodeFolder.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
            return BaseNodeFolder;
        }


        public BaseDataVariableState? CreateBaseDataVariableState(DataServiceStorageDefaultObjectItem item, FolderState node)
        {
            var nodeId = new NodeId(item.Address);
            // 关键步骤：尝试查找节点是否已存在
            BaseDataVariableState? dataNode = Find(nodeId) as BaseDataVariableState;
            //当前节点已经存在，直接返回null,避免重复添加
            if (dataNode != null) return null;

            dataNode = new BaseDataVariableState(node)
            {
                NodeId = new NodeId(item.Address),
                BrowseName = new QualifiedName(item.Address),
                DisplayName = new LocalizedText(item.Address),
                DataType = GetDataTypeId(item.ValueType),
                ValueRank = ValueRanks.Scalar,
                AccessLevel = AccessLevels.CurrentRead | AccessLevels.CurrentWrite,
                UserAccessLevel = AccessLevels.CurrentRead | AccessLevels.CurrentWrite,
                Value = GetDataValue(item.DefaultValue, item.ValueType),
                StatusCode = StatusCodes.Good,
                Timestamp = DateTime.Now
            };
            return dataNode;
        }
    }
}
