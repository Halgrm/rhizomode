#nullable enable

using System;
using System.Collections.Generic;
using R3;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Query;
using Rhizomode.SharedKernel;
using Rhizomode.UI.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="GraphReadModel"/> + <see cref="GraphEventBus"/> を購読し、
    /// UI 側 ViewModel (<see cref="NodeViewModel"/> / <see cref="EdgeViewModel"/>) を構築する projector。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 5: UI.Presentation 層は <see cref="GraphState"/> を直接参照せず、本 projector が
    /// 構築した ViewModel のみを消費する。Phase 5 完了条件:
    /// <c>UI.Presentation</c> 配下から <c>Graph.*</c> 参照 0 件 → 本 projector が唯一の橋渡し。
    ///
    /// 現状 (Phase 5 partial): projector は ViewModel を構築できるが、UI.Presentation 側の
    /// migration はまだ。Phase 5 続きで NodeVisualController 等を ViewModel 消費に書き換える。
    /// </remarks>
    public sealed class GraphStateToViewModelProjector : IDisposable
    {
        private readonly GraphReadModel _readModel;
        private readonly GraphEventBus _eventBus;
        private readonly Subject<NodeViewModel> _onNodeAdded = new();
        private readonly Subject<string> _onNodeRemoved = new();
        private readonly Subject<EdgeViewModel> _onEdgeAdded = new();
        private readonly Subject<string> _onEdgeRemoved = new();
        private readonly Subject<GraphChangeSet> _onGraphChanged = new();

        private readonly CompositeDisposable _subscriptions = new();

        public Observable<NodeViewModel> OnNodeAdded => _onNodeAdded;
        public Observable<string> OnNodeRemoved => _onNodeRemoved;
        public Observable<EdgeViewModel> OnEdgeAdded => _onEdgeAdded;
        public Observable<string> OnEdgeRemoved => _onEdgeRemoved;
        public Observable<GraphChangeSet> OnGraphChanged => _onGraphChanged;

        public GraphStateToViewModelProjector(GraphReadModel readModel, GraphEventBus eventBus)
        {
            _readModel = readModel;
            _eventBus = eventBus;

            _subscriptions.Add(_eventBus.OnNodeAdded.Subscribe(OnNodeAddedFromBus));
            _subscriptions.Add(_eventBus.OnNodeRemoved.Subscribe(id => _onNodeRemoved.OnNext(id)));
            _subscriptions.Add(_eventBus.OnEdgeAdded.Subscribe(OnEdgeAddedFromBus));
            _subscriptions.Add(_eventBus.OnEdgeRemoved.Subscribe(id => _onEdgeRemoved.OnNext(id)));
            _subscriptions.Add(_eventBus.OnGraphChanged.Subscribe(cs => _onGraphChanged.OnNext(cs)));
        }

        /// <summary>現在の GraphState からノード全件の ViewModel を構築する (初期化用)。</summary>
        public IReadOnlyList<NodeViewModel> SnapshotNodes()
        {
            var list = new List<NodeViewModel>(_readModel.NodeCount);
            foreach (var node in _readModel.Nodes.Values)
            {
                list.Add(BuildNodeViewModel(node));
            }
            return list;
        }

        /// <summary>現在の GraphState からエッジ全件の ViewModel を構築する (初期化用)。</summary>
        public IReadOnlyList<EdgeViewModel> SnapshotEdges()
        {
            var list = new List<EdgeViewModel>(_readModel.EdgeCount);
            foreach (var edge in _readModel.Edges)
            {
                list.Add(BuildEdgeViewModel(edge));
            }
            return list;
        }

        private void OnNodeAddedFromBus(string nodeId)
        {
            var node = _readModel.GetNode(nodeId);
            if (node == null) return;
            _onNodeAdded.OnNext(BuildNodeViewModel(node));
        }

        private void OnEdgeAddedFromBus(string edgeId)
        {
            foreach (var edge in _readModel.Edges)
            {
                if (edge.Id == edgeId)
                {
                    _onEdgeAdded.OnNext(BuildEdgeViewModel(edge));
                    return;
                }
            }
        }

        private static NodeViewModel BuildNodeViewModel(NodeBase node)
        {
            var inputs = new List<PortViewModel>(node.InputPorts.Count);
            foreach (var kvp in node.InputPorts)
            {
                inputs.Add(new PortViewModel(kvp.Key, kvp.Value.Type, IsConnected: false));
            }
            var outputs = new List<PortViewModel>(node.OutputPorts.Count);
            foreach (var kvp in node.OutputPorts)
            {
                outputs.Add(new PortViewModel(kvp.Key, kvp.Value.Type, IsConnected: false));
            }
            var pos = new RzVector3(node.Position.x, node.Position.y, node.Position.z);
            return new NodeViewModel(
                NodeId: node.Id,
                TypeName: node.NodeType,
                Label: node.NodeType,
                Position: pos,
                InputPorts: inputs,
                OutputPorts: outputs);
        }

        private static EdgeViewModel BuildEdgeViewModel(Edge edge)
        {
            return new EdgeViewModel(
                edge.Id, edge.FromNodeId, edge.FromPort, edge.ToNodeId, edge.ToPort);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _onNodeAdded.Dispose();
            _onNodeRemoved.Dispose();
            _onEdgeAdded.Dispose();
            _onEdgeRemoved.Dispose();
            _onGraphChanged.Dispose();
        }
    }
}
