#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using R3;
using Rhizomode.Audio.Analysis;
using Rhizomode.Audio.GraphAdapter;
using Rhizomode.Cameras;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Ableton.Transport;
using Rhizomode.Ableton.Session;
using Rhizomode.Ableton.GraphAdapter;
using Rhizomode.Nodes.Ableton;
using Rhizomode.Nodes.OscMidi;
using Rhizomode.Modules;
using Rhizomode.Nodes.Generators;
using Rhizomode.Nodes.Input;
using Rhizomode.Nodes.Math;
using Rhizomode.Nodes.Modules;
using Rhizomode.Nodes.Time;
using Rhizomode.Nodes.Utility;
using Rhizomode.UI;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.XR
{
    /// <summary>
    /// ゲーム起動時に全システムの初期化と相互接続を行う。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GraphContextBehaviour? graphContext;
        [SerializeField] private NodeVisualManager? visualManager;
        [SerializeField] private ControllerInputRouter? controllerInput;
        [SerializeField] private EdgeVisualManager? edgeVisualManager;
        [SerializeField] private EdgeDragHandler? edgeDragHandler;
        [SerializeField] private EdgeCutHandler? edgeCutHandler;
        [SerializeField] private NodeDeleteHandler? nodeDeleteHandler;
        [SerializeField] private NodeGrabHandler? nodeGrabHandler;
        [SerializeField] private UIRaycastDriver? uiRaycastDriver;
        [SerializeField] private SharedRaycastService? sharedRaycastService;
        [SerializeField] private ScrollMenuVisualController? scrollMenuVisual;
        [SerializeField] private ScrollMenuInteractionHandler? scrollMenuInteraction;
        [SerializeField] private AudioDriverBehaviour? audioDriver;
        [SerializeField] private ModuleDefinition[]? moduleDefinitions;
        [SerializeField] private CinemachineModule[]? cinemachineModules;
        [SerializeField] private PresetManager? presetManager;
        [SerializeField] private SpoutSenderController? spoutSender;
        [SerializeField] private NdiSenderController? ndiSender;

        [Header("Week 5 — 統合システム")]
        [SerializeField] private MirrorOutputController? mirrorOutput;
        [SerializeField] private AudioDeviceSelector? audioDeviceSelector;
        [SerializeField] private StatusPanelController? statusPanel;
        [SerializeField] private CameraManagerPanelController? cameraManagerPanel;
        [SerializeField] private DesktopMirrorBlitter? desktopBlitter;
        [SerializeField] private PathControlPointVisualManager? pathEditorManager;
        [SerializeField] private PathControlPointGrabHandler? pathControlPointGrabHandler;
        [SerializeField] private GraphSaveLoadManager? graphSaveLoad;

        [Header("Object3D")]
        [SerializeField] private Object3DPrefabList? object3DPrefabs;
        [SerializeField] private Object3DGrabHandler? object3DGrabHandler;

        [Header("Desktop Debug")]
        [SerializeField] private DesktopInputRouter? desktopInput;
        [SerializeField] private CinemachinePreviewMonitor? cinemachinePreview;

        [Header("Scene Switching")]
        [SerializeField] private AdditiveSceneLoader? sceneLoader;

        [Header("Ableton OSC")]
        [SerializeField] private AbletonLink? abletonLink;
        [SerializeField] private AbletonOscBridge? abletonBridge;
        [SerializeField] private AbletonSetupPanel? abletonSetupPanel;
        [SerializeField] private AbletonClipGridManager? abletonGridManager;
        [SerializeField] private ClipFireRayHandler? clipFireHandler;
        [SerializeField] private AbletonControlPanel? abletonControlPanel;

        [Header("Ableton UI Anchor (Editor で位置・回転調整)")]
        [Tooltip("Ableton UI 全体の基準アンカー。Setup/Grid/Control パネルがこの位置・向きを共有して配置される。" +
                 "未設定ならプレイヤー前方にフォールバック。Z+ がパネル正面。")]
        [SerializeField] private Transform? abletonUiAnchor;

        [Header("Ableton Outer Frame (Grid + Control 全体を囲む枠)")]
        [Tooltip("UnlitRoundedFrame シェーダーを使ったマテリアル。null なら外枠を生成しない。")]
        [SerializeField] private Material? abletonOuterFrameMaterial;
        [Tooltip("コンテンツ端からの余白 (m)。フレームはこの分外側に広がる。")]
        [SerializeField, Range(0.01f, 0.2f)] private float abletonOuterFramePadding = 0.06f;
        [Tooltip("フレームの背後オフセット (m)。すべての UI の最も奥に置く。")]
        [SerializeField, Range(0.0f, 0.05f)] private float abletonOuterFrameDepthOffset = 0.012f;
        [Tooltip("外枠の角丸半径 (m)。")]
        [SerializeField, Range(0.0f, 0.2f)] private float abletonOuterFrameCornerRadius = 0.04f;

        [Header("Ableton Macro (どの Track / Device の Rack の Macro を VR から触るか)")]
        [Tooltip("Track index。-1 = Master Track (推奨: Live Set 切替で位置がズレない)、0..N-1 = 通常 Track。")]
        [SerializeField] private int macroTrackIndex = -1;
        [Tooltip("Track 内のどの Device の Macro を扱うか。0 = 最初のデバイス。")]
        [SerializeField, Range(0, 16)] private int macroDeviceIndex = 0;

        private GameObject? _abletonOuterFrameInstance;
        private IDisposable? _macroValueListenerSub;

        private NodeTypeRegistry? _typeRegistry;

        /// <summary>実際に使用中の入力ルーター（VRまたはデスクトップ）。</summary>
        private IControllerInput? _activeInput;

        // AudioDeviceSelector イベント購読解除用デリゲート
        private Action<string>? _onDeviceSelected;
        private Action? _onRefreshRequested;

        /// <summary>
        /// モジュールノードに紐づくPrefabインスタンスを追跡する。
        /// ノード削除・グラフロード時のクリーンアップに使用。
        /// </summary>
        private readonly Dictionary<string, GameObject> _moduleInstances = new();

        /// <summary>Object3Dプレハブ名→元プレハブの逆引き。Instantiate用。</summary>
        private readonly Dictionary<string, GameObject> _object3DPrefabMap = new();

        private static readonly Dictionary<string, Func<string, NodeBase>> NodeFactoryMap = new()
        {
            ["ConstFloat"] = id => new ConstFloatNode(id),
            ["AudioDevice"] = id => new AudioDeviceNode(id),
            ["AudioTrigger"] = id => new AudioTriggerNode(id),
            ["BeatDetector"] = id => new BeatDetectorNode(id),
            ["TapTempo"] = id => new TapTempoNode(id),
            ["Multiply"] = id => new MultiplyNode(id),
            ["Smooth"] = id => new SmoothNode(id),
            ["Time"] = id => new Nodes.Time.TimeNode(id),
            ["Threshold"] = id => new ThresholdNode(id),
            ["Toggle"] = id => new ToggleNode(id),
            ["FloatMonitor"] = id => new FloatMonitorNode(id),
            ["BoolMonitor"] = id => new BoolMonitorNode(id),
            ["ColorMonitor"] = id => new ColorMonitorNode(id),
            ["AudioMonitor"] = id => new AudioMonitorNode(id),
            ["ConstColor"] = id => new ConstColorNode(id),
            ["LFO"] = id => new LfoNode(id),
            ["Noise"] = id => new NoiseNode(id),
            ["OscReceiver"] = id => new OscReceiverNode(id),
            ["MidiCC"] = id => new MidiCCNode(id),
            ["Add"] = id => new AddNode(id),
            ["Remap"] = id => new RemapNode(id),
            ["Delay"] = id => new DelayNode(id),
            ["Timer"] = id => new TimerNode(id),
            ["ColorToFloats"] = id => new ColorToFloatsNode(id),
            ["FloatsToColor"] = id => new FloatsToColorNode(id),
            ["ColorToHSV"] = id => new ColorToHSVNode(id),
            ["HSVToColor"] = id => new HSVToColorNode(id),
            ["AudioBand"] = id => new AudioBandNode(id),
            ["SpectrumMonitor"] = id => new SpectrumMonitorNode(id),
            ["Trigger"] = id => new TriggerNode(id),
            ["SceneSwitch"] = id => new SceneSwitchNode(id),
            ["SceneDark"] = id => new SceneTriggerNode(id, "SceneDark", 0),
            ["SceneWhite"] = id => new SceneTriggerNode(id, "SceneWhite", 1),
            ["SceneNature"] = id => new SceneTriggerNode(id, "SceneNature", 2),
            ["AbletonTempo"] = id => new AbletonTempoNode(id),
            ["AbletonTransport"] = id => new AbletonTransportNode(id),
            ["AbletonTrackVolume"] = id => new AbletonTrackVolumeNode(id),
            ["AbletonClipFire"] = id => new AbletonClipFireNode(id),
        };

        private void Awake()
        {
            _typeRegistry = new NodeTypeRegistry();
            RegisterNodeTypes();
            RegisterFactories();
            RegisterModuleTypes();
            RegisterCinemachineModules();
            RegisterObject3DTypes();
            InitializeSystems();
            InitializeVerticalSliceSystems();
            RegisterSceneObjects();
        }

        private void RegisterNodeTypes()
        {
            if (_typeRegistry == null) return;

            _typeRegistry.Register(new NodeTypeInfo("ConstFloat", "Const Float", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("AudioDevice", "Audio Device", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("AudioTrigger", "Audio Trigger", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("AudioBand", "Audio Band", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("BeatDetector", "Beat Detector", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("TapTempo", "Tap Tempo", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("Trigger", "Trigger", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("Multiply", "Multiply", NodeCategory.Math));
            _typeRegistry.Register(new NodeTypeInfo("Smooth", "Smooth", NodeCategory.Math));
            _typeRegistry.Register(new NodeTypeInfo("Time", "Time", NodeCategory.Time));
            _typeRegistry.Register(new NodeTypeInfo("Threshold", "Threshold", NodeCategory.Utility));
            _typeRegistry.Register(new NodeTypeInfo("Toggle", "Toggle", NodeCategory.Utility));
            _typeRegistry.Register(new NodeTypeInfo("FloatMonitor", "Float Monitor", NodeCategory.Utility));
            _typeRegistry.Register(new NodeTypeInfo("BoolMonitor", "Bool Monitor", NodeCategory.Utility));
            _typeRegistry.Register(new NodeTypeInfo("ColorMonitor", "Color Monitor", NodeCategory.Utility));
            _typeRegistry.Register(new NodeTypeInfo("AudioMonitor", "Audio Monitor", NodeCategory.Utility));
            _typeRegistry.Register(new NodeTypeInfo("SpectrumMonitor", "Spectrum Monitor", NodeCategory.Utility));
            _typeRegistry.Register(new NodeTypeInfo("ConstColor", "Const Color", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("LFO", "LFO", NodeCategory.Time));
            _typeRegistry.Register(new NodeTypeInfo("Noise", "Noise", NodeCategory.Time));
            _typeRegistry.Register(new NodeTypeInfo("OscReceiver", "OSC Receiver", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("MidiCC", "MIDI CC", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("Add", "Add", NodeCategory.Math));
            _typeRegistry.Register(new NodeTypeInfo("Remap", "Remap", NodeCategory.Math));
            _typeRegistry.Register(new NodeTypeInfo("Delay", "Delay", NodeCategory.Time));
            _typeRegistry.Register(new NodeTypeInfo("Timer", "Timer", NodeCategory.Time));
            _typeRegistry.Register(new NodeTypeInfo("ColorToFloats", "Color → Floats", NodeCategory.Utility));
            _typeRegistry.Register(new NodeTypeInfo("FloatsToColor", "Floats → Color", NodeCategory.Utility));
            _typeRegistry.Register(new NodeTypeInfo("ColorToHSV", "Color → HSV", NodeCategory.Utility));
            _typeRegistry.Register(new NodeTypeInfo("HSVToColor", "HSV → Color", NodeCategory.Utility));
            _typeRegistry.Register(new NodeTypeInfo("SceneSwitch", "Scene Switch", NodeCategory.Scene));
            _typeRegistry.Register(new NodeTypeInfo("SceneDark", "Dark", NodeCategory.Scene));
            _typeRegistry.Register(new NodeTypeInfo("SceneWhite", "White", NodeCategory.Scene));
            _typeRegistry.Register(new NodeTypeInfo("SceneNature", "Nature", NodeCategory.Scene));
            _typeRegistry.Register(new NodeTypeInfo("AbletonTempo", "Ableton Tempo", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("AbletonTransport", "Ableton Transport", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("AbletonTrackVolume", "Ableton Track Vol", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("AbletonClipFire", "Ableton Clip Fire", NodeCategory.Input));
        }

        private void RegisterFactories()
        {
            if (_typeRegistry == null) return;

            foreach (var typeName in _typeRegistry.AllTypes.Keys)
            {
                if (!NodeFactoryMap.TryGetValue(typeName, out var factory))
                {
                    Debug.LogError($"[GameBootstrap] No factory for registered type: {typeName}");
                    continue;
                }

                graphContext?.Context.RegisterNodeFactory(typeName, factory);
            }
        }

        /// <summary>
        /// ModuleDefinition配列からVFX/Shaderモジュールノードのタイプとファクトリを動的登録する。
        /// ファクトリはノード生成のみ。Prefab注入はメニュー選択時(InjectModuleIfNeeded)
        /// またはグラフロード後(ReinjectModulesAfterLoad)に分離して行う。
        /// </summary>
        private void RegisterModuleTypes()
        {
            if (_typeRegistry == null || moduleDefinitions == null) return;

            foreach (var def in moduleDefinitions)
            {
                if (def == null) continue;

                var capturedDef = def;

                // Prefabのコンポーネントで登録カテゴリを判定
                var hasVfx = def.prefab != null && def.prefab.GetComponent<VFXModule>() != null;
                var hasShader = def.prefab != null && def.prefab.GetComponent<ShaderModule>() != null;

                // どちらもない場合は両方登録（後方互換）
                if (!hasVfx && !hasShader) { hasVfx = true; hasShader = true; }

                if (hasVfx)
                {
                    var vfxTypeName = $"VFX_{def.moduleName}";
                    _typeRegistry.Register(new NodeTypeInfo(vfxTypeName, $"VFX: {def.moduleName}", NodeCategory.VFX));
                    Func<string, NodeBase> vfxFactory = id => new VFXModuleNode(id, capturedDef);
                    graphContext?.Context.RegisterNodeFactory(vfxTypeName, vfxFactory);
                }

                if (hasShader)
                {
                    var shaderTypeName = $"Shader_{def.moduleName}";
                    _typeRegistry.Register(new NodeTypeInfo(shaderTypeName, $"Shader: {def.moduleName}", NodeCategory.Shader));
                    Func<string, NodeBase> shaderFactory = id => new ShaderModuleNode(id, capturedDef);
                    graphContext?.Context.RegisterNodeFactory(shaderTypeName, shaderFactory);
                }
            }
        }

        /// <summary>
        /// VFXModuleNodeにPrefabインスタンスとVFXModuleを注入する。
        /// Prefabが未設定またはGetComponent失敗時はログ出力のみで続行（映像停止防止）。
        /// </summary>
        private void InstantiateVFXModule(VFXModuleNode node, ModuleDefinition def)
        {
            if (def.prefab == null)
            {
                Debug.LogWarning($"[GameBootstrap] VFX module '{def.moduleName}' has no prefab assigned");
                return;
            }

            try
            {
                var instance = Instantiate(def.prefab);
                instance.name = $"VFXModule_{def.moduleName}_{node.Id[..8]}";

                // ノード位置の前方にVFXを配置（プレイヤーから見える位置）
                if (controllerInput != null)
                {
                    var headPos = _activeInput!.HeadPosition;
                    var headFwd = _activeInput!.HeadForward;
                    instance.transform.position = headPos + headFwd * 1.5f;
                }
                else
                {
                    instance.transform.position = node.Position;
                }

                _moduleInstances[node.Id] = instance;

                var module = instance.GetComponent<VFXModule>();
                if (module != null)
                {
                    module.Initialize(def);
                    node.Module = module;
                }
                else
                {
                    Debug.LogError(
                        $"[GameBootstrap] VFX prefab '{def.prefab.name}' lacks VFXModule component");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameBootstrap] VFX module instantiation failed for '{def.moduleName}': {e.Message}");
            }
        }

        /// <summary>
        /// ShaderModuleNodeにPrefabインスタンスとShaderModuleを注入する。
        /// RendererコンポーネントがPrefab上に必要。
        /// </summary>
        private void InstantiateShaderModule(ShaderModuleNode node, ModuleDefinition def)
        {
            if (def.prefab == null)
            {
                Debug.LogWarning($"[GameBootstrap] Shader module '{def.moduleName}' has no prefab assigned");
                return;
            }

            try
            {
                var instance = Instantiate(def.prefab);
                instance.name = $"ShaderModule_{def.moduleName}_{node.Id[..8]}";
                _moduleInstances[node.Id] = instance;

                var module = instance.GetComponent<ShaderModule>();
                var renderer = instance.GetComponent<Renderer>();
                if (module != null && renderer != null)
                {
                    module.Initialize(def, renderer);
                    node.Module = module;
                }
                else
                {
                    Debug.LogError(
                        $"[GameBootstrap] Shader prefab '{def.prefab.name}' lacks ShaderModule or Renderer component");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameBootstrap] Shader module instantiation failed for '{def.moduleName}': {e.Message}");
            }
        }

        /// <summary>
        /// Object3DNodeにプレハブインスタンスとObject3DProxyを注入する。
        /// </summary>
        private void InstantiateObject3D(Object3DNode node)
        {
            if (!_object3DPrefabMap.TryGetValue(node.PrefabName, out var prefab))
            {
                Debug.LogWarning($"[GameBootstrap] Object3D prefab not found: {node.PrefabName}");
                return;
            }

            try
            {
                var instance = Instantiate(prefab);
                instance.name = $"Object3D_{node.PrefabName}_{node.Id[..8]}";

                // プレイヤー前方にスポーン
                if (controllerInput != null)
                {
                    var headPos = _activeInput!.HeadPosition;
                    var headFwd = _activeInput!.HeadForward;
                    instance.transform.position = headPos + headFwd * 1.0f;
                }
                else
                {
                    instance.transform.position = node.Position;
                }

                _moduleInstances[node.Id] = instance;

                // Object3DProxy追加（なければ）
                var proxy = instance.GetComponent<Object3DProxy>();
                if (proxy == null)
                    proxy = instance.AddComponent<Object3DProxy>();

                // コライダー追加（なければ）
                if (instance.GetComponent<Collider>() == null)
                {
                    var meshFilter = instance.GetComponent<MeshFilter>();
                    if (meshFilter != null)
                        instance.AddComponent<MeshCollider>();
                    else
                        instance.AddComponent<BoxCollider>();
                }

                proxy.NodeId = node.Id;

                // グラブハンドラーに登録
                object3DGrabHandler?.Register(proxy);

                // R3 Observable購読を開始
                if (graphContext != null)
                    node.BindProxyObservables(graphContext.Context, proxy.Position, proxy.Scale);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameBootstrap] Object3D instantiation failed for '{node.PrefabName}': {e.Message}");
            }
        }

        /// <summary>
        /// CinemachineModuleからCinemachineノードのタイプとファクトリを登録する。
        /// 注意: ファクトリ未実装のため、タイプ登録もスキップする（メニューに表示しない）。
        /// </summary>
        private void RegisterCinemachineModules()
        {
            // TODO: CinemachineModuleNode用ファクトリ実装後にタイプ登録を有効化する。
            // ファクトリなしで_typeRegistryに登録するとメニューに表示されるが生成不可になる。
        }

        /// <summary>
        /// Object3DPrefabListからObject3Dノードのタイプとファクトリを動的登録する。
        /// </summary>
        private void RegisterObject3DTypes()
        {
            if (_typeRegistry == null || object3DPrefabs == null) return;

            foreach (var prefab in object3DPrefabs.Prefabs)
            {
                if (prefab == null) continue;

                var prefabName = prefab.name;
                var typeName = $"Object3D_{prefabName}";
                var capturedName = prefabName;

                _object3DPrefabMap[prefabName] = prefab;
                _typeRegistry.Register(new NodeTypeInfo(typeName, $"3D: {prefabName}", NodeCategory.Scene));

                Func<string, NodeBase> factory = id => new Object3DNode(id, capturedName);
                graphContext?.Context.RegisterNodeFactory(typeName, factory);
            }
        }

        private void InitializeSystems()
        {
            if (_typeRegistry == null) return;

            if (visualManager != null)
                visualManager.Initialize(_typeRegistry);

            if (audioDriver != null && graphContext != null)
                audioDriver.Initialize(graphContext);

            InitializeInteractionHandlers();
        }

        /// <summary>
        /// シーン上のSceneObjectBridgeを全検出し、対応するノードを自動生成する。
        /// </summary>
        private void RegisterSceneObjects()
        {
            if (_typeRegistry == null || graphContext == null || visualManager == null) return;

            // SceneObjectタイプをレジストリに登録（Sceneカテゴリ）
            _typeRegistry.Register(new NodeTypeInfo("SceneObject", "Scene Object", NodeCategory.Utility));

            // デシリアライズ用ファクトリ登録
            graphContext.Context.RegisterNodeFactory("SceneObject", id =>
                new SceneObjectNode(id, "Restored", true, true, true));

            var bridges = FindObjectsByType<SceneObjectBridge>(FindObjectsSortMode.None);
            foreach (var bridge in bridges)
            {
                try
                {
                    var nodeId = Guid.NewGuid().ToString();
                    var node = new SceneObjectNode(
                        nodeId, bridge.gameObject.name,
                        bridge.ExposePosition, bridge.ExposeRotation, bridge.ExposeScale);
                    node.SetTarget(bridge.transform);
                    bridge.NodeId = nodeId;

                    graphContext.Context.RegisterNode(node);

                    // 対象オブジェクトの上方にノードを生成
                    var spawnPos = bridge.transform.position + Vector3.up * 0.3f;
                    var visual = visualManager.CreateNodeVisual(node, spawnPos);

                    // プレイヤー方向を向ける
                    if (visual != null && controllerInput != null)
                    {
                        var headPos = _activeInput!.HeadPosition;
                        visual.transform.rotation = Quaternion.LookRotation(spawnPos - headPos);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameBootstrap] SceneObject setup failed for '{bridge.gameObject.name}': {e.Message}");
                }
            }
        }

        /// <summary>
        /// Week 5: MirrorOutput, AudioDevice, StatusPanel, SaveLoadを初期化・接続する。
        /// </summary>
        private void InitializeVerticalSliceSystems()
        {
            // GraphSaveLoadManager
            if (graphSaveLoad != null && graphContext != null)
            {
                graphSaveLoad.Initialize(graphContext);

                // ロード完了後にノード・エッジビジュアルを再構築
                graphSaveLoad.OnGraphLoaded += OnGraphLoaded;
            }

            // StatusPanelController
            if (statusPanel != null && graphContext != null)
                statusPanel.Initialize(graphContext);

            // CameraManagerPanelController
            if (cameraManagerPanel != null && graphContext != null)
            {
                cameraManagerPanel.Initialize(graphContext);
                // 編集モード中はエッジ接続・切断・ノード削除を一時無効化
                cameraManagerPanel.AddEditModeListener(isEditing =>
                {
                    edgeDragHandler?.SetEnabled(!isEditing);
                    edgeCutHandler?.SetEnabled(!isEditing);
                    nodeDeleteHandler?.SetEnabled(!isEditing);
                });
            }

            // MirrorOutputController → Spout/NDI
            var headTransform = desktopInput != null && desktopInput.gameObject.activeInHierarchy
                ? desktopInput.HeadTransform
                : controllerInput?.HeadTransform;
            if (mirrorOutput != null && headTransform != null)
            {
                mirrorOutput.Initialize(headTransform);
                mirrorOutput.Activate();

                if (mirrorOutput.OutputTexture != null)
                {
                    spoutSender?.StartSending(mirrorOutput.OutputTexture);
                    ndiSender?.StartSending(mirrorOutput.OutputTexture);
                    desktopBlitter?.SetSource(mirrorOutput.OutputTexture);
                }
            }

            // CinemachinePreviewMonitor（デスクトップデバッグ時のみ）
            bool isDesktopMode = desktopInput != null && desktopInput.gameObject.activeInHierarchy;
            if (cinemachinePreview != null && isDesktopMode)
            {
                // CinemachinePreviewRig が非アクティブならアクティブ化
                var rig = cinemachinePreview.transform.root.gameObject;
                if (!rig.activeSelf)
                    rig.SetActive(true);

                cinemachinePreview.Initialize();
            }

            // AudioDeviceSelector → AudioAnalyzer
            InitializeAudioDeviceSelector();

            // Ableton OSC設定パネル＋クリップグリッド初期化
            InitializeAbletonOsc();
        }

        /// <summary>
        /// Ableton OSC設定パネルを表示し、Connect押下時にレイアウト問い合わせ＋
        /// グリッド生成を実行する。Skip時は単純にパネルを閉じる。
        /// PlayerPrefsへのhost/port保存はこの層で行う（UIはプリミティブのみ扱うため）。
        /// </summary>
        private void InitializeAbletonOsc()
        {
            if (abletonSetupPanel == null) return;

            var host = PlayerPrefs.GetString("abl.host", "127.0.0.1");
            var sendPort = PlayerPrefs.GetInt("abl.sendPort", 11000);
            var recvPort = PlayerPrefs.GetInt("abl.recvPort", 11001);
            abletonSetupPanel.SetInitialValues(host, sendPort, recvPort);

            abletonSetupPanel.OnConnectRequested += async (h, sp, rp) =>
            {
                try
                {
                    PlayerPrefs.SetString("abl.host", h);
                    PlayerPrefs.SetInt("abl.sendPort", sp);
                    PlayerPrefs.SetInt("abl.recvPort", rp);
                    PlayerPrefs.Save();

                    abletonSetupPanel.SetStatus("Connecting…", Color.yellow);
                    abletonLink?.Reconnect(h, sp, rp);

                    var ok = abletonBridge != null && await abletonBridge.QueryLayoutAsync();
                    if (!ok)
                        abletonSetupPanel.SetStatus("Timeout — empty grid", Color.red);

                    if (abletonGridManager != null)
                    {
                        if (abletonControlPanel != null)
                            abletonGridManager.SetSpacing(
                                abletonControlPanel.TrackHorizontalSpacing,
                                abletonControlPanel.SceneVerticalSpacing);

                        var (gridPos, gridRot) = ResolveGridPose();
                        abletonGridManager.SpawnGrid(gridPos, gridRot);
                        BuildControlPanel(gridPos, gridRot);
                        await PopulateMacrosAsync();
                        SpawnAbletonOuterFrame(gridPos, gridRot);
                    }

                    abletonSetupPanel.Hide();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameBootstrap] Ableton connect flow failed: {e.Message}");
                    abletonSetupPanel.SetStatus($"Error: {e.Message}", Color.red);
                }
            };

            abletonSetupPanel.OnSkipRequested += () => abletonSetupPanel.Hide();

            if (abletonGridManager != null && abletonBridge != null)
                abletonGridManager.Initialize(abletonBridge);

            if (clipFireHandler != null && _activeInput != null
                && sharedRaycastService != null && abletonLink != null)
            {
                clipFireHandler.Initialize(_activeInput, sharedRaycastService, abletonLink);
            }

            WireControlPanelEvents();

            // 起動時1回だけ表示。アンカーがあればそこ、無ければプレイヤー前方
            ShowSetupPanel();
        }

        private void ShowSetupPanel()
        {
            if (abletonSetupPanel == null) return;

            if (abletonUiAnchor != null)
            {
                abletonSetupPanel.ShowAt(abletonUiAnchor.position, abletonUiAnchor.rotation);
            }
            else if (_activeInput != null)
            {
                abletonSetupPanel.Show(_activeInput.HeadPosition, _activeInput.HeadForward);
            }
        }

        private (Vector3 pos, Quaternion rot) ResolveGridPose()
        {
            if (abletonUiAnchor != null)
                return (abletonUiAnchor.position, abletonUiAnchor.rotation);

            if (_activeInput != null)
            {
                var pos = _activeInput.HeadPosition + _activeInput.HeadForward * 0.8f;
                var rot = Quaternion.LookRotation(pos - _activeInput.HeadPosition);
                return (pos, rot);
            }

            return (Vector3.zero, Quaternion.identity);
        }

        private void WireControlPanelEvents()
        {
            if (abletonControlPanel == null) return;

            abletonControlPanel.OnMasterVolumeChanged += v =>
                abletonLink?.Send("/live/master/set/volume", v);
            abletonControlPanel.OnTempoChanged += v =>
                abletonLink?.Send("/live/song/set/tempo", v);
            abletonControlPanel.OnTrackVolumeChanged += (t, v) =>
                abletonLink?.SendIntFloat("/live/track/set/volume", t, v);
            abletonControlPanel.OnTrackStopRequested += t =>
                abletonLink?.Send("/live/track/stop_all_clips", t);
            abletonControlPanel.OnPlayRequested += () =>
                abletonLink?.Send("/live/song/start_playing");
            abletonControlPanel.OnStopRequested += () =>
                abletonLink?.Send("/live/song/stop_playing");
            abletonControlPanel.OnMacroChanged += (macroIdx, value) =>
                SendMacroValue(macroIdx, value);
            abletonControlPanel.OnMacroTargetChangeRequested += (dt, dd) =>
                _ = RebindMacrosAsync(dt, dd);
        }

        /// <summary>
        /// Macro 対象 Track/Device を delta ぶんずらして再構築する。
        /// Track index は -1 (Master) を含む循環: -1 → 0 → ... → NumTracks-1 → -1。
        /// Device index は負を 0 にクランプ (上限取得は省略)。
        /// </summary>
        private async Task RebindMacrosAsync(int trackDelta, int deviceDelta)
        {
            if (abletonBridge == null || abletonControlPanel == null) return;

            var newTrack = macroTrackIndex + trackDelta;
            var newDevice = Mathf.Max(0, macroDeviceIndex + deviceDelta);

            // Track 範囲循環: -1 (Master) と 0..NumTracks-1
            var numTracks = abletonBridge.NumTracks;
            if (numTracks > 0)
            {
                if (newTrack < -1) newTrack = numTracks - 1;
                else if (newTrack >= numTracks) newTrack = -1;
            }
            else if (newTrack < -1)
            {
                newTrack = -1;
            }

            // 既存 listener を停止
            UnsubscribeMacroValueListener();

            macroTrackIndex = newTrack;
            macroDeviceIndex = newDevice;
            abletonControlPanel.SetMacroTargetLabel(newTrack, newDevice);

            // 新ターゲットで再 Query → 再 Build → 再 Subscribe
            await PopulateMacrosAsync();
        }

        /// <summary>
        /// 現在の Macro 対象に対する parameter/value listener を解除する。
        /// stop_listen を Live に送信し、ローカル Subscribe を Dispose。
        /// </summary>
        private void UnsubscribeMacroValueListener()
        {
            if (abletonLink != null && abletonBridge != null)
            {
                foreach (var m in abletonBridge.Macros)
                {
                    abletonLink.SendInt3(
                        "/live/device/stop_listen/parameter/value",
                        macroTrackIndex, macroDeviceIndex, m.ParamId);
                }
            }

            _macroValueListenerSub?.Dispose();
            _macroValueListenerSub = null;
        }

        /// <summary>
        /// AbletonControlPanel の Macro Knob 操作 → /live/device/set/parameter/value 送信。
        /// macroIdx は 0 始まり、ParamId に +1 して送る (0 = Device On はスキップ)。
        /// </summary>
        private void SendMacroValue(int macroIdx, float value)
        {
            if (abletonLink == null) return;
            var paramId = macroIdx + 1;
            abletonLink.SendInt3Float(
                "/live/device/set/parameter/value",
                macroTrackIndex, macroDeviceIndex, paramId, value);
        }

        /// <summary>
        /// Macro メタを Bridge から取得 → ControlPanel にセット → start_listen で双方向同期を確立。
        /// 失敗しても他の UI は動くよう例外を握る。
        /// </summary>
        private async Task PopulateMacrosAsync()
        {
            if (abletonBridge == null || abletonControlPanel == null) return;

            var count = abletonControlPanel.MacroCount;

            try
            {
                await abletonBridge.QueryMacrosAsync(macroTrackIndex, macroDeviceIndex, count);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameBootstrap] QueryMacrosAsync failed: {e.Message}");
                return;
            }

            var macros = abletonBridge.Macros;
            if (macros == null || macros.Length == 0) return;

            var names = new string[macros.Length];
            var values = new float[macros.Length];
            var mins = new float[macros.Length];
            var maxs = new float[macros.Length];
            for (var i = 0; i < macros.Length; i++)
            {
                names[i] = macros[i].Name ?? string.Empty;
                values[i] = macros[i].Value;
                mins[i] = macros[i].Min;
                maxs[i] = macros[i].Max;
            }

            abletonControlPanel.BuildMacroStrip(names, values, mins, maxs);
            abletonControlPanel.SetMacroTargetLabel(macroTrackIndex, macroDeviceIndex);
            SubscribeMacroValueListener(macros);
        }

        /// <summary>
        /// /live/device/get/parameter/value への listen を全 Macro 分張り、
        /// 応答が来たら ControlPanel.SetMacroValue で UI に反映 (Push やオートメーション対応)。
        /// </summary>
        private void SubscribeMacroValueListener(AbletonMacroMeta[] macros)
        {
            _macroValueListenerSub?.Dispose();
            _macroValueListenerSub = null;

            if (abletonLink == null || abletonControlPanel == null) return;

            // 各 Macro に start_listen
            foreach (var m in macros)
            {
                abletonLink.SendInt3(
                    "/live/device/start_listen/parameter/value",
                    macroTrackIndex, macroDeviceIndex, m.ParamId);
            }

            // ParamId → macroIdx の逆引きを構築
            var paramIdToIdx = new Dictionary<int, int>(macros.Length);
            for (var i = 0; i < macros.Length; i++)
                paramIdToIdx[macros[i].ParamId] = i;

            _macroValueListenerSub = abletonLink
                .GetAddressObservable("/live/device/get/parameter/value")
                .Subscribe(msg =>
                {
                    if (msg.IntArgs.Length < 3) return;
                    if (msg.IntArgs[0] != macroTrackIndex || msg.IntArgs[1] != macroDeviceIndex) return;
                    var paramId = msg.IntArgs[2];
                    if (!paramIdToIdx.TryGetValue(paramId, out var idx)) return;

                    var v = msg.FloatArgs.Length > 3 ? msg.FloatArgs[3] : 0f;
                    abletonControlPanel.SetMacroValue(idx, v);
                });
        }

        private void BuildControlPanel(Vector3 gridOrigin, Quaternion facing)
        {
            if (abletonControlPanel == null || abletonBridge == null) return;

            var tracks = abletonBridge.Tracks;
            if (tracks == null || tracks.Length == 0) return;

            var hSpacing = abletonControlPanel.TrackHorizontalSpacing;
            var vSpacing = abletonControlPanel.SceneVerticalSpacing;
            var trackNames = new string[tracks.Length];
            for (var i = 0; i < tracks.Length; i++)
                trackNames[i] = tracks[i].Name ?? string.Empty;

            var panelWidth = Mathf.Max(0.4f, tracks.Length * hSpacing);
            var panelHeight = panelWidth * AbletonControlPanel.TextureAspectRatio;
            var rightAxis = facing * Vector3.right;
            var upAxis = facing * Vector3.up;
            var centerX = (tracks.Length - 1) * 0.5f * hSpacing;
            var verticalOffset = panelHeight * 0.5f + vSpacing * 0.6f;
            var panelPos = gridOrigin + rightAxis * centerX - upAxis * verticalOffset;

            abletonControlPanel.Build(trackNames, panelPos, facing, panelWidth);
        }

        /// <summary>
        /// グリッド + コントロールパネル全体を囲む角丸フレームを生成する。
        /// マテリアル未設定時はスキップ。再Connect時は前のインスタンスを破棄。
        /// </summary>
        private void SpawnAbletonOuterFrame(Vector3 gridOrigin, Quaternion facing)
        {
            if (abletonOuterFrameMaterial == null) return;
            if (abletonBridge == null || abletonControlPanel == null) return;

            var tracks = abletonBridge.Tracks;
            if (tracks == null || tracks.Length == 0) return;

            var hSpacing = abletonControlPanel.TrackHorizontalSpacing;
            var vSpacing = abletonControlPanel.SceneVerticalSpacing;
            var controlPanelGap = vSpacing * 0.6f;

            var numTracks = tracks.Length;
            var numScenes = abletonBridge.NumScenes;

            // グリッドのローカル範囲 (origin が左下)
            var gridWidth = (numTracks - 1) * hSpacing + hSpacing;     // 左右余白込み
            var gridHeight = (numScenes - 1) * vSpacing + vSpacing;
            var gridCenterX = (numTracks - 1) * 0.5f * hSpacing;
            var gridCenterY = (numScenes - 1) * 0.5f * vSpacing;

            // コントロールパネル領域 (グリッド下、横はグリッド中心揃え)
            var ctrlWidth = Mathf.Max(0.4f, numTracks * hSpacing);
            var ctrlHeight = ctrlWidth * AbletonControlPanel.TextureAspectRatio;
            var ctrlCenterY = -controlPanelGap - ctrlHeight * 0.5f;

            // 全体バウンディング
            var totalLeft = Mathf.Min(-hSpacing * 0.5f, gridCenterX - ctrlWidth * 0.5f);
            var totalRight = Mathf.Max(gridCenterX + gridWidth * 0.5f, gridCenterX + ctrlWidth * 0.5f);
            var totalTop = gridCenterY + gridHeight * 0.5f;
            var totalBottom = ctrlCenterY - ctrlHeight * 0.5f;

            var bboxWidth = totalRight - totalLeft + abletonOuterFramePadding * 2f;
            var bboxHeight = totalTop - totalBottom + abletonOuterFramePadding * 2f;
            var bboxCenterLocal = new Vector3(
                (totalLeft + totalRight) * 0.5f,
                (totalTop + totalBottom) * 0.5f,
                abletonOuterFrameDepthOffset);

            if (_abletonOuterFrameInstance != null)
                Destroy(_abletonOuterFrameInstance);

            var frame = GameObject.CreatePrimitive(PrimitiveType.Quad);
            frame.name = "AbletonOuterFrame";
            var collider = frame.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            // gridOrigin/facing をワールドアンカーとし、ローカルオフセットを加える
            var worldCenter = gridOrigin + facing * bboxCenterLocal;
            frame.transform.SetPositionAndRotation(worldCenter, facing);
            frame.transform.localScale = new Vector3(bboxWidth, bboxHeight, 1f);

            var renderer = frame.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // Z 値が近接するため明示的に SortingOrder を負にして必ずクリップより手前ではなく
            // 後ろに描画 (Transparent Queue 内では SortingOrder が距離より優先される)
            renderer.sortingOrder = -10;

            var matInstance = new Material(abletonOuterFrameMaterial);
            // フレーム自身の Render Queue も明示的に下げる (Transparent=3000 → 2990)
            matInstance.renderQueue = 2990;
            matInstance.SetVector("_RectSize", new Vector4(bboxWidth, bboxHeight, 0f, 0f));
            matInstance.SetFloat("_CornerRadius", abletonOuterFrameCornerRadius);
            renderer.sharedMaterial = matInstance;

            _abletonOuterFrameInstance = frame;
        }

        private void InitializeAudioDeviceSelector()
        {
            if (audioDeviceSelector == null || audioDriver?.Analyzer == null) return;

            var analyzer = audioDriver.Analyzer;
            audioDeviceSelector.Initialize(analyzer.AvailableDevices, analyzer.CurrentDevice);

            _onDeviceSelected = deviceName =>
            {
                analyzer.Initialize(deviceName);
                audioDeviceSelector.SetCurrentDevice(analyzer.CurrentDevice);
                statusPanel?.SetAudioDevice(deviceName);
            };
            audioDeviceSelector.OnDeviceSelected += _onDeviceSelected;

            _onRefreshRequested = () =>
            {
                audioDeviceSelector.UpdateDeviceList(analyzer.AvailableDevices);
            };
            audioDeviceSelector.OnRefreshRequested += _onRefreshRequested;

            // 初期デバイスがあればステータスパネルに反映
            if (analyzer.CurrentDevice != null)
                statusPanel?.SetAudioDevice(analyzer.CurrentDevice);
        }

        private void InitializeInteractionHandlers()
        {
            // VR / デスクトップ入力ルーター切替
            bool isDesktop = desktopInput != null && desktopInput.gameObject.activeInHierarchy;

            if (isDesktop)
            {
                _activeInput = desktopInput;
                // VR入力ルーターを無効化（競合防止）
                if (controllerInput != null) controllerInput.enabled = false;
                Debug.Log("[GameBootstrap] Desktop debug mode active");
            }
            else
            {
                _activeInput = controllerInput;
            }

            if (_activeInput == null)
            {
                Debug.LogError("[GameBootstrap] No input router available!");
                return;
            }

            if (sharedRaycastService == null)
                Debug.LogError("[GameBootstrap] sharedRaycastService is not assigned!");

            var input = _activeInput;
            IRayProvider rayProvider = (IRayProvider)input;
            IControllerPose controllerPose = (IControllerPose)input;
            ILeftHandRay leftHandRay = (ILeftHandRay)input;
            ILeftHandInput leftHandInput = (ILeftHandInput)input;

            // 共有レイキャストサービスの初期化（全ハンドラの前に）
            if (sharedRaycastService != null)
                sharedRaycastService.Initialize(rayProvider);

            if (edgeVisualManager != null && visualManager != null)
                edgeVisualManager.Initialize(visualManager);

            if (edgeDragHandler != null && visualManager != null &&
                graphContext != null && edgeVisualManager != null &&
                sharedRaycastService != null)
            {
                edgeDragHandler.Initialize(
                    rayProvider, input, visualManager,
                    graphContext, edgeVisualManager, sharedRaycastService);

                // グラブ中はエッジ接続をスキップ
                if (nodeGrabHandler != null)
                    edgeDragHandler.SetGrabbingCheck(() => nodeGrabHandler.IsGrabbing);
            }

            if (edgeCutHandler != null && edgeVisualManager != null && graphContext != null)
            {
                edgeCutHandler.Initialize(input, rayProvider, edgeVisualManager, graphContext);
            }

            if (nodeDeleteHandler != null && visualManager != null &&
                graphContext != null && edgeVisualManager != null &&
                sharedRaycastService != null)
            {
                nodeDeleteHandler.Initialize(
                    input, sharedRaycastService, visualManager,
                    graphContext, edgeVisualManager);
                nodeDeleteHandler.SetDeleteDependencies(edgeDragHandler, this);
            }

            if (nodeGrabHandler != null && visualManager != null &&
                sharedRaycastService != null && edgeVisualManager != null)
            {
                nodeGrabHandler.Initialize(
                    input, controllerPose, leftHandRay, leftHandInput,
                    sharedRaycastService, visualManager, edgeVisualManager);
            }

            if (pathControlPointGrabHandler != null && pathEditorManager != null &&
                sharedRaycastService != null)
            {
                pathControlPointGrabHandler.Initialize(
                    input, controllerPose, sharedRaycastService, pathEditorManager);
            }

            if (object3DGrabHandler != null)
            {
                // Turn入力: VRならControllerInputRouter.OnTurnInput、デスクトップならDesktopInputRouter.OnTurnInput
                Observable<Vector2> turnInput = isDesktop
                    ? desktopInput!.OnTurnInput
                    : controllerInput!.OnTurnInput;
                object3DGrabHandler.Initialize(
                    input, controllerPose, leftHandRay, leftHandInput, turnInput);
            }

            if (uiRaycastDriver != null && sharedRaycastService != null)
                uiRaycastDriver.Initialize(input, sharedRaycastService);

            // 巻物メニューの初期化
            if (scrollMenuVisual != null && _typeRegistry != null)
            {
                scrollMenuVisual.Initialize(_typeRegistry);

                // ノード選択イベント: GraphContextのファクトリでノード生成
                scrollMenuVisual.OnNodeTypeSelected += OnScrollMenuNodeSelected;
            }

            if (scrollMenuInteraction != null && scrollMenuVisual != null &&
                sharedRaycastService != null)
            {
                scrollMenuInteraction.Initialize(
                    input, leftHandRay, leftHandInput,
                    sharedRaycastService, scrollMenuVisual);

                if (isDesktop)
                    scrollMenuInteraction.SetDesktopMode(true);

                // メニューオープン中にエッジ接続を無効化するための連携
                if (edgeDragHandler != null)
                    scrollMenuInteraction.SetEdgeDragHandler(edgeDragHandler);

                // メニュー状態変更時にエッジ切断・ノード削除・クリップ発火も無効化
                scrollMenuInteraction.SetMenuStateCallback(isIdle =>
                {
                    edgeCutHandler?.SetEnabled(isIdle);
                    nodeDeleteHandler?.SetEnabled(isIdle);
                    clipFireHandler?.SetEnabled(isIdle);
                });
            }
        }

        private void OnGraphLoaded()
        {
            if (graphContext == null) return;

            var ctx = graphContext.Context;

            // 旧モジュールインスタンスを破棄（グラフ切替時のリーク防止）
            CleanupModuleInstances();

            // デシリアライズされたモジュールノードにPrefab・IPerformanceModuleを再注入
            ReinjectModulesAfterLoad(ctx);

            // ノードビジュアルを再構築
            visualManager?.RebuildAllVisuals(ctx);

            // エッジビジュアルを再構築
            edgeVisualManager?.RebuildAllEdgeVisuals(ctx);

            // ロード後、全ノードをプレイヤー方向に向ける
            if (visualManager != null && controllerInput != null)
            {
                var headPos = _activeInput!.HeadPosition;
                foreach (var node in ctx.Nodes.Values)
                {
                    var visual = visualManager.GetVisual(node.Id);
                    if (visual != null)
                    {
                        var pos = visual.transform.position;
                        visual.transform.rotation = Quaternion.LookRotation(pos - headPos);
                    }
                }
            }
        }

        /// <summary>
        /// グラフロード後、ModuleNodeBaseのModule未設定ノードにPrefabを再生成・注入する。
        /// ファクトリ経由のCreateNodeでは注入済みだが、デシリアライズ復元時は未設定のため。
        /// </summary>
        private void ReinjectModulesAfterLoad(GraphState ctx)
        {
            foreach (var node in ctx.Nodes.Values)
            {
                try
                {
                    switch (node)
                    {
                        case VFXModuleNode vfxNode:
                            InstantiateVFXModule(vfxNode, vfxNode.Definition);
                            break;
                        case ShaderModuleNode shaderNode:
                            InstantiateShaderModule(shaderNode, shaderNode.Definition);
                            break;
                        case SceneSwitchNode sceneNode:
                            sceneNode.Loader = sceneLoader;
                            break;
                        case SceneTriggerNode triggerNode:
                            triggerNode.Loader = sceneLoader;
                            break;
                        case Object3DNode object3DNode:
                            InstantiateObject3D(object3DNode);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[GameBootstrap] Module re-injection failed for node '{node.Id}': {e.Message}");
                }
            }
        }

        /// <summary>
        /// 全モジュールPrefabインスタンスを破棄する。
        /// </summary>
        private void CleanupModuleInstances()
        {
            foreach (var kvp in _moduleInstances)
            {
                if (kvp.Value != null)
                {
                    // Object3DProxyの場合はグラブハンドラーから登録解除
                    var proxy = kvp.Value.GetComponent<Object3DProxy>();
                    if (proxy != null)
                        object3DGrabHandler?.Unregister(proxy);

                    Destroy(kvp.Value);
                }
            }
            _moduleInstances.Clear();
        }

        private void OnDestroy()
        {
            // イベント購読解除
            if (graphSaveLoad != null)
                graphSaveLoad.OnGraphLoaded -= OnGraphLoaded;

            if (scrollMenuVisual != null)
                scrollMenuVisual.OnNodeTypeSelected -= OnScrollMenuNodeSelected;

            // AudioDeviceSelector イベント購読解除
            if (audioDeviceSelector != null)
            {
                if (_onDeviceSelected != null)
                    audioDeviceSelector.OnDeviceSelected -= _onDeviceSelected;
                if (_onRefreshRequested != null)
                    audioDeviceSelector.OnRefreshRequested -= _onRefreshRequested;
            }

            // Ableton Macro listener 解除
            _macroValueListenerSub?.Dispose();
            _macroValueListenerSub = null;

            // モジュールPrefabインスタンスの破棄
            CleanupModuleInstances();
        }

        private void OnScrollMenuNodeSelected(string nodeType)
        {
            if (graphContext == null || visualManager == null || controllerInput == null) return;

            Debug.Log($"[GameBootstrap] OnScrollMenuNodeSelected: {nodeType}");

            var ctx = graphContext.Context;
            var node = ctx.CreateNode(nodeType);
            if (node == null)
            {
                Debug.LogWarning($"[GameBootstrap] ScrollMenu: failed to create node '{nodeType}'");
                return;
            }

            // ノード生成後にスクロールメニューを閉じてIdle状態に戻す
            scrollMenuInteraction?.CloseMenu();

            var headPos = _activeInput!.HeadPosition;
            var spawnPos = headPos + _activeInput!.HeadForward * 0.3f;
            node.Position = spawnPos;

            // モジュールノードの場合、Prefabインスタンスを生成してModule注入
            InjectModuleIfNeeded(node);

            Debug.Log($"[GameBootstrap] Node created: {node.Id} type={node.NodeType} inputPorts={node.InputPorts.Count}");

            ctx.RegisterNode(node);
            var visual = visualManager.CreateNodeVisual(node, spawnPos);
            if (visual != null)
                visual.transform.rotation = Quaternion.LookRotation(spawnPos - headPos);

            // 入力ポートにConst/Toggleを自動生成+プリコネクト
            SpawnInputNodes(node, spawnPos, headPos);

            Debug.Log($"[GameBootstrap] Node setup complete: {node.NodeType}");
        }

        /// <summary>
        /// ノードの全入力ポートに対してConst/Toggleノードを自動生成・プリコネクトする。
        /// 全ノード共通（モジュールノード・通常ノード問わず）。
        /// </summary>
        private void SpawnInputNodes(NodeBase targetNode, Vector3 nodePos, Vector3 headPos)
        {
            if (graphContext == null || visualManager == null || edgeVisualManager == null) return;

            var ctx = graphContext.Context;
            var inputPorts = targetNode.InputPorts;
            if (inputPorts.Count == 0) return;

            // モジュールノードのイベントポート（OnPlay等）はスキップ（ユーザーが手動接続）
            var moduleNode = targetNode as ModuleNodeBase;

            // スキップ対象を除いたポート数でレイアウト計算
            var portCount = 0;
            foreach (var kvp in inputPorts)
            {
                if (moduleNode != null && moduleNode.Definition.IsEvent(kvp.Key)) continue;
                portCount++;
            }
            if (portCount == 0) return;

            var nodeForward = (nodePos - headPos).normalized;
            var nodeRight = Vector3.Cross(Vector3.up, nodeForward).normalized;
            var startOffset = nodePos - nodeRight * 0.35f;
            var verticalSpacing = 0.18f;
            var topY = startOffset.y + (portCount - 1) * verticalSpacing * 0.5f;
            var slotIndex = 0;

            foreach (var kvp in inputPorts)
            {
                var portName = kvp.Key;
                var portType = kvp.Value.Type;

                // モジュールノードのイベントポートはスキップ
                if (moduleNode != null && moduleNode.Definition.IsEvent(portName))
                    continue;

                NodeBase? sourceNode = null;
                switch (portType)
                {
                    case ParamType.Float:
                        sourceNode = new ConstFloatNode(Guid.NewGuid().ToString());
                        break;
                    case ParamType.Color:
                        sourceNode = new ConstColorNode(Guid.NewGuid().ToString());
                        break;
                    case ParamType.Bool:
                        sourceNode = new ToggleNode(Guid.NewGuid().ToString());
                        break;
                }

                if (sourceNode == null) continue;

                var pos = new Vector3(
                    startOffset.x,
                    topY - slotIndex * verticalSpacing,
                    startOffset.z);
                sourceNode.Position = pos;

                ctx.RegisterNode(sourceNode);
                var visual = visualManager.CreateNodeVisual(sourceNode, pos);
                if (visual != null)
                    visual.transform.rotation = Quaternion.LookRotation(pos - headPos);

                var outputPort = sourceNode is ToggleNode ? "State" : "Value";
                if (ctx.TryConnect(sourceNode.Id, outputPort, targetNode.Id, portName))
                {
                    // 接続後に初期値を再発行（Subject<T>はリプレイしないため）
                    if (sourceNode is ConstFloatNode constFloat)
                        constFloat.Value = constFloat.Value;
                    else if (sourceNode is ConstColorNode constColor)
                        constColor.Value = constColor.Value;

                    var edges = ctx.Edges;
                    for (var j = edges.Count - 1; j >= 0; j--)
                    {
                        var edge = edges[j];
                        if (edge.FromNodeId == sourceNode.Id && edge.FromPort == outputPort &&
                            edge.ToNodeId == targetNode.Id && edge.ToPort == portName)
                        {
                            edgeVisualManager.CreateEdgeVisual(edge, portType);
                            break;
                        }
                    }
                }

                // ToggleノードにはTriggerノードを追加生成してプリコネクト
                if (sourceNode is ToggleNode toggleNode)
                {
                    var triggerNode = new TriggerNode(Guid.NewGuid().ToString());
                    var triggerPos = pos - nodeRight * 0.3f;
                    triggerNode.Position = triggerPos;

                    ctx.RegisterNode(triggerNode);
                    var triggerVisual = visualManager.CreateNodeVisual(triggerNode, triggerPos);
                    if (triggerVisual != null)
                        triggerVisual.transform.rotation = Quaternion.LookRotation(triggerPos - headPos);

                    if (ctx.TryConnect(triggerNode.Id, "Trigger", toggleNode.Id, "Trigger"))
                    {
                        var triggerEdges = ctx.Edges;
                        for (var k = triggerEdges.Count - 1; k >= 0; k--)
                        {
                            var te = triggerEdges[k];
                            if (te.FromNodeId == triggerNode.Id && te.FromPort == "Trigger" &&
                                te.ToNodeId == toggleNode.Id && te.ToPort == "Trigger")
                            {
                                edgeVisualManager.CreateEdgeVisual(te, ParamType.Bool);
                                break;
                            }
                        }
                    }
                }

                slotIndex++;
            }
        }

        /// <summary>
        /// ノードがModuleNodeBaseの場合、対応するPrefabをInstantiateしてModuleを注入する。
        /// メニュー選択時の単発生成用。デシリアライズ時はReinjectModulesAfterLoadが担当。
        /// </summary>
        private void InjectModuleIfNeeded(NodeBase node)
        {
            switch (node)
            {
                case VFXModuleNode vfxNode:
                    InstantiateVFXModule(vfxNode, vfxNode.Definition);
                    break;
                case ShaderModuleNode shaderNode:
                    InstantiateShaderModule(shaderNode, shaderNode.Definition);
                    break;
                case SceneSwitchNode sceneNode:
                    sceneNode.Loader = sceneLoader;
                    break;
                case SceneTriggerNode triggerNode:
                    triggerNode.Loader = sceneLoader;
                    break;
                case Object3DNode object3DNode:
                    InstantiateObject3D(object3DNode);
                    break;
            }
        }

        /// <summary>
        /// 指定ノードのモジュールPrefabインスタンスを破棄する。ノード削除時に呼ぶ。
        /// </summary>
        public void DestroyModuleInstance(string nodeId)
        {
            if (!_moduleInstances.TryGetValue(nodeId, out var instance)) return;

            _moduleInstances.Remove(nodeId);
            if (instance != null)
            {
                var proxy = instance.GetComponent<Object3DProxy>();
                if (proxy != null)
                    object3DGrabHandler?.Unregister(proxy);

                Destroy(instance);
            }
        }
    }
}
