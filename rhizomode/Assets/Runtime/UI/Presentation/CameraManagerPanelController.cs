#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Cameras;
using Rhizomode.UI.Contracts;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// CinemachineCamera 群を GUI で管理するパネル。
    /// - シーン内の CinemachineCamera を自動列挙
    /// - クリックで Priority を切替（ライブ化）
    /// - 選択カメラの FOV/Dutch をスライダーで調整
    /// - PathCameraController を持つカメラには Progress ソース dropdown を表示し、
    ///   グラフ内 Float 出力ポートを購読して SplineDolly.CameraPosition を駆動
    /// </summary>
    /// <remarks>
    /// Phase 9 Round C で partial class に分割:
    /// - <c>CameraManagerPanelController.cs</c> (本ファイル): フィールド / Awake / Update / Initialize / TryCacheUI / OnDestroy / NodePortRef
    /// - <c>CameraManagerPanelController.Cameras.cs</c>: カメラ列挙 / 選択 / Live priority / FOV+Dutch+LookAt callback
    /// - <c>CameraManagerPanelController.Path.cs</c>: PathCameraController 連動 (Progress source / Edit toggle)
    /// </remarks>
    [RequireComponent(typeof(WorldPanelHost))]
    public partial class CameraManagerPanelController : MonoBehaviour
    {
        private const int LivePriority = 20;
        private const int DormantPriority = 5;
        private const string NoSourceLabel = "(none)";
        private const string NoLookAtLabel = "(none)";
        private const int PanelTextureWidth = 360;
        // LookAt Phase 2-A で Place/Edit Toggle 2 行追加されたため縦を 480 → 680 に拡大 (約 40% 増)。
        // 同時に WorldHeight も 0.36 → 0.51 に拡大しアスペクトを維持。
        private const int PanelTextureHeight = 680;
        private const float PanelWorldWidth = 0.28f;
        private const float PanelWorldHeight = 0.51f;

        [SerializeField] private VisualTreeAsset? panelUxml;
        [SerializeField] private StyleSheet? panelStyleSheet;
        [SerializeField] private PathControlPointVisualManager? pathEditorManager;
        [SerializeField] private LookAtMarkerVisualManager? lookAtMarkerManager;

        private WorldPanelHost? _panelHost;
        private IFloatOutputCatalog? _floatOutputCatalog;
        private readonly List<CinemachineCamera> _cameras = new();
        private CinemachineCamera? _selected;
        private IDisposable? _progressSubscription;
        private readonly List<Button> _cameraButtons = new();
        private readonly List<FloatOutputRef> _floatOutputs = new();
        private readonly List<Action<bool>> _editModeListeners = new();
        private int _editModeRefCount;

        private VisualElement? _root;
        private VisualElement? _list;
        private VisualElement? _details;
        private VisualElement? _progressRow;
        private VisualElement? _editRow;
        private Label? _detailsTitle;
        private Slider? _fovSlider;
        private Label? _fovValue;
        private Slider? _dutchSlider;
        private Label? _dutchValue;
        private DropdownField? _progressDropdown;
        private Button? _progressRefreshButton;
        private Slider? _progressSlider;
        private Label? _progressValue;
        private DropdownField? _lookAtDropdown;
        private Toggle? _editPathToggle;
        private Toggle? _lookAtPlaceToggle;
        private Toggle? _lookAtEditToggle;
        private Toggle? _mirrorShowUiToggle;
        private MirrorOutputController? _mirrorOutput;
        private bool _initialized;

        /// <summary>
        /// Float 出力カタログを設定する。GameBootstrap から呼ぶ。
        /// Round E5 で <c>GraphContextBehaviour</c> から <see cref="IFloatOutputCatalog"/> に変更。
        /// </summary>
        public void Initialize(IFloatOutputCatalog floatOutputCatalog)
        {
            _floatOutputCatalog = floatOutputCatalog;
            EnsureHostInitialized();
        }

        /// <summary>
        /// Mirror カメラへの UI 表示切替を Toggle にバインドする。VerticalSliceBootstrapWiring から呼ぶ。
        /// UI cache 前に呼ばれた場合は参照だけ保持し、TryCacheUI で初期同期する。
        /// </summary>
        public void BindMirrorOutput(MirrorOutputController mirror)
        {
            _mirrorOutput = mirror;
            SyncMirrorToggleFromOutput();
        }

        private void SyncMirrorToggleFromOutput()
        {
            if (_mirrorShowUiToggle == null || _mirrorOutput == null) return;
            _mirrorShowUiToggle.SetValueWithoutNotify(_mirrorOutput.IsUIVisible);
        }

        private void OnMirrorShowUiToggleChanged(ChangeEvent<bool> e)
        {
            _mirrorOutput?.SetUIVisible(e.newValue);
        }

        private void Awake()
        {
            _panelHost = GetComponent<WorldPanelHost>();
        }

        private void Update()
        {
            if (!_initialized)
            {
                EnsureHostInitialized();
                TryCacheUI();
                return;
            }

            SyncLookAtToggles();
        }

        /// <summary>
        /// <see cref="LookAtMarkerVisualManager"/> の <c>IsPlacing</c>/<c>IsEditing</c> と UI Toggle 値を
        /// 同期する。Place は配置完了で自動 OFF、Edit は外部 (例: 別カメラ選択時の強制 EndEditing) でも UI 反映する。
        /// </summary>
        /// <remarks>
        /// Update polling は 60-90fps × 2 fields の bool 比較なので negligible。event 駆動より破綻が少ない。
        /// SetValueWithoutNotify を使うので OnLookAt*ToggleChanged は再発火しない。
        /// </remarks>
        private void SyncLookAtToggles()
        {
            if (lookAtMarkerManager == null) return;

            if (_lookAtPlaceToggle != null && _lookAtPlaceToggle.value != lookAtMarkerManager.IsPlacing)
                _lookAtPlaceToggle.SetValueWithoutNotify(lookAtMarkerManager.IsPlacing);

            if (_lookAtEditToggle != null && _lookAtEditToggle.value != lookAtMarkerManager.IsEditing)
                _lookAtEditToggle.SetValueWithoutNotify(lookAtMarkerManager.IsEditing);
        }

        private void EnsureHostInitialized()
        {
            if (_panelHost == null) _panelHost = GetComponent<WorldPanelHost>();
            if (_panelHost == null || panelUxml == null) return;
            if (_panelHost.IsInitialized) return;

            _panelHost.Initialize(panelUxml, panelStyleSheet, PanelTextureWidth, PanelTextureHeight);
            _panelHost.Resize(PanelWorldWidth, PanelWorldHeight);
        }

        private void TryCacheUI()
        {
            if (_panelHost == null) return;
            _root = _panelHost.Root;
            if (_root == null) return;

            _list = _root.Q<VisualElement>("camera-list");
            _details = _root.Q<VisualElement>("details");
            _progressRow = _root.Q<VisualElement>("progress-row");
            _editRow = _root.Q<VisualElement>("edit-row");
            _detailsTitle = _root.Q<Label>("details-title");
            _fovSlider = _root.Q<Slider>("fov-slider");
            _fovValue = _root.Q<Label>("fov-value");
            _dutchSlider = _root.Q<Slider>("dutch-slider");
            _dutchValue = _root.Q<Label>("dutch-value");
            _progressDropdown = _root.Q<DropdownField>("progress-source");
            _progressRefreshButton = _root.Q<Button>("progress-refresh");
            _progressSlider = _root.Q<Slider>("progress-slider");
            _progressValue = _root.Q<Label>("progress-value");
            _lookAtDropdown = _root.Q<DropdownField>("lookat-target");
            _editPathToggle = _root.Q<Toggle>("edit-path-toggle");
            _lookAtPlaceToggle = _root.Q<Toggle>("lookat-place-toggle");
            _lookAtEditToggle = _root.Q<Toggle>("lookat-edit-toggle");
            _mirrorShowUiToggle = _root.Q<Toggle>("mirror-show-ui-toggle");

            if (_list == null || _fovSlider == null || _dutchSlider == null) return;

            _fovSlider.RegisterValueChangedCallback(OnFovChanged);
            _dutchSlider.RegisterValueChangedCallback(OnDutchChanged);
            _progressDropdown?.RegisterValueChangedCallback(OnProgressSourceChanged);
            _progressSlider?.RegisterValueChangedCallback(OnProgressSliderChanged);
            _lookAtDropdown?.RegisterValueChangedCallback(OnLookAtChanged);
            _editPathToggle?.RegisterValueChangedCallback(OnEditPathToggleChanged);
            _lookAtPlaceToggle?.RegisterValueChangedCallback(OnLookAtPlaceToggleChanged);
            _lookAtEditToggle?.RegisterValueChangedCallback(OnLookAtEditToggleChanged);
            _mirrorShowUiToggle?.RegisterValueChangedCallback(OnMirrorShowUiToggleChanged);
            SyncMirrorToggleFromOutput();
            if (_progressRefreshButton != null) _progressRefreshButton.clicked += OnProgressRefreshClicked;

            // LookAt registry の追加/削除/命名変化で dropdown を自動更新する。
            LookAtTargetMarker.OnRegistryChanged += OnLookAtRegistryChanged;

            DiscoverCameras();
            RefreshCameraList();
            HideDetails();
            _initialized = true;
        }

        private void OnDestroy()
        {
            _progressSubscription?.Dispose();
            LookAtTargetMarker.OnRegistryChanged -= OnLookAtRegistryChanged;

            // F1 fix re-review (Codex, 2026-05-18): edit mode 中に panel が破棄されると
            // listener (NodeGrab/Object3DGrab/EdgeDrag/EdgeCut/NodeDelete の SetEnabled(false))
            // が無効化されたまま残る。refcount > 0 ならまとめて 0 まで巻き戻して active=false を
            // 1 度発火し、handler を re-enable する。
            if (_editModeRefCount > 0)
            {
                _editModeRefCount = 0;
                foreach (var listener in _editModeListeners) listener?.Invoke(false);
            }
        }

        /// <summary>
        /// <see cref="LookAtTargetMarker.OnRegistryChanged"/> subscriber。選択中カメラがあれば dropdown を再構築。
        /// </summary>
        private void OnLookAtRegistryChanged()
        {
            if (_selected == null) return;
            RefreshLookAtDropdown(_selected);
        }
    }
}
