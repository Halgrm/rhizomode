#nullable enable

using System;
using System.Linq;
using R3;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// ノード削除を管理する。レイでノードを選択し、右手Aで即削除する。
    /// </summary>
    public class NodeDeleteHandler : MonoBehaviour
    {
        private const float RayMaxDistance = 5f;

        private IRayProvider? _rayProvider;
        private NodeVisualManager? _visualManager;
        private GraphContextBehaviour? _graphContext;
        private EdgeVisualManager? _edgeVisualManager;
        private IDisposable? _deleteSubscription;

        private string? _selectedNodeId;

        /// <summary>
        /// 依存関係を設定し、入力を購読する。
        /// </summary>
        public void Initialize(
            IControllerInput controllerInput,
            IRayProvider rayProvider,
            NodeVisualManager visualManager,
            GraphContextBehaviour graphContext,
            EdgeVisualManager edgeVisualManager)
        {
            _rayProvider = rayProvider;
            _visualManager = visualManager;
            _graphContext = graphContext;
            _edgeVisualManager = edgeVisualManager;

            _deleteSubscription = controllerInput.OnDeleteNode
                .Subscribe(_ => DeleteSelectedNode());
        }

        private void Update()
        {
            if (_rayProvider == null) return;

            var ray = new Ray(_rayProvider.RayOrigin, _rayProvider.RayDirection);
            if (Physics.Raycast(ray, out var hit, RayMaxDistance))
            {
                var visual = hit.collider.GetComponent<NodeVisualController>();
                _selectedNodeId = visual?.Node?.Id;
            }
            else
            {
                _selectedNodeId = null;
            }
        }

        private void DeleteSelectedNode()
        {
            if (_selectedNodeId == null || _graphContext == null ||
                _visualManager == null || _edgeVisualManager == null)
                return;

            var nodeId = _selectedNodeId;

            try
            {
                // 関連エッジVisualを先に削除
                var relatedEdges = _graphContext.Context.Edges
                    .Where(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId)
                    .Select(e => e.Id)
                    .ToList();

                foreach (var edgeId in relatedEdges)
                {
                    _edgeVisualManager.DestroyEdgeVisual(edgeId);
                }

                // GraphContextからノード削除（エッジSubscription破棄含む）
                _graphContext.Context.RemoveNode(nodeId);

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
