#nullable enable

using System.Collections.Generic;

namespace Rhizomode.Graph.Events
{
    /// <summary>
    /// 1 つの mutation scope で発生したグラフ変更を id ベースで集約した DTO。
    /// </summary>
    /// <remarks>
    /// Plan v5.3: 「GraphChangeSet DTO/id ベース」。
    /// concrete <see cref="Rhizomode.Graph.Model.NodeBase"/> や <see cref="Rhizomode.Graph.Model.Edge"/>
    /// を含まない (受信側が graph state から再 fetch する設計、id 不変)。
    /// </remarks>
    public sealed record GraphChangeSet(
        IReadOnlyList<string> AddedNodeIds,
        IReadOnlyList<string> RemovedNodeIds,
        IReadOnlyList<string> AddedEdgeIds,
        IReadOnlyList<string> RemovedEdgeIds,
        IReadOnlyList<string> ChangedParamNodeIds)
    {
        public static readonly GraphChangeSet Empty = new(
            System.Array.Empty<string>(),
            System.Array.Empty<string>(),
            System.Array.Empty<string>(),
            System.Array.Empty<string>(),
            System.Array.Empty<string>());

        public bool IsEmpty =>
            AddedNodeIds.Count == 0 &&
            RemovedNodeIds.Count == 0 &&
            AddedEdgeIds.Count == 0 &&
            RemovedEdgeIds.Count == 0 &&
            ChangedParamNodeIds.Count == 0;
    }
}
