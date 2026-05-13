#nullable enable

using System.Collections.Generic;
using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Serialization
{
    /// <summary>
    /// <see cref="GraphData"/> JSON DTO を <see cref="HydrationPlan"/> に変換する plan builder。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 7: 「data 変換のみ、実行しない」。
    /// <c>HydrationPlanExecutor</c> が plan を受け取り NodeRuntime 経由で BeforeSetup → Setup →
    /// AfterSetup → AfterDeserialize → OnGraphChanged を駆動する。
    ///
    /// Transitional 違反 (Phase 13 で resolve): NodeHydrationEntry.ParamValues は現状 empty で、
    /// legacy <c>NodeBase.RestoreParamsFromJson(paramsJson)</c> 経由で復元する。
    /// 各ノードが INodeParamReader を実装すれば paramsJson 経路は廃止可能。
    /// </remarks>
    public sealed class GraphHydrator
    {
        public HydrationPlan Build(GraphData data)
        {
            var nodes = new List<NodeHydrationEntry>(data.nodes.Count);
            foreach (var nd in data.nodes)
            {
                var pos = new RzVector3(
                    nd.position.Length > 0 ? nd.position[0] : 0f,
                    nd.position.Length > 1 ? nd.position[1] : 0f,
                    nd.position.Length > 2 ? nd.position[2] : 0f);
                nodes.Add(new NodeHydrationEntry(
                    nd.id, nd.type, pos,
                    new Dictionary<string, ParamValue>())
                {
                    ParamsJson = nd.paramsJson ?? ""
                });
            }

            var edges = new List<EdgeHydrationEntry>(data.edges.Count);
            foreach (var ed in data.edges)
            {
                edges.Add(new EdgeHydrationEntry(ed.id, ed.from, ed.fromPort, ed.to, ed.toPort));
            }

            return new HydrationPlan(nodes, edges);
        }
    }
}
