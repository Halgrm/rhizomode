#nullable enable

using System;
using System.Collections.Generic;

namespace Rhizomode.Graph.Model
{
    /// <summary>
    /// エッジの集合を、id・endpoint・ノード隣接で索引する。
    /// </summary>
    /// <remarks>
    /// Phase 2 で導入。Plan v5.3 の <c>List&lt;Edge&gt; → EdgeIndex</c> 移行の中核。
    /// O(1) lookup を提供:
    /// - id → Edge
    /// - (from, fromPort, to, toPort) → Edge (重複検出用)
    /// - nodeId → 隣接エッジ列挙
    ///
    /// 内部状態は <see cref="GraphState"/> から委譲されるが、Phase 8 で
    /// GraphState の List ベース API ([Obsolete]) を削除後は唯一のソースとなる。
    /// </remarks>
    public sealed class EdgeIndex
    {
        private readonly Dictionary<string, Edge> _byId = new();
        private readonly Dictionary<EndpointKey, Edge> _byEndpoint = new();
        private readonly Dictionary<string, HashSet<string>> _outgoingByNode = new();
        private readonly Dictionary<string, HashSet<string>> _incomingByNode = new();
        private readonly List<Edge> _orderedEdges = new();

        public int Count => _orderedEdges.Count;

        public IReadOnlyList<Edge> Edges => _orderedEdges;

        public Edge? GetById(string edgeId) =>
            _byId.TryGetValue(edgeId, out var e) ? e : null;

        public bool ContainsEndpoint(string fromNodeId, string fromPort, string toNodeId, string toPort) =>
            _byEndpoint.ContainsKey(new EndpointKey(fromNodeId, fromPort, toNodeId, toPort));

        public Edge? GetByEndpoint(string fromNodeId, string fromPort, string toNodeId, string toPort) =>
            _byEndpoint.TryGetValue(new EndpointKey(fromNodeId, fromPort, toNodeId, toPort), out var e)
                ? e : null;

        public IReadOnlyCollection<string> OutgoingEdgeIds(string nodeId) =>
            _outgoingByNode.TryGetValue(nodeId, out var set)
                ? (IReadOnlyCollection<string>)set
                : Array.Empty<string>();

        public IReadOnlyCollection<string> IncomingEdgeIds(string nodeId) =>
            _incomingByNode.TryGetValue(nodeId, out var set)
                ? (IReadOnlyCollection<string>)set
                : Array.Empty<string>();

        public bool Add(Edge edge)
        {
            var key = new EndpointKey(edge.FromNodeId, edge.FromPort, edge.ToNodeId, edge.ToPort);
            if (_byEndpoint.ContainsKey(key) || _byId.ContainsKey(edge.Id)) return false;

            _byId[edge.Id] = edge;
            _byEndpoint[key] = edge;
            GetOrCreate(_outgoingByNode, edge.FromNodeId).Add(edge.Id);
            GetOrCreate(_incomingByNode, edge.ToNodeId).Add(edge.Id);
            _orderedEdges.Add(edge);
            return true;
        }

        public bool RemoveById(string edgeId)
        {
            if (!_byId.TryGetValue(edgeId, out var edge)) return false;
            var key = new EndpointKey(edge.FromNodeId, edge.FromPort, edge.ToNodeId, edge.ToPort);
            _byId.Remove(edgeId);
            _byEndpoint.Remove(key);
            if (_outgoingByNode.TryGetValue(edge.FromNodeId, out var outSet)) outSet.Remove(edgeId);
            if (_incomingByNode.TryGetValue(edge.ToNodeId, out var inSet)) inSet.Remove(edgeId);
            _orderedEdges.Remove(edge);
            return true;
        }

        public void Clear()
        {
            _byId.Clear();
            _byEndpoint.Clear();
            _outgoingByNode.Clear();
            _incomingByNode.Clear();
            _orderedEdges.Clear();
        }

        private static HashSet<string> GetOrCreate(Dictionary<string, HashSet<string>> dict, string key)
        {
            if (!dict.TryGetValue(key, out var set))
            {
                set = new HashSet<string>();
                dict[key] = set;
            }
            return set;
        }

        private readonly record struct EndpointKey(
            string FromNodeId, string FromPort, string ToNodeId, string ToPort);
    }
}
