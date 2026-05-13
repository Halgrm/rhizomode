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
    /// VContainer で DI 注入する。
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
        /// Deserialize/Preset Import 完了通知。全 processor の AfterDeserialize を呼ぶ。
        /// </summary>
        public void NotifyAfterDeserialize()
        {
            foreach (var p in _processors)
            {
                SafeInvoke(() => p.AfterDeserialize(_state), "AfterDeserialize");
            }
        }

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
