#nullable enable

using System.Collections.Generic;
using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Serialization
{
    /// <summary>
    /// グラフ復元のための plan (Graph.Runtime に渡される pure data DTO)。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 (v5.1-5 で field ホワイトリスト化):
    /// HydrationPlan が保持してよいのは以下のみ:
    ///   - node id / type name / position / param values
    ///   - edge id / edge endpoint
    /// system 固有の復元情報 (shader property / prefab path / clip metadata 等) は持たない。
    /// それらは <c>Graph.Runtime.HydrationPlanExecutor</c> が
    /// <see cref="Rhizomode.Graph.Runtime.INodeLifecycleProcessor.AfterDeserialize"/> 経由で復元する。
    ///
    /// 違反検知: <c>Editor/HydrationPlanShapeValidator.cs</c> で field 型ホワイトリストを CI 検証 (Phase 1G/2)。
    /// </remarks>
    public sealed record HydrationPlan(
        IReadOnlyList<NodeHydrationEntry> Nodes,
        IReadOnlyList<EdgeHydrationEntry> Edges);

    public sealed record NodeHydrationEntry(
        string NodeId,
        string TypeName,
        RzVector3 Position,
        IReadOnlyDictionary<string, ParamValue> ParamValues);

    public sealed record EdgeHydrationEntry(
        string EdgeId,
        string FromNodeId,
        string FromPortName,
        string ToNodeId,
        string ToPortName);
}
