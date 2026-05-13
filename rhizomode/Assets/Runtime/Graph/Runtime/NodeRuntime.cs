#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Model;

namespace Rhizomode.Graph.Runtime
{
    /// <summary>
    /// ノードの BeforeSetup → Setup → AfterSetup を統括する orchestrator。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 6: <see cref="GraphState.RegisterNode"/> の代わりに使われ、
    /// 全 <see cref="INodeLifecycleProcessor"/> を順序保証付きで実行する。
    ///
    /// Phase 2 ではスケルトン (空 processor リストで動く)。Phase 6 で各 system processor を
    /// VContainer で DI 注入する。Phase 6 Round C で <see cref="AddEdge"/> +
    /// <see cref="EmitGraphChanged"/> 拡張、<see cref="HydrationPlanExecutor"/> から駆動可能に。
    /// </remarks>
    public sealed class NodeRuntime
    {
        private readonly GraphState _state;
        private readonly GraphEventBus _eventBus;
        private readonly IReadOnlyList<INodeLifecycleProcessor> _processors;

        public NodeRuntime(
            GraphState state,
            GraphEventBus eventBus,
            IReadOnlyList<INodeLifecycleProcessor>? processors = null)
        {
            _state = state;
            _eventBus = eventBus;
            _processors = processors ?? Array.Empty<INodeLifecycleProcessor>();
        }

        /// <summary>
        /// ノードを登録し、フルライフサイクルを実行する。
        /// </summary>
        public void RegisterNode(NodeBase node, NodeInitMode mode)
        {
            foreach (var p in _processors)
            {
                SafeInvoke(() => p.BeforeSetup(node, mode), $"BeforeSetup {node.Id}");
            }

            _state.RegisterNode(node);

            foreach (var p in _processors)
            {
                SafeInvoke(() => p.AfterSetup(node, mode), $"AfterSetup {node.Id}");
            }

            _eventBus.EmitNodeAdded(node.Id);
        }

        /// <summary>
        /// エッジを追加する。型不一致・port 未発見・重複の場合 false を返す。
        /// </summary>
        /// <remarks>
        /// Plan v5.3 Phase 6 Round C: <see cref="HydrationPlanExecutor"/> から呼ばれる。
        /// 現状は <see cref="GraphState.TryConnect"/> (legacy) を経由 — edge ID は自動生成 GUID。
        /// Phase 7 で GraphState に id 指定可能 API を追加し、ここで supplied <paramref name="edgeId"/>
        /// を honor する設計に切替予定。それまでは supplied id は <see cref="GraphEventBus.EmitEdgeAdded"/>
        /// の通知 id としてのみ使われる (Snapshot/Undo 整合性のため呼び出し側は元 id を保持)。
        /// </remarks>
        public bool AddEdge(string edgeId, string fromNode, string fromPort, string toNode, string toPort)
        {
#pragma warning disable CS0618
            var success = _state.TryConnect(fromNode, fromPort, toNode, toPort);
#pragma warning restore CS0618
            if (success)
            {
                _eventBus.EmitEdgeAdded(edgeId);
            }
            return success;
        }

        /// <summary>
        /// Deserialize/Preset Import 完了通知。全 processor の AfterDeserialize を呼ぶ。
        /// </summary>
        public void NotifyAfterDeserialize()
        {
            foreach (var p in _processors)
            {
                SafeInvoke(() => p.AfterDeserialize(_state), "AfterDeserialize");
            }
        }

        /// <summary>
        /// <see cref="GraphEventBus.OnGraphChanged"/> に <see cref="GraphChangeSet"/> を発火する。
        /// </summary>
        public void EmitGraphChanged(GraphChangeSet changeSet) => _eventBus.EmitGraphChanged(changeSet);

        /// <summary>現在の <see cref="GraphState"/> (テスト/HydrationPlanExecutor 用)。</summary>
        internal GraphState State => _state;

        /// <summary>現在の <see cref="GraphEventBus"/> (HydrationPlanExecutor のスコープ用)。</summary>
        internal GraphEventBus EventBus => _eventBus;

        private static void SafeInvoke(Action a, string label)
        {
            try { a(); }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[NodeRuntime] {label} failed — {e.Message}");
            }
        }
    }
}
