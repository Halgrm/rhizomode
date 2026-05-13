#nullable enable

using System;
using R3;
using Rhizomode.Interaction.Contracts;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.UI.Contracts;
using UnityEngine;

using Rhizomode.Input.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// ステートマシン方式のエッジ接続ハンドラ。Rectorのパターンを踏襲し、
    /// トリガー2回クリックで接続を完了する。ドラッグ不要。
    /// Idle → SourceSelected（プレビューライン表示）→ 接続 or キャンセル → Idle
    /// </summary>
    /// <remarks>
    /// Phase 9 Round D で partial class に分割:
    /// - <c>EdgeDragHandler.cs</c> (本ファイル): 公開 API / Initialize / Update / state machine entry / OnDestroy / フィールド
    /// - <c>EdgeDragHandler.PortSelection.cs</c>: 出力/入力ポートの最近傍探索
    /// - <c>EdgeDragHandler.Preview.cs</c>: プレビューライン (LineRenderer) の生成 / 破棄 / 更新
    /// - <c>EdgeDragHandler.Highlight.cs</c>: ハイライトの状態管理
    /// </remarks>
    public partial class EdgeDragHandler : MonoBehaviour
    {
        [Header("エッジ接続設定")]
        [SerializeField, Range(0.001f, 0.01f), Tooltip("プレビューラインの幅（メートル）")]
        private float previewLineWidth = 0.003f;

        [SerializeField, Range(0.5f, 5f), Tooltip("プレビューラインの最大長さ（メートル）")]
        private float defaultRayEndDistance = 2f;

        [SerializeField, Range(0.01f, 0.15f), Tooltip("ポート自動選択の距離閾値（メートル）")]
        private float portSelectThreshold = 0.04f;

        private static Shader? CachedPreviewShader;

        private IRayProvider? _rayProvider;
        private IControllerInput? _controllerInput;
        private NodeVisualManager? _visualManager;
        private GraphContextBehaviour? _graphContext;
        private EdgeVisualManager? _edgeVisualManager;
        private SharedRaycastService? _sharedRaycast;
        private IIntentSink? _intentSink;
        private IDisposable? _selectSubscription;

        // メニューオープン中やグラブ中など外部から無効化する用
        private bool _isEnabled = true;
        private Func<bool>? _isGrabbingCheck;

        // 状態
        private EdgeConnectionState _state = EdgeConnectionState.Idle;
        private string? _sourceNodeId;
        private string? _sourcePortName;
        private ParamType _sourceParamType;

        // プレビューライン
        private LineRenderer? _previewLine;
        private GameObject? _previewGo;
        private Material? _previewMaterial;

        // ハイライト中のポート
        private string? _highlightedNodeId;
        private string? _highlightedPortName;

        /// <summary>現在の接続状態。</summary>
        public EdgeConnectionState State => _state;

        /// <summary>
        /// 外部からエッジ接続操作を有効/無効にする（メニューオープン中は無効化）。
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;

            // 無効化時にソース選択中なら状態リセット
            if (!enabled && _state != EdgeConnectionState.Idle)
                ResetState();
        }

        /// <summary>
        /// グラブ中かどうかの判定関数を設定する。グラブ中はエッジ接続操作をスキップ。
        /// </summary>
        public void SetGrabbingCheck(Func<bool> isGrabbing)
        {
            _isGrabbingCheck = isGrabbing;
        }

        /// <summary>
        /// 指定ノードがソース選択中ならリセットする。ノード削除時の連携用。
        /// </summary>
        public void CancelIfInvolves(string nodeId)
        {
            if (_state != EdgeConnectionState.Idle && _sourceNodeId == nodeId)
                ResetState();
        }

        /// <summary>
        /// 依存関係を設定し、入力を購読する。
        /// </summary>
        public void Initialize(
            IRayProvider rayProvider,
            IControllerInput controllerInput,
            NodeVisualManager visualManager,
            GraphContextBehaviour graphContext,
            EdgeVisualManager edgeVisualManager,
            SharedRaycastService sharedRaycast)
        {
            _rayProvider = rayProvider;
            _controllerInput = controllerInput;
            _visualManager = visualManager;
            _graphContext = graphContext;
            _edgeVisualManager = edgeVisualManager;
            _sharedRaycast = sharedRaycast;

            _selectSubscription = controllerInput.OnSelect
                .Where(pressed => pressed)
                .Subscribe(_ => OnTriggerPressed());
        }

        /// <summary>
        /// Plan v5.3 Phase 5: 空間操作 intent の発行先を注入する。
        /// </summary>
        public void SetIntentSink(IIntentSink intentSink) => _intentSink = intentSink;

        private void Update()
        {
            if (_state != EdgeConnectionState.SourceSelected) return;
            if (_rayProvider == null || _visualManager == null) return;

            UpdatePreviewLine();
            UpdateSnapHighlight();
        }

        private void OnTriggerPressed()
        {
            // メニューオープン中・グラブ中など外部から無効化されていればスキップ
            if (!_isEnabled) return;
            if (_isGrabbingCheck?.Invoke() == true) return;

            try
            {
                switch (_state)
                {
                    case EdgeConnectionState.Idle:
                        TrySelectSource();
                        break;
                    case EdgeConnectionState.SourceSelected:
                        TrySelectTargetOrCancel();
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EdgeDragHandler] Exception during trigger: {e.Message}");
                ResetState();
            }
        }

        /// <summary>
        /// ソースノードの出力ポートを選択する。
        /// </summary>
        private void TrySelectSource()
        {
            if (_sharedRaycast == null || _visualManager == null) return;
            if (!_sharedRaycast.HasHit) return;

            var hit = _sharedRaycast.CurrentHit;
            var nodeVisual = _visualManager.GetVisualByCollider(hit.collider);
            if (nodeVisual?.Node == null) return;

            // 出力ポートを持っているか確認し、最も近いものを自動選択
            var (portName, paramType) = FindNearestOutputPort(nodeVisual, hit.point);
            if (portName == null) return;

            _sourceNodeId = nodeVisual.Node.Id;
            _sourcePortName = portName;
            _sourceParamType = paramType;
            _state = EdgeConnectionState.SourceSelected;

            // ソースポートをハイライト
            nodeVisual.SetPortHighlight(portName, true);
            _highlightedNodeId = _sourceNodeId;
            _highlightedPortName = portName;

            CreatePreviewLine(paramType);
        }

        /// <summary>
        /// ターゲットノードの入力ポートを選択して接続、またはキャンセルする。
        /// </summary>
        private void TrySelectTargetOrCancel()
        {
            if (_sharedRaycast == null || _visualManager == null) return;

            // レイが何にも当たらない → キャンセル
            if (!_sharedRaycast.HasHit)
            {
                Cancel();
                return;
            }

            var hit = _sharedRaycast.CurrentHit;
            var nodeVisual = _visualManager.GetVisualByCollider(hit.collider);

            // ノード以外に当たった → キャンセル
            if (nodeVisual?.Node == null)
            {
                Cancel();
                return;
            }

            // 同じノードをクリック → キャンセル
            if (nodeVisual.Node.Id == _sourceNodeId)
            {
                Cancel();
                return;
            }

            // ターゲットノードの互換入力ポートを自動選択
            var (targetPort, _) = FindNearestCompatibleInputPort(nodeVisual, hit.point);
            if (targetPort == null)
            {
                // 互換ポートなし → キャンセルせず留まる（別ノードを選べるように）
                return;
            }

            // 接続実行
            TryConnect(nodeVisual.Node.Id, targetPort);
            ResetState();
        }

        private void TryConnect(string targetNodeId, string targetPort)
        {
            if (_graphContext == null || _edgeVisualManager == null) return;
            if (_intentSink == null) return;
            if (_sourceNodeId == null || _sourcePortName == null) return;

            // Plan v5.3 Phase 5: GraphState.TryConnect 直接呼び出しを intent emit に置換。
            var intent = new ConnectPortsIntent(
                _sourceNodeId, _sourcePortName, targetNodeId, targetPort);
            if (!_intentSink.Emit(intent))
            {
                Debug.LogWarning($"[EdgeDragHandler] Connection emit failed: {_sourceNodeId}.{_sourcePortName} → {targetNodeId}.{targetPort}");
                return;
            }

            // 接続成功: 新規 Edge を GraphState から読んで visual 生成
            // (Round G で GraphStateToViewModelProjector.OnEdgeAdded 経由に置換予定)
            var context = _graphContext.Context;
            var edges = context.Edges;
            for (var i = edges.Count - 1; i >= 0; i--)
            {
                var edge = edges[i];
                if (edge.FromNodeId == _sourceNodeId && edge.FromPort == _sourcePortName &&
                    edge.ToNodeId == targetNodeId && edge.ToPort == targetPort)
                {
                    var vm = new EdgeViewModel(edge.Id, edge.FromNodeId, edge.FromPort, edge.ToNodeId, edge.ToPort);
                    _edgeVisualManager.CreateEdgeVisual(vm, _sourceParamType);
                    break;
                }
            }
        }

        private void Cancel()
        {
            ResetState();
        }

        private void ResetState()
        {
            ClearHighlight();
            DestroyPreview();
            _state = EdgeConnectionState.Idle;
            _sourceNodeId = null;
            _sourcePortName = null;
        }

        private void OnDestroy()
        {
            _selectSubscription?.Dispose();
            DestroyPreview();
        }
    }

    /// <summary>
    /// エッジ接続の状態。
    /// </summary>
    public enum EdgeConnectionState
    {
        /// <summary>待機中。トリガーでソースノード選択。</summary>
        Idle,

        /// <summary>ソースポート選択済み。トリガーでターゲットノード選択 or キャンセル。</summary>
        SourceSelected
    }
}
