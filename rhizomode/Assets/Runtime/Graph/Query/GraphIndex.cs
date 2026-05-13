#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.Model;

namespace Rhizomode.Graph.Query
{
    /// <summary>
    /// グラフのノード隣接索引 (read model 用)。
    /// </summary>
    /// <remarks>
    /// Plan v5.3: subscriber 側が「ノード id からその incoming/outgoing エッジ列挙」を
    /// O(1) で取得するための索引。<see cref="EdgeIndex"/> を委譲する単純なラッパ。
    ///
    /// Phase 2 ではミニマル実装。Phase 5 で UI/Module/Audio Adapter から消費される。
    /// </remarks>
    public sealed class GraphIndex
    {
        private readonly EdgeIndex _edgeIndex;

        public GraphIndex(EdgeIndex edgeIndex)
        {
            _edgeIndex = edgeIndex;
        }

        public IReadOnlyCollection<string> OutgoingEdgeIds(string nodeId) =>
            _edgeIndex.OutgoingEdgeIds(nodeId);

        public IReadOnlyCollection<string> IncomingEdgeIds(string nodeId) =>
            _edgeIndex.IncomingEdgeIds(nodeId);

        public Edge? GetEdge(string edgeId) => _edgeIndex.GetById(edgeId);
    }
}
