#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.Model;

namespace Rhizomode.Graph.Query
{
    /// <summary>
    /// グラフの読み取り専用 projection。
    /// </summary>
    /// <remarks>
    /// Plan v5.3: UI / Audio / Module 等の subscriber 側が <see cref="GraphState"/> を直接触らず、
    /// この read model + <see cref="Events.GraphEventBus"/> 経由で id ベース通知を購読する。
    ///
    /// Phase 2 では <see cref="GraphState"/> を単純にラップする。Phase 5 で
    /// GraphStateToViewModelProjector が本 read model を消費して UI ViewModel を構築する。
    /// </remarks>
    public sealed class GraphReadModel
    {
        private readonly GraphState _state;

        public GraphReadModel(GraphState state)
        {
            _state = state;
        }

        public IReadOnlyDictionary<string, NodeBase> Nodes => _state.Nodes;
        public IReadOnlyList<Edge> Edges => _state.Edges;

        public NodeBase? GetNode(string nodeId) =>
            _state.Nodes.TryGetValue(nodeId, out var n) ? n : null;

        public bool ContainsNode(string nodeId) => _state.Nodes.ContainsKey(nodeId);

        public int NodeCount => _state.Nodes.Count;
        public int EdgeCount => _state.Edges.Count;
    }
}
