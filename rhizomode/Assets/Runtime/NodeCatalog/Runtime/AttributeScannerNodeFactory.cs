#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Model;

namespace Rhizomode.NodeCatalog.Runtime
{
    /// <summary>
    /// <see cref="NodeTypeAttributeScanner"/> 出力の <see cref="NodeTypeRegistration"/> 列を
    /// <see cref="INodeFactory"/> contract として公開する adapter。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 4: GraphMutationApplier から本 factory を経由してノードを生成する。
    /// 結果として GameBootstrap の NodeFactoryMap ハードコードは不要になる
    /// (Phase 5 で GameBootstrap 縮小と同時に剥がす)。
    /// </remarks>
    public sealed class AttributeScannerNodeFactory : INodeFactory
    {
        private readonly Dictionary<string, NodeTypeRegistration> _byTypeName;

        public AttributeScannerNodeFactory(IReadOnlyList<NodeTypeRegistration> registrations)
        {
            _byTypeName = new Dictionary<string, NodeTypeRegistration>(registrations.Count);
            foreach (var reg in registrations)
            {
                _byTypeName[reg.Display.TypeName] = reg;
            }
        }

        public bool CanCreate(string typeName) => _byTypeName.ContainsKey(typeName);

        public NodeBase? Create(string typeName, string nodeId)
        {
            if (!_byTypeName.TryGetValue(typeName, out var reg)) return null;
            return reg.Factory(nodeId);
        }

        public IEnumerable<string> AllTypeNames => _byTypeName.Keys;
    }
}
