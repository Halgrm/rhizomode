#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Model;

namespace Rhizomode.Nodes.Scene
{
    /// <summary>
    /// <see cref="SceneTriggerCatalog"/> を参照して SceneTriggerNode を typeName ごとに生成する factory。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 4 follow-up: 動的 typeName (SceneDark/SceneWhite/SceneNature) を
    /// <see cref="INodeFactory"/> contract で公開する。<see cref="NodeCatalog.Runtime.AttributeScannerNodeFactory"/>
    /// と <see cref="CompositeNodeFactory"/> 等で合成される想定。
    /// </remarks>
    public sealed class SceneTriggerNodeFactory : INodeFactory
    {
        private readonly SceneTriggerCatalog _catalog;

        public SceneTriggerNodeFactory(SceneTriggerCatalog catalog)
        {
            _catalog = catalog;
        }

        public bool CanCreate(string typeName) => _catalog.FindByTypeName(typeName) != null;

        public NodeBase? Create(string typeName, string nodeId)
        {
            var entry = _catalog.FindByTypeName(typeName);
            if (entry == null) return null;
            return new SceneTriggerNode(nodeId, entry.TypeName, entry.SceneIndex);
        }

        public IEnumerable<string> AllTypeNames
        {
            get
            {
                foreach (var e in _catalog.Entries) yield return e.TypeName;
            }
        }
    }
}
