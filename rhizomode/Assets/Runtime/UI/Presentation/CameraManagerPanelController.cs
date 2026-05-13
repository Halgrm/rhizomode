#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Graph.Model;
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
        private const int PanelTextureHeight = 480;
        private const float PanelWorldWidth = 0.28f;
        private const float PanelWorldHeight = 0.36f;

        [SerializeField] private VisualTreeAsset? panelUxml;
        [SerializeField] private StyleSheet? panelStyleSheet;
        [SerializeField] private PathControlPointVisualManager? pathEditorManager;

        private WorldPanelHost? _panelHost;
        private GraphContextBehaviour? _graphContext;
        private readonly List<CinemachineCamera> _cameras = new();
        private CinemachineCamera? _selected;
        private IDisposable? _progressSubscription;
        private readonly List<Button> _cameraButtons = new();
        private readonly List<NodePortRef> _floatOutputs = new();
        private readonly List<Action<bool>> _editModeListeners = new();

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
        private bool _initialized;

        private readonly struct NodePortRef
        {
            public readonly string NodeId;
            public readonly string DisplayName;

            public NodePortRef(string nodeId, string displayName)
            {
                NodeId = nodeId;
                DisplayName = displayName;
            }
        }

        /// <summary>
        /// GraphState への参照を設定する。GameBootstrap から呼ぶ。
        /// </summary>
        public void Initialize(GraphContextBehaviour graphContext)
        {
            _graphContext = graphContext;
            EnsureHostInitialized();
        }

        private void Awake()
        {
            _panelHost = GetComponent<WorldPanelHost>();
        }

        private void Update()
        {
            if (_initialized) return;
            EnsureHostInitialized();
            TryCacheUI();
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

            if (_list == null || _fovSlider == null || _dutchSlider == null) return;

            _fovSlider.RegisterValueChangedCallback(OnFovChanged);
            _dutchSlider.RegisterValueChangedCallback(OnDutchChanged);
            _progressDropdown?.RegisterValueChangedCallback(OnProgressSourceChanged);
            _progressSlider?.RegisterValueChangedCallback(OnProgressSliderChanged);
            _lookAtDropdown?.RegisterValueChangedCallback(OnLookAtChanged);
            _editPathToggle?.RegisterValueChangedCallback(OnEditPathToggleChanged);
            if (_progressRefreshButton != null) _progressRefreshButton.clicked += OnProgressRefreshClicked;

            DiscoverCameras();
            RefreshCameraList();
            HideDetails();
            _initialized = true;
        }

        private void OnDestroy()
        {
            _progressSubscription?.Dispose();
        }
    }
}
