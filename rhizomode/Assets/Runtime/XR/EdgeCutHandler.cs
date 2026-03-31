#nullable enable

using System;
using System.Linq;
using R3;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// エッジ切断を管理する。レイでエッジをハイライトし、右手Bで切断する。
    /// </summary>
    public class EdgeCutHandler : MonoBehaviour
    {
        private IRayProvider? _rayProvider;
        private EdgeVisualManager? _edgeVisualManager;
        private GraphContextBehaviour? _graphContext;
        private IDisposable? _cutSubscription;

        private string? _highlightedEdgeId;

        /// <summary>
        /// 依存関係を設定し、入力を購読する。
        /// </summary>
        public void Initialize(
            IControllerInput controllerInput,
            IRayProvider rayProvider,
            EdgeVisualManager edgeVisualManager,
            GraphContextBehaviour graphContext)
        {
            _rayProvider = rayProvider;
            _edgeVisualManager = edgeVisualManager;
            _graphContext = graphContext;

            _cutSubscription = controllerInput.OnCutEdge
                .Subscribe(_ => CutHighlightedEdge());
        }

        private void Update()
        {
            if (_rayProvider == null || _edgeVisualManager == null) return;

            var nearId = _edgeVisualManager.GetEdgeIdNearRay(
                _rayProvider.RayOrigin, _rayProvider.RayDirection);

            if (nearId != _highlightedEdgeId)
            {
                if (_highlightedEdgeId != null)
                    _edgeVisualManager.SetHighlight(_highlightedEdgeId, false);

                _highlightedEdgeId = nearId;

                if (_highlightedEdgeId != null)
                    _edgeVisualManager.SetHighlight(_highlightedEdgeId, true);
            }
        }

        private void CutHighlightedEdge()
        {
            if (_highlightedEdgeId == null || _edgeVisualManager == null || _graphContext == null)
                return;

            var visuals = _edgeVisualManager.EdgeVisuals;
            if (!visuals.TryGetValue(_highlightedEdgeId, out var visual)) return;

            try
            {
                _graphContext.Context.Disconnect(
                    visual.FromNodeId, visual.FromPort,
                    visual.ToNodeId, visual.ToPort);
                _edgeVisualManager.DestroyEdgeVisual(_highlightedEdgeId);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EdgeCutHandler] Cut failed: {e.Message}");
            }

            _highlightedEdgeId = null;
        }

        private void OnDestroy()
        {
            _cutSubscription?.Dispose();
        }
    }
}
