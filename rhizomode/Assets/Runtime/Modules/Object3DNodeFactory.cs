#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Modules;

namespace Rhizomode.Modules
{
    /// <summary>
    /// <see cref="Object3DPrefabList"/> SO から Object3D_ プレフィックスの動的 typeName を
    /// <see cref="INodeFactory"/> として公開する。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 4 follow-up: GameBootstrap.RegisterObject3DPrefabs を本 factory に置換する。
    ///
    /// typeName 規約: "Object3D_{prefabName}" → Object3DNode(id, prefabName)
    /// ※ Prefab の実体注入 (Instantiate + Object3DProxy 接続) は GameBootstrap が引き続き担当する。
    ///   本 factory はノードのインスタンスを生成するのみ。
    /// </remarks>
    public sealed class Object3DNodeFactory : INodeFactory
    {
        private const string Prefix = "Object3D_";

        private readonly HashSet<string> _prefabNames;

        public Object3DNodeFactory(Object3DPrefabList prefabList)
        {
            _prefabNames = new HashSet<string>();
            foreach (var prefab in prefabList.Prefabs)
            {
                if (prefab == null) continue;
                if (string.IsNullOrEmpty(prefab.name)) continue;
                _prefabNames.Add(prefab.name);
            }
        }

        public bool CanCreate(string typeName)
        {
            if (!typeName.StartsWith(Prefix, System.StringComparison.Ordinal)) return false;
            var prefabName = typeName.Substring(Prefix.Length);
            return _prefabNames.Contains(prefabName);
        }

        public NodeBase? Create(string typeName, string nodeId)
        {
            if (!CanCreate(typeName)) return null;
            var prefabName = typeName.Substring(Prefix.Length);
            return new Object3DNode(nodeId, prefabName);
        }

        public IEnumerable<string> AllTypeNames
        {
            get
            {
                foreach (var name in _prefabNames) yield return Prefix + name;
            }
        }
    }
}
