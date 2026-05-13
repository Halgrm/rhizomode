#nullable enable

using System;
using System.Collections.Generic;

namespace Rhizomode.Graph.Events
{
    /// <summary>
    /// 複数の mutation を 1 つの <see cref="GraphChangeSet"/> にまとめるためのスコープ。
    /// </summary>
    /// <remarks>
    /// Plan v5.3: Deserialize/PresetImport 等の bulk 操作で、個別の OnNodeAdded 等を抑制し、
    /// Dispose 時に <see cref="GraphEventBus.OnGraphChanged"/> で一括通知する。
    ///
    /// 使用例:
    /// <code>
    /// using (var scope = new GraphMutationScope(eventBus))
    /// {
    ///     scope.RecordNodeAdded(nodeA.Id);
    ///     scope.RecordNodeAdded(nodeB.Id);
    ///     scope.RecordEdgeAdded(edge.Id);
    /// } // ここで OnGraphChanged が 1 回発火
    /// </code>
    /// </remarks>
    public sealed class GraphMutationScope : IDisposable
    {
        private readonly GraphEventBus _bus;
        private readonly List<string> _addedNodes = new();
        private readonly List<string> _removedNodes = new();
        private readonly List<string> _addedEdges = new();
        private readonly List<string> _removedEdges = new();
        private readonly List<string> _changedParamNodes = new();
        private bool _disposed;

        public GraphMutationScope(GraphEventBus bus)
        {
            _bus = bus;
        }

        public void RecordNodeAdded(string nodeId) => _addedNodes.Add(nodeId);
        public void RecordNodeRemoved(string nodeId) => _removedNodes.Add(nodeId);
        public void RecordEdgeAdded(string edgeId) => _addedEdges.Add(edgeId);
        public void RecordEdgeRemoved(string edgeId) => _removedEdges.Add(edgeId);
        public void RecordParamChanged(string nodeId) => _changedParamNodes.Add(nodeId);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var changeSet = new GraphChangeSet(
                _addedNodes,
                _removedNodes,
                _addedEdges,
                _removedEdges,
                _changedParamNodes);

            if (changeSet.IsEmpty) return;
            _bus.EmitGraphChanged(changeSet);
        }
    }
}
