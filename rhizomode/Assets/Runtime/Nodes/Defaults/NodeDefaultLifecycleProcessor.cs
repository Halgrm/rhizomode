#nullable enable

using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using Rhizomode.SharedKernel;

namespace Rhizomode.Nodes.Defaults
{
    /// <summary>
    /// 新規スポーン時のみ <see cref="NodeDefaultsRegistry"/> を参照して
    /// ノードに default 値を流し込む lifecycle processor。
    /// </summary>
    /// <remarks>
    /// Plan v5.3-2 (boundary CI で固定): 本 processor はノード具体型を一切知らない。
    /// <see cref="INodeParamAccessor.TrySetParam"/> のみ呼ぶため、新ノード追加で
    /// 本ファイルを修正する必要はない (registry に typeName を追加するだけ)。
    ///
    /// 適用タイミング: <see cref="NodeInitMode.FreshSpawn"/> のみ。Deserialize/PresetImport/UndoRedo
    /// では saved value を上書きしないため発火しない。
    /// </remarks>
    public sealed class NodeDefaultLifecycleProcessor : INodeLifecycleProcessor
    {
        private readonly NodeDefaultsRegistry _registry;

        public NodeDefaultLifecycleProcessor(NodeDefaultsRegistry registry)
        {
            _registry = registry;
        }

        public void BeforeSetup(NodeBase node, NodeInitMode mode)
        {
            if (mode != NodeInitMode.FreshSpawn) return;
            if (node is not INodeParamAccessor accessor) return;

            foreach (var entry in _registry.GetDefaultsFor(node.NodeType))
            {
                accessor.TrySetParam(entry.ParamName, entry.Value);
            }
        }

        public void AfterSetup(NodeBase node, NodeInitMode mode)
        {
            // no-op
        }

        public void AfterDeserialize(GraphState state)
        {
            // no-op
        }
    }
}
