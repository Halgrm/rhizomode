#nullable enable

using System.Collections.Generic;

namespace Rhizomode.Graph.Snapshot
{
    /// <summary>
    /// グラフ全体の不変な像 (canonical immutable projection)。
    /// </summary>
    /// <remarks>
    /// Phase 2 で導入。Snapshot 二重責務 (Plan v5.0):
    /// (1) Undo runtime DTO: GraphCommand が Pre/Post state を Snapshot で保持
    /// (2) Serialization 中間表現: Serializer/Hydrator が GraphState ↔ Snapshot ↔ GraphData 変換
    /// これにより Graph.Mutation は Graph.Serialization を参照せず、Snapshot のみを参照する。
    /// </remarks>
    public sealed record GraphSnapshot(
        IReadOnlyList<NodeSnapshot> Nodes,
        IReadOnlyList<EdgeSnapshot> Edges)
    {
        public static readonly GraphSnapshot Empty =
            new(new List<NodeSnapshot>(), new List<EdgeSnapshot>());
    }
}
