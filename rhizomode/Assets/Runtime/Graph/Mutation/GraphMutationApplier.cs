#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Snapshot;
using Rhizomode.SharedKernel;
using UnityEngine;

namespace Rhizomode.Graph.Mutation
{
    /// <summary>
    /// <see cref="IGraphCommand"/> を <see cref="GraphState"/> に適用する executor。
    /// Snapshot 取得・GraphState 操作・EventBus 発火を一手に引き受ける。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 8: Dispatcher が本 applier を経由してのみ mutation を実行する。
    /// GraphState の mutation メソッドは internal 化済 — Graph.Mutation は InternalsVisibleTo で
    /// 許可された正規 consumer。
    ///
    /// 各 command の適用は <see cref="Apply"/> で switch される。失敗時は何もしない (Plan v5.3
    /// "Video must never stop" 原則: 受信側で再 fetch すれば最新が分かる)。
    ///
    /// <see cref="RestoreFromSnapshot"/> は <see cref="GraphCommandDispatcher"/> の Undo/Redo から呼ばれる。
    /// </remarks>
    public sealed class GraphMutationApplier
    {
        private readonly GraphState _state;
        private readonly INodeFactory _factory;
        private readonly GraphEventBus _bus;

        public GraphMutationApplier(GraphState state, INodeFactory factory, GraphEventBus bus)
        {
            _state = state;
            _factory = factory;
            _bus = bus;
        }

        /// <summary>現在の GraphState から canonical <see cref="GraphSnapshot"/> を構築する。</summary>
        public GraphSnapshot CaptureSnapshot()
        {
            var nodes = new List<NodeSnapshot>(_state.Nodes.Count);
            foreach (var node in _state.Nodes.Values)
            {
                var pos = new RzVector3(node.Position.x, node.Position.y, node.Position.z);
                var paramValues = CaptureNodeParams(node);
                nodes.Add(new NodeSnapshot(node.Id, node.NodeType, pos, paramValues));
            }

            var edges = new List<EdgeSnapshot>(_state.Edges.Count);
            foreach (var edge in _state.Edges)
            {
                edges.Add(new EdgeSnapshot(edge.Id, edge.FromNodeId, edge.FromPort, edge.ToNodeId, edge.ToPort));
            }

            return new GraphSnapshot(nodes, edges);
        }

        /// <summary>
        /// command を解釈して <see cref="GraphState"/> に適用する (失敗を呼び出し側に通知しない void 版)。
        /// </summary>
        /// <remarks>
        /// F-Vf-d.2 で <see cref="TryApply"/> へ delegate する形に変更。失敗判定が必要な caller は
        /// <see cref="TryApply"/> を直接呼ぶか、<see cref="GraphMutationScope"/> 経由で atomic 単位を作る。
        /// </remarks>
        public void Apply(IGraphCommand command) => TryApply(command);

        /// <summary>
        /// command を解釈して <see cref="GraphState"/> に適用する。成功時 true、失敗時 false。
        /// </summary>
        /// <remarks>
        /// F-Vf-d.2 (Codex review #3 NON_ATOMIC_MULTI_DISPATCH): <see cref="GraphMutationScope"/> が
        /// 連投 dispatch を atomic に扱うため、各 Apply の成否を返す必要があった。Phase 8: GraphState
        /// の mutation メソッド (RegisterNode/RemoveNode/TryConnect/Disconnect/Clear) は internal 化。
        /// Graph.Mutation は InternalsVisibleTo で許可された正規 consumer。
        /// </remarks>
        public bool TryApply(IGraphCommand command)
        {
            switch (command)
            {
                case AddNodeCommand add: return TryApplyAdd(add);
                case RemoveNodeCommand remove: return TryApplyRemove(remove);
                case ConnectPortsCommand connect: return TryApplyConnect(connect);
                case DisconnectEdgeCommand disconnect: return TryApplyDisconnect(disconnect);
                case MoveNodeCommand move: return TryApplyMove(move);
                case SetNodeParamCommand setParam: return TryApplySetParam(setParam);
                case LoadGraphCommand load: RestoreFromSnapshot(load.Snapshot); return true;
                case CompositeCommand: return true;
                default:
                    Debug.LogWarning(
                        $"[GraphMutationApplier] Unhandled command type: {command.GetType().Name}. " +
                        "Add a case in TryApply() or remove the command from IGraphCommand hierarchy.");
                    return false;
            }
        }

