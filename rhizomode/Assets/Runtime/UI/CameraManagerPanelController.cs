#nullable enable

using System;
using System.Collections.Generic;
using R3;
using Rhizomode.Cameras;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UIElements;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

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
    [RequireComponent(typeof(WorldPanelHost))]
    public class CameraManagerPanelController : MonoBehaviour
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

        private void DiscoverCameras()
        {
            _cameras.Clear();
            var found = FindObjectsByType<CinemachineCamera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            _cameras.AddRange(found);
        }

        private void RefreshCameraList()
        {
            if (_list == null) return;
            _list.Clear();
            _cameraButtons.Clear();

            foreach (var cam in _cameras)
            {
                if (cam == null) continue;
                var captured = cam;
                var button = new Button(() => HandleCameraClicked(captured))
                {
                    text = cam.name
                };
                button.AddToClassList("camera-button");
                _cameraButtons.Add(button);
                _list.Add(button);
            }
            UpdateLiveHighlights();
        }

        private void HandleCameraClicked(CinemachineCamera cam)
        {
            _selected = cam;
            foreach (var c in _cameras)
            {
                if (c == null) continue;
                c.Priority = (c == cam) ? LivePriority : DormantPriority;
            }
            UpdateLiveHighlights();
            ShowDetails(cam);
        }

        private void UpdateLiveHighlights()
        {
            for (int i = 0; i < _cameraButtons.Count && i < _cameras.Count; i++)
            {
                var cam = _cameras[i];
                if (cam == null) continue;
                bool isLive = cam.Priority.Value >= LivePriority;
                if (isLive) _cameraButtons[i].AddToClassList("camera-button--live");
                else _cameraButtons[i].RemoveFromClassList("camera-button--live");
            }
        }

        private void ShowDetails(CinemachineCamera cam)
        {
            if (_details == null) return;
            _details.style.display = DisplayStyle.Flex;
            if (_detailsTitle != null) _detailsTitle.text = cam.name;

            var lens = cam.Lens;
            _fovSlider?.SetValueWithoutNotify(lens.FieldOfView);
            if (_fovValue != null) _fovValue.text = $"{lens.FieldOfView:F0}";
            _dutchSlider?.SetValueWithoutNotify(lens.Dutch);
            if (_dutchValue != null) _dutchValue.text = $"{lens.Dutch:F0}";

            RefreshLookAtDropdown(cam);

            var pathCam = cam.GetComponent<PathCameraController>();
            if (pathCam != null)
            {
                if (_progressRow != null) _progressRow.style.display = DisplayStyle.Flex;
                if (_editRow != null) _editRow.style.display = DisplayStyle.Flex;
                RefreshFloatOutputs();
                if (_progressDropdown != null)
                {
                    var labels = new List<string> { NoSourceLabel };
                    foreach (var p in _floatOutputs) labels.Add(p.DisplayName);
                    _progressDropdown.choices = labels;
                    _progressDropdown.SetValueWithoutNotify(NoSourceLabel);
                }
                // 既存の Source 購読を解除し、Slider を現在値で初期化
                _progressSubscription?.Dispose();
                _progressSubscription = null;
                if (_progressSlider != null) _progressSlider.SetValueWithoutNotify(pathCam.Progress);
                if (_progressValue != null) _progressValue.text = $"{pathCam.Progress:F2}";
                // 別カメラを選んだら編集モードは強制終了
                if (_editPathToggle != null && _editPathToggle.value)
                {
                    _editPathToggle.SetValueWithoutNotify(false);
                    StopEditing();
                }
            }
            else
            {
                if (_progressRow != null) _progressRow.style.display = DisplayStyle.None;
                if (_editRow != null) _editRow.style.display = DisplayStyle.None;
                if (_editPathToggle != null && _editPathToggle.value)
                {
                    _editPathToggle.SetValueWithoutNotify(false);
                    StopEditing();
                }
            }
        }

        /// <summary>
        /// "Edit Path" トグルの状態変化を受け取り他ハンドラに通知する。
        /// 受信側 (GameBootstrap) は EdgeDragHandler/EdgeCutHandler/NodeDeleteHandler を一時停止させる。
        /// </summary>
        public void AddEditModeListener(Action<bool> listener)
        {
            _editModeListeners.Add(listener);
        }

        private void OnEditPathToggleChanged(ChangeEvent<bool> e)
        {
            if (e.newValue) StartEditing();
            else StopEditing();
        }

        private void StartEditing()
        {
            if (_selected == null || pathEditorManager == null) return;
            var pathCam = _selected.GetComponent<PathCameraController>();
            if (pathCam == null) return;
            pathEditorManager.BeginEdit(pathCam);
            NotifyEditMode(true);
        }

        private void StopEditing()
        {
            if (pathEditorManager == null) return;
            pathEditorManager.EndEdit();
            NotifyEditMode(false);
        }

        private void NotifyEditMode(bool isEditing)
        {
            foreach (var listener in _editModeListeners) listener?.Invoke(isEditing);
        }

        private void HideDetails()
        {
            if (_details != null) _details.style.display = DisplayStyle.None;
        }

        private void RefreshFloatOutputs()
        {
            _floatOutputs.Clear();
            if (_graphContext == null) return;
            var ctx = _graphContext.Context;
            foreach (var node in ctx.Nodes.Values)
            {
                foreach (var kv in node.OutputPorts)
                {
                    if (kv.Value.Type != ParamType.Float) continue;
                    var display = $"{node.NodeType} · {kv.Key}";
                    _floatOutputs.Add(new NodePortRef(node.Id, display));
                }
            }
        }

        private void OnFovChanged(ChangeEvent<float> e)
        {
            if (_selected == null) return;
            var lens = _selected.Lens;
            lens.FieldOfView = Mathf.Clamp(e.newValue, 1f, 179f);
            _selected.Lens = lens;
            if (_fovValue != null) _fovValue.text = $"{lens.FieldOfView:F0}";
        }

        private void OnDutchChanged(ChangeEvent<float> e)
        {
            if (_selected == null) return;
            var lens = _selected.Lens;
            lens.Dutch = e.newValue;
            _selected.Lens = lens;
            if (_dutchValue != null) _dutchValue.text = $"{lens.Dutch:F0}";
        }

        private void OnProgressSourceChanged(ChangeEvent<string> e)
        {
            _progressSubscription?.Dispose();
            _progressSubscription = null;

            if (_selected == null) return;
            var pathCam = _selected.GetComponent<PathCameraController>();
            if (pathCam == null) return;
            if (string.IsNullOrEmpty(e.newValue) || e.newValue == NoSourceLabel) return;
            if (_graphContext == null) return;

            string? targetNodeId = null;
            foreach (var p in _floatOutputs)
            {
                if (p.DisplayName != e.newValue) continue;
                targetNodeId = p.NodeId;
                break;
            }
            if (targetNodeId == null) return;

            var ctx = _graphContext.Context;
            if (!ctx.Nodes.TryGetValue(targetNodeId, out var node)) return;

            // ポート名はラベルの "·" 右側
            var dot = e.newValue.LastIndexOf('·');
            if (dot < 0 || dot + 2 > e.newValue.Length) return;
            var portName = e.newValue.Substring(dot + 1).Trim();

            var port = node.GetOutputPort(portName);
            if (port is not OutputPort<float> floatPort) return;

            _progressSubscription = floatPort.Observable.Subscribe(v =>
            {
                pathCam.SetProgress(v);
                // スライダーも追従させて現在値を可視化
                if (_progressSlider != null) _progressSlider.SetValueWithoutNotify(Mathf.Clamp01(v));
                if (_progressValue != null) _progressValue.text = $"{v:F2}";
            });
        }

        /// <summary>
        /// Progress スライダーで手動駆動する。Source が選ばれていてもスライダー操作は通る (一時オーバーライド)。
        /// </summary>
        private void OnProgressSliderChanged(ChangeEvent<float> e)
        {
            if (_selected == null) return;
            var pathCam = _selected.GetComponent<PathCameraController>();
            if (pathCam == null) return;
            pathCam.SetProgress(e.newValue);
            if (_progressValue != null) _progressValue.text = $"{e.newValue:F2}";
        }

        /// <summary>
        /// 選択中カメラの LookAt フィールドの現状に合わせて Look At dropdown を再構築する。
        /// </summary>
        private void RefreshLookAtDropdown(CinemachineCamera cam)
        {
            if (_lookAtDropdown == null) return;

            var labels = new List<string> { NoLookAtLabel };
            foreach (var t in LookAtTargetMarker.AllTargets)
            {
                if (t == null) continue;
                labels.Add(t.DisplayName);
            }
            _lookAtDropdown.choices = labels;

            // 現在の LookAt 設定を反映
            string current = NoLookAtLabel;
            if (cam.LookAt != null)
            {
                var marker = cam.LookAt.GetComponent<LookAtTargetMarker>();
                if (marker != null && labels.Contains(marker.DisplayName))
                    current = marker.DisplayName;
            }
            _lookAtDropdown.SetValueWithoutNotify(current);
        }

        private void OnLookAtChanged(ChangeEvent<string> e)
        {
            if (_selected == null) return;
            if (string.IsNullOrEmpty(e.newValue) || e.newValue == NoLookAtLabel)
            {
                _selected.LookAt = null;
                return;
            }
            foreach (var t in LookAtTargetMarker.AllTargets)
            {
                if (t == null) continue;
                if (t.DisplayName == e.newValue)
                {
                    _selected.LookAt = t.transform;
                    return;
                }
            }
        }

        /// <summary>
        /// Dropdown を最新の Float 出力ポート一覧で再構築する。
        /// 編集モード後に LFO を追加したり等で必要。
        /// </summary>
        private void OnProgressRefreshClicked()
        {
            if (_selected == null) return;
            var pathCam = _selected.GetComponent<PathCameraController>();
            if (pathCam == null || _progressDropdown == null) return;

            RefreshFloatOutputs();
            var labels = new List<string> { NoSourceLabel };
            foreach (var p in _floatOutputs) labels.Add(p.DisplayName);
            _progressDropdown.choices = labels;

            // 現在選択が一覧から消えていたら (none) に戻す
            if (!labels.Contains(_progressDropdown.value))
            {
                _progressDropdown.SetValueWithoutNotify(NoSourceLabel);
                _progressSubscription?.Dispose();
                _progressSubscription = null;
            }
        }

        private void OnDestroy()
        {
            _progressSubscription?.Dispose();
        }
    }
}
