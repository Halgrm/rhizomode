#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Model;

namespace Rhizomode.NodeCatalog.Runtime
{
    /// <summary>
    /// 複数の <see cref="INodeFactory"/> を順次試す合成 factory。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 4 follow-up: 静的 (<see cref="AttributeScannerNodeFactory"/>) と
    /// 動的 (<see cref="Nodes.Scene.SceneTriggerNodeFactory"/>, Module/Object3D factory 等) を
    /// 1 つの contract に束ねる。先頭から順に CanCreate を試行し、最初に true を返した factory を使う。
    /// </remarks>
    public sealed class CompositeNodeFactory : INodeFactory
    {
        private readonly IReadOnlyList<INodeFactory> _factories;

        public CompositeNodeFactory(IReadOnlyList<INodeFactory> factories)
        {
            _factories = factories;
        }

        public bool CanCreate(string typeName)
        {
            foreach (var f in _factories)
            {
                if (f.CanCreate(typeName)) return true;
            }
            return false;
        }

        public NodeBase? Create(string typeName, string nodeId)
        {
            foreach (var f in _factories)
            {
                if (f.CanCreate(typeName)) return f.Create(typeName, nodeId);
            }
            return null;
        }
    }
}