        private bool TryApplyAdd(AddNodeCommand cmd)
        {
            if (!_factory.CanCreate(cmd.TypeName))
            {
                Debug.LogWarning($"[GraphMutationApplier] Unknown typeName: {cmd.TypeName}");
                return false;
            }
            var node = _factory.Create(cmd.TypeName, cmd.NodeId);
            if (node == null) return false;
            node.Position = new Vector3(cmd.Position.X, cmd.Position.Y, cmd.Position.Z);
            _state.RegisterNode(node);
            _bus.EmitNodeAdded(cmd.NodeId);
            return true;
        }

        private bool TryApplyRemove(RemoveNodeCommand cmd)
        {
            if (!_state.Nodes.ContainsKey(cmd.NodeId)) return false;
            _state.RemoveNode(cmd.NodeId);
            _bus.EmitNodeRemoved(cmd.NodeId);
            return true;
        }

        private bool TryApplyConnect(ConnectPortsCommand cmd)
        {
            // Phase 8 Codex Axis A fix (re-review): cmd.EdgeId を実 edge.Id として保持。
            // 旧コードは TryConnect が新 GUID を生成しつつ bus.EmitEdgeAdded(cmd.EdgeId) で異なる id を
            // 通知していたため、projector / Undo / Snapshot の edge identity 不整合が発生していた。
            if (_state.TryConnect(cmd.FromNodeId, cmd.FromPortName, cmd.ToNodeId, cmd.ToPortName, cmd.EdgeId))
            {
                _bus.EmitEdgeAdded(cmd.EdgeId);
                return true;
            }
            return false;
        }

        private bool TryApplyDisconnect(DisconnectEdgeCommand cmd)
        {
            Edge? target = null;
            foreach (var e in _state.Edges)
            {
                if (e.Id == cmd.EdgeId) { target = e; break; }
            }
            if (target == null) return false;
            _state.Disconnect(target.FromNodeId, target.FromPort, target.ToNodeId, target.ToPort);
            _bus.EmitEdgeRemoved(cmd.EdgeId);
            return true;
        }

        private bool TryApplyMove(MoveNodeCommand cmd)
        {
            if (!_state.Nodes.TryGetValue(cmd.NodeId, out var node)) return false;
            node.Position = new Vector3(cmd.NewPosition.X, cmd.NewPosition.Y, cmd.NewPosition.Z);
            return true;
        }

        private bool TryApplySetParam(SetNodeParamCommand cmd)
        {
            if (!_state.Nodes.TryGetValue(cmd.NodeId, out var node)) return false;
            if (node is INodeParamAccessor accessor)
            {
                accessor.TrySetParam(cmd.ParamName, cmd.Value);
                // Note (Codex review): individual param change events are intentionally NOT emitted here.
                // High-frequency callers (LFO / Ableton macros) would allocate per call. Subscribers that
                // care about param drift should bind to the node's port chain instead, or batch via
                // GraphMutationScope when applying many param commands at once.
                return true;
            }
            return false;
        }

        /// <summary>
        /// Snapshot から GraphState を完全復元する (Undo/Redo + LoadGraph で使用)。
        /// </summary>
        public void RestoreFromSnapshot(GraphSnapshot snapshot)
        {
            _state.Clear();

            foreach (var nodeSnap in snapshot.Nodes)
            {
                if (!_factory.CanCreate(nodeSnap.TypeName)) continue;
                var node = _factory.Create(nodeSnap.TypeName, nodeSnap.NodeId);
                if (node == null) continue;
                node.Position = new Vector3(nodeSnap.Position.X, nodeSnap.Position.Y, nodeSnap.Position.Z);
                if (node is INodeParamAccessor accessor)
                {
                    foreach (var kvp in nodeSnap.ParamValues)
                    {
                        accessor.TrySetParam(kvp.Key, kvp.Value);
                    }
                }
                _state.RegisterNode(node);
            }

            foreach (var edgeSnap in snapshot.Edges)
            {
                // Phase 8 Codex Axis A fix (re-review): Undo/Redo の Snapshot 復元時も edge id を保持。
                _state.TryConnect(edgeSnap.FromNodeId, edgeSnap.FromPortName,
                                  edgeSnap.ToNodeId, edgeSnap.ToPortName, edgeSnap.EdgeId);
            }
        }

        private static IReadOnlyDictionary<string, ParamValue> CaptureNodeParams(NodeBase node)
        {
            if (node is not INodeParamAccessor accessor) return EmptyParams;

            // Phase 2 段階では各 ParamDefinition の列挙手段が無いので空辞書を返す。
            // Phase 4 ([NodeType] + INodeTypeProvider 経由でパラメータメタデータ取得) で実装する。
            return EmptyParams;
        }

        private static readonly IReadOnlyDictionary<string, ParamValue> EmptyParams =
            new Dictionary<string, ParamValue>();
    }
}
