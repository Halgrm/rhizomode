#nullable enable

using System;
using System.Collections.Generic;
using R3;
using Rhizomode.Interaction.Contracts;
using Rhizomode.UI;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.XR
{
    /// <summary>
    /// ノード削除を管理する。レイでノードを選択し、右手Aで即削除する。
    /// </summary>
    public class NodeDeleteHandler : MonoBehaviour
    {
        private SharedRaycastService? _sharedRaycast;
        private NodeVisualManager? _visualManager;
        private GraphContextBehaviour? _graphContext;
        private EdgeVisualManager? _edgeVisualManager;
        private EdgeDragHandler? _edgeDragHandler;
        private Action<string>? _destroyModuleAction;
        private IIntentSink? _intentSink;
        private IDisposable? _deleteSubscription;

        private string? _selectedNodeId;
        private readonly List<string> _edgeIdBuffer = new();
        private bool _isEnabled = true;

        /// <summary>
        /// 外部からノード削除操作を有効/無効にする（メニューオープン中は無効化）。
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
        }

        /// <summary>
        /// 依存関係を設定し、入力を購読する。
        /// </summary>
        public void Initialize(
            IControllerInput controllerInput,
            SharedRaycastService sharedRaycast,
            NodeVisualManager visualManager,
            GraphContextBehaviour graphContext,
            EdgeVisualManager edgeVisualManager)
        {
            _sharedRaycast = sharedRaycast;
            _visualManager = visualManager;
            _graphContext = graphContext;
            _edgeVisualManager = edgeVisualManager;

            _deleteSubscription = controllerInput.OnDeleteNode
                .Subscribe(_ => DeleteSelectedNode());
        }

        /// <summary>
        /// EdgeDragHandlerとモジュール破棄コールバックを設定する（ノード削除時の連携用）。
        /// </summary>
        public void SetDeleteDependencies(EdgeDragHandler? edgeDragHandler, Action<string>? destroyModuleAction)
        {
            _edgeDragHandler = edgeDragHandler;
            _destroyModuleAction = destroyModuleAction;
        }

        /// <summary>Plan v5.3 Phase 5: 空間操作 intent の発行先を注入する。</summary>
        public void SetIntentSink(IIntentSink intentSink) => _intentSink = intentSink;

        private void Update()
        {
            if (_sharedRaycast == null || _visualManager == null) return;

            if (_sharedRaycast.HasHit)
            {
                var visual = _visualManager.GetVisualByCollider(_sharedRaycast.CurrentHit.collider);
                _selectedNodeId = visual?.Node?.NodeId;
            }
            else
            {
                _selectedNodeId = null;
            }
        }

        private void DeleteSelectedNode()
        {
            if (!_isEnabled)
            {
                Debug.LogWarning("[NodeDeleteHandler] Delete blocked: disabled (menu open?)");
                return;
            }
            if (_selectedNodeId == null)
            {
                Debug.LogWarning("[NodeDeleteHandler] Delete blocked: no node selected (ray not hitting any node)");
                return;
            }
            if (_graphContext == null || _visualManager == null || _edgeVisualManager == null)
            {
                Debug.LogWarning("[NodeDeleteHandler] Delete blocked: missing dependencies");
                return;
            }

            var nodeId = _selectedNodeId;

            // 削除前に再検証（別ハンドラが先に削除した可能性）
            if (!_graphContext.Context.Nodes.ContainsKey(nodeId))
            {
                _selectedNodeId = null;
                return;
            }

            try
            {
                // EdgeDragHandlerが削除対象ノードを参照中ならリセット
                _edgeDragHandler?.CancelIfInvolves(nodeId);

                // 関連エッジを収集（visual 削除は intent 成功後）。LINQ不使用、GC allocなし。
                _edgeIdBuffer.Clear();
                foreach (var e in _graphContext.Context.Edges)
                {
                    if (e.FromNodeId == nodeId || e.ToNodeId == nodeId)
                        _edgeIdBuffer.Add(e.Id);
                }

                // Plan v5.3 Phase 5: GraphState.RemoveNode 直接呼び出しを intent emit に置換。
                // Translator が RemoveNodeCommand (Origin=Interaction) を Dispatcher 経由で実行。
                // Codex review fix: Emit 失敗時は visual/module cleanup をスキップして orphan を防ぐ。
                var emitted = _intentSink?.Emit(new DeleteNodeIntent(nodeId)) ?? false;
                if (!emitted)
                {
                    Debug.LogWarning($"[NodeDeleteHandler] Delete intent rejected: {nodeId}");
                    _selectedNodeId = null;
                    return;
                }

                // intent 成功後に visual/module を順次破棄
                foreach (var edgeId in _edgeIdBuffer)
                {
                    _edgeVisualManager.DestroyEdgeVisual(edgeId);
                }

                // モジュールPrefabインスタンスの破棄（リーク防止）
                _destroyModuleAction?.Invoke(nodeId);

                // ノードVisual破棄
                _visualManager.DestroyNodeVisual(nodeId);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NodeDeleteHandler] Delete failed: {nodeId} — {e.Message}");
            }

            _selectedNodeId = null;
        }

        private void OnDestroy()
        {
            _deleteSubscription?.Dispose();
        }
    }
}
