#nullable enable

using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Serialization;
using Rhizomode.SharedKernel;
using UnityEngine;

namespace Rhizomode.Graph.Runtime
{
    /// <summary>
    /// <see cref="HydrationPlan"/> を受けて Deserialize 順序を駆動する executor。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 6 Round C 実装:
    /// <code>
    /// using var scope = new GraphMutationScope(eventBus);
    /// 全 node について:
    ///   factory.Create(typeName, nodeId)
    ///   node.Position = entry.Position.ToUnity()
    ///   if INodeParamAccessor: TrySetParam(name, value) (Snapshot 由来の ParamValue)
    ///   runtime.RegisterNode(node, Deserialize)  // BeforeSetup → Setup → AfterSetup → EmitNodeAdded
    ///   scope.RecordNodeAdded(nodeId)
    /// 全 edge について:
    ///   runtime.AddEdge(edgeId, from, fromPort, to, toPort)
    ///   scope.RecordEdgeAdded(edgeId)
    /// runtime.NotifyAfterDeserialize()
    /// scope.Dispose()  // OnGraphChanged を 1 回発火
    /// </code>
    ///
    /// Phase 7 で GraphHydrator (GraphData → HydrationPlan) が作られ、GraphSaveLoadManager.LoadGraph
    /// が legacy GraphState.Deserialize の代わりに本 Executor を呼ぶ。Phase 8 で
    /// GameBootstrap.ReinjectModulesAfterLoad 削除。
    /// </remarks>
    public sealed class HydrationPlanExecutor
    {
        private readonly NodeRuntime _runtime;

        public HydrationPlanExecutor(NodeRuntime runtime)
        {
            _runtime = runtime;
        }

        /// <summary>
        /// HydrationPlan を実行してグラフを復元する。
        /// </summary>
        /// <param name="plan">復元対象の plan (Hydrator が生成)。</param>
        /// <param name="factory">ノード生成 factory (CompositeNodeFactory 想定)。</param>
        public void Execute(HydrationPlan plan, INodeFactory factory)
        {
            using var scope = new GraphMutationScope(_runtime.EventBus);

            foreach (var entry in plan.Nodes)
            {
                var node = factory.Create(entry.TypeName, entry.NodeId);
                if (node == null)
                {
                    Debug.LogWarning($"[HydrationPlanExecutor] Unknown node type: {entry.TypeName} ({entry.NodeId})");
                    continue;
                }

                node.Position = new Vector3(entry.Position.X, entry.Position.Y, entry.Position.Z);

                if (node is INodeParamAccessor accessor)
                {
                    foreach (var kvp in entry.ParamValues)
                    {
                        if (!accessor.TrySetParam(kvp.Key, kvp.Value))
                        {
                            Debug.LogWarning(
                                $"[HydrationPlanExecutor] TrySetParam failed: {entry.NodeId}.{kvp.Key}");
                        }
                    }
                }

                _runtime.RegisterNode(node, NodeInitMode.Deserialize);
                scope.RecordNodeAdded(entry.NodeId);
            }

            foreach (var edge in plan.Edges)
            {
                if (_runtime.AddEdge(edge.EdgeId, edge.FromNodeId, edge.FromPortName,
                        edge.ToNodeId, edge.ToPortName))
                {
                    scope.RecordEdgeAdded(edge.EdgeId);
                }
                else
                {
                    Debug.LogWarning(
                        $"[HydrationPlanExecutor] Edge restoration failed: " +
                        $"{edge.FromNodeId}.{edge.FromPortName} → {edge.ToNodeId}.{edge.ToPortName}");
                }
            }

            _runtime.NotifyAfterDeserialize();

            // scope.Dispose() (using) で OnGraphChanged を 1 回発火
        }
    }
}
