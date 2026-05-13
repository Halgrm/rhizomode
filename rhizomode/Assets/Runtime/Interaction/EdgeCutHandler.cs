#nullable enable

using System;
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
    /// エッジ切断を管理する。レイでエッジをハイライトし、右手Bで切断する。
    /// </summary>
    public class EdgeCutHandler : MonoBehaviour
    {
        private IRayProvider? _rayProvider;
        private EdgeVisualManager? _edgeVisualManager;
        private GraphContextBehaviour? _graphContext;
        private IIntentSink? _intentSink;
        private IDisposable? _cutSubscription;

        private string? _highlightedEdgeId;
        private bool _isEnabled = true;

        /// <summary>
        /// 外部からエッジ切断操作を有効/無効にする（メニューオープン中は無効化）。
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

        /// <summary>
        /// Plan v5.3 Phase 5: 空間操作 intent の発行先を注入する。
        /// GameBootstrap が SpatialIntentToCommandTranslator を渡す。
        /// </summary>
        public void SetIntentSink(IIntentSink intentSink) => _intentSink = intentSink;

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
            if (!_isEnabled) return;
            if (_highlightedEdgeId == null || _edgeVisualManager == null || _graphContext == null)
                return;

            var visuals = _edgeVisualManager.EdgeVisuals;
            if (!visuals.TryGetValue(_highlightedEdgeId, out var visual)) return;

            try
            {
                // Plan v5.3 Phase 5: GraphState.Disconnect 直接呼び出しを intent emit に置換。
                // Translator が DisconnectEdgeCommand を Origin=Interaction で発行 → Dispatcher 経由で適用。
                _intentSink?.Emit(new DisconnectEdgeIntent(_highlightedEdgeId));
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
