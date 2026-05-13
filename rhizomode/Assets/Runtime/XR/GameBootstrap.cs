#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using R3;
using Rhizomode.Audio.Analysis;
using Rhizomode.Audio.GraphAdapter;
using Rhizomode.Cameras;
using Rhizomode.Graph.Serialization;
using Rhizomode.Persistence.Json;
using Rhizomode.Scene.GraphAdapter;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using Rhizomode.Ableton.Transport;
using Rhizomode.Ableton.Session;
using Rhizomode.Ableton.GraphAdapter;
using Rhizomode.Nodes.Ableton;
using Rhizomode.Nodes.OscMidi;
using Rhizomode.Nodes.Audio;
using Rhizomode.Nodes.Scene;
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
using Rhizomode.Input.XR;
using Rhizomode.Input.Desktop;
using Rhizomode.Scene.Contracts;
using Rhizomode.Scene.Runtime;
using Rhizomode.Interaction;

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

        /// <summary>Object3Dプレハブ名→元プレハブの逆引き。Instantiate用。</summary>
        private readonly Dictionary<string, GameObject> _object3DPrefabMap = new();

        /// <summary>
        /// Phase 5 Round E で WireIntentSink が作る EventBus。Subject<T> 5 件を保持するため
        /// OnDestroy で Dispose 必須 (Codex review fix #8)。Phase 8 で VContainer Installer に
        /// 移行したら本フィールドは削除される。
        /// </summary>
        private Rhizomode.Graph.Events.GraphEventBus? _phase5EventBus;

        /// <summary>
        /// Phase 7 Round B: GraphSaveLoadManager + WireIntentSink で共有する Composite factory。
        /// Awake 後の最初の呼び出し時に lazy 構築。Phase 8 で VContainer Installer に移行予定。
        /// </summary>
        private Rhizomode.Graph.CatalogBridge.INodeFactory? _sharedFactory;

        /// <summary>
        /// Phase 6 Round A: Module/Object3D の Prefab instantiation + IPerformanceModule 注入を
        /// 担当する LifecycleProcessor。InstantiateVFXModule / InstantiateShaderModule /
        /// InstantiateObject3D / DestroyModuleInstance / CleanupModuleInstances は本 processor に集約。
        /// Phase 8 Round B で NodeRuntime の processors リストに登録 — AfterSetup 自動駆動化。
        /// </summary>
        private ModuleLifecycleProcessor? _moduleProcessor;

        /// <summary>
        /// Phase 6 Round B: ISceneLoaderConsumer に ISceneLoader を注入する LifecycleProcessor。
        /// Phase 8 Round B で NodeRuntime の processors リストに登録 — BeforeSetup 自動駆動化。
        /// </summary>
        private SceneLoaderLifecycleProcessor? _sceneLoaderProcessor;

        /// <summary>
        /// Phase 8 Round B: GraphState ミューテーション (RegisterNode / AddEdge) の唯一窓口。
        /// Awake 時に eager 構築し、processors 経由で BeforeSetup → Setup → AfterSetup を駆動。
        /// 旧 ctx.RegisterNode / ctx.TryConnect 直接呼び出しを置換。
        /// </summary>
        private Rhizomode.Graph.Runtime.NodeRuntime? _nodeRuntime;

        /// <summary>
        /// Phase 8 Round C: ScrollMenu spawn + 入力ノード auto-spawn の graph mutation 部を集約。
        /// visual 創出は引き続き GameBootstrap が担当。
        /// </summary>
        private NodeSpawnService? _nodeSpawnService;

        /// <summary>
        /// Phase 8 Round D: SceneObjectBridge スキャン + SceneObjectNode 自動 spawn を集約。
        /// </summary>
        private SceneObjectRegistrationService? _sceneObjectService;

        // Phase 8 Round E: NodeFactoryMap + RegisterNodeTypes + RegisterFactories +
        // RegisterModuleTypes + RegisterObject3DTypes は NodeRegistrationOrchestrator に移送済 (F-8.2 抽出 3/N)。

        private void Awake()
        {
            _typeRegistry = new NodeTypeRegistry();

            // Phase 8 Round E: 全 type/factory 登録を NodeRegistrationOrchestrator に委譲 (F-8.2 抽出 3/N)。
            if (graphContext != null)
            {
                var registrationOrchestrator = new NodeRegistrationOrchestrator(
                    _typeRegistry, graphContext.Context, moduleDefinitions, object3DPrefabs);
                registrationOrchestrator.RegisterAll();

                // Object3D prefab map は ModuleLifecycleProcessor の dependency として利用
                foreach (var kvp in registrationOrchestrator.Object3DPrefabMap)
                    _object3DPrefabMap[kvp.Key] = kvp.Value;
            }

            // Phase 6 Round A: ModuleLifecycleProcessor を初期化
            // (RegisterObject3DTypes で _object3DPrefabMap が populate された後)。
            _moduleProcessor = new ModuleLifecycleProcessor(
                _object3DPrefabMap,
                new BootstrapModulePlacement(this),
                new BootstrapObject3DRegistry(this));

            // Phase 6 Round B: SceneLoaderLifecycleProcessor を初期化。
            // sceneLoader は [SerializeField] で MonoBehaviour に注入される。
            _sceneLoaderProcessor = new SceneLoaderLifecycleProcessor(sceneLoader);

            // Phase 8 Round B: NodeRuntime を eager 構築。EventBus + factory も lift して field 化。
            // processors 順序: SceneLoaderLifecycleProcessor (BeforeSetup で Loader 注入) →
            //                  ModuleLifecycleProcessor (AfterSetup で Prefab + Module 注入)
            // この設計で ScrollMenu Spawn / SceneObject 自動登録 / SpawnInputNodes 全てが
            // 自動的に Lifecycle を駆動する (旧来の手動 InjectModuleIfNeeded 呼び出しを撤廃)。
            if (graphContext != null)
            {
                EnsureSharedFactoryAndEventBus();
                _nodeRuntime = new Rhizomode.Graph.Runtime.NodeRuntime(
                    graphContext.Context, _phase5EventBus!,
                    new Rhizomode.Graph.Runtime.INodeLifecycleProcessor[]
                    {
                        _sceneLoaderProcessor, _moduleProcessor
                    });

                // Phase 8 Round C: NodeSpawnService を初期化 (Plan v5.3 F-8.2 抽出 1/N)。
                _nodeSpawnService = new NodeSpawnService(graphContext.Context, _nodeRuntime);

                // Phase 8 Round D: SceneObjectRegistrationService を初期化 (F-8.2 抽出 2/N)。
                if (_typeRegistry != null)
                {
                    _sceneObjectService = new SceneObjectRegistrationService(
                        _typeRegistry, graphContext.Context, _nodeRuntime);
                }
            }

            InitializeSystems();
            InitializeVerticalSliceSystems();
            RegisterSceneObjects();
        }

        /// <summary>
        /// Phase 6 Round A 用 placement adapter。<see cref="_activeInput"/> から head pose を取り、
        /// FreshSpawn は head+forward*offset、Deserialize は node.Position を返す。
        /// VFX は offset=1.5、Object3D は offset=1.0 (旧コード保持の意図)。
        /// </summary>
        private sealed class BootstrapModulePlacement : IModulePlacementService
        {
            private readonly GameBootstrap _owner;
            public BootstrapModulePlacement(GameBootstrap owner) { _owner = owner; }

            public Vector3 GetSpawnPosition(NodeBase node, NodeInitMode mode)
            {
                if (mode != NodeInitMode.FreshSpawn || _owner._activeInput == null)
                    return node.Position;

                var headPos = _owner._activeInput.HeadPosition;
                var headFwd = _owner._activeInput.HeadForward;
                var offset = node is Object3DNode ? 1.0f : 1.5f;
                return headPos + headFwd * offset;
            }
        }

        /// <summary>
        /// Phase 6 Round A 用 Object3DProxy registry adapter。<see cref="object3DGrabHandler"/> に転送する。
        /// </summary>
        private sealed class BootstrapObject3DRegistry : IObject3DProxyRegistry
        {
            private readonly GameBootstrap _owner;
            public BootstrapObject3DRegistry(GameBootstrap owner) { _owner = owner; }

            public void Register(Object3DProxy proxy) => _owner.object3DGrabHandler?.Register(proxy);
            public void Unregister(Object3DProxy proxy) => _owner.object3DGrabHandler?.Unregister(proxy);
        }

        /// <summary>
        /// Phase 6 Round A: Object3D Proxy の Observable 購読のみここで実行。
        /// Prefab 生成 + IPerformanceModule 注入は <see cref="ModuleLifecycleProcessor"/> が担当。
        /// 本ヘルパーは <see cref="GraphState"/> 依存を Modules layer から切り離すために残置。
        /// </summary>
        private void BindObject3DProxyObservables(Object3DNode node)
        {
            if (_moduleProcessor == null || graphContext == null) return;
            if (!_moduleProcessor.Instances.TryGetValue(node.Id, out var instance)) return;
            if (instance == null) return;

            var proxy = instance.GetComponent<Object3DProxy>();
            if (proxy == null) return;

            node.BindProxyObservables(graphContext.Context, proxy.Position, proxy.Scale);
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
        /// シーン上の SceneObjectBridge を全検出し、対応するノードを自動生成する。
        /// Phase 8 Round D: graph 操作は SceneObjectRegistrationService に委譲、visual は本クラスで生成。
        /// </summary>
        private void RegisterSceneObjects()
        {
            if (visualManager == null || _sceneObjectService == null) return;

            _sceneObjectService.RegisterTypeAndFactory();

            var bridges = FindObjectsByType<SceneObjectBridge>(FindObjectsSortMode.None);
            var results = _sceneObjectService.RegisterBridges(bridges);

            foreach (var r in results)
            {
                var visual = visualManager.CreateNodeVisual(r.Node, r.SpawnPosition);
                if (visual != null && controllerInput != null)
                {
                    var headPos = _activeInput!.HeadPosition;
                    visual.transform.rotation = Quaternion.LookRotation(r.SpawnPosition - headPos);
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

                // Phase 7 Round B: Persistence + Serialization の依存を注入。
                // factory / eventBus / nodeRuntime / executor を構築する (WireIntentSink でも使う)。
                ConfigureSaveLoad();

                // Phase 8 Codex review fix #1+#3: load 開始時に旧 module instance を破棄
                // (OnGraphLoaded 内で CleanupAll すると Executor が attach した新 module まで巻き込む)。
                graphSaveLoad.OnGraphLoading += OnGraphLoadingHandler;
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
                // Phase 6 Round A: DestroyModuleInstance → _moduleProcessor.DestroyInstance に委譲
                nodeDeleteHandler.SetDeleteDependencies(edgeDragHandler,
                    _moduleProcessor != null ? _moduleProcessor.DestroyInstance : (Action<string>?)null);
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

            // Plan v5.3 Phase 5 Round E: SpatialIntentToCommandTranslator wiring。
            // 3 handler (EdgeDrag / EdgeCut / NodeDelete) を intent emit に切替。
            WireIntentSink();
        }

        private void WireIntentSink()
        {
            if (graphContext == null) return;

            EnsureSharedFactoryAndEventBus();
            var applier = new Rhizomode.Graph.Mutation.GraphMutationApplier(
                graphContext.Context, _sharedFactory!, _phase5EventBus!);
            var dispatcher = new Rhizomode.Graph.Mutation.GraphCommandDispatcher(applier);
            var translator = new Rhizomode.Interaction.GraphAdapter.SpatialIntentToCommandTranslator(dispatcher);

            edgeDragHandler?.SetIntentSink(translator);
            edgeCutHandler?.SetIntentSink(translator);
            nodeDeleteHandler?.SetIntentSink(translator);

            Debug.Log("[GameBootstrap] Phase 5 Round E: IntentSink wired up for 3 handlers.");
        }

        /// <summary>
        /// Phase 7 Round B: factory + EventBus を遅延生成 (WireIntentSink と ConfigureSaveLoad で共有)。
        /// </summary>
        private void EnsureSharedFactoryAndEventBus()
        {
            if (_sharedFactory == null)
            {
                var scanner = new Rhizomode.NodeCatalog.Runtime.NodeTypeAttributeScanner();
                var staticFactory = new Rhizomode.NodeCatalog.Runtime.AttributeScannerNodeFactory(scanner.Scan());
                _sharedFactory = new Rhizomode.NodeCatalog.Runtime.CompositeNodeFactory(
                    new Rhizomode.Graph.CatalogBridge.INodeFactory[] { staticFactory });
            }
            if (_phase5EventBus == null)
            {
                _phase5EventBus = new Rhizomode.Graph.Events.GraphEventBus();
            }
        }

        /// <summary>
        /// Phase 7 Round B: GraphSaveLoadManager に Persistence + Hydrator + Executor + Factory を注入。
        /// Phase 8 Round B: NodeRuntime は Awake で eager 構築済み (processors 含む) — 本メソッドは
        /// その _nodeRuntime を Executor に渡すだけ。
        /// </summary>
        private void ConfigureSaveLoad()
        {
            if (graphSaveLoad == null || graphContext == null || _nodeRuntime == null) return;

            var pathProvider = new JsonSavePathProvider();
            var repository = new JsonGraphRepository(pathProvider);
            var hydrator = new GraphHydrator();
            var executor = new HydrationPlanExecutor(_nodeRuntime);

            graphSaveLoad.Configure(repository, hydrator, executor, _sharedFactory!, pathProvider);

            Debug.Log("[GameBootstrap] Phase 7: SaveLoad configured (Repository + Hydrator + Executor).");
        }

        /// <summary>
        /// Phase 8 Codex review fix #1+#3: load 開始時に旧 module instance を破棄。
        /// この時点では Executor が新 module を attach する前なので、_moduleProcessor.Instances には
        /// 旧 graph の entry のみが存在する → 安全に全破棄できる。
        /// </summary>
        private void OnGraphLoadingHandler()
        {
            _moduleProcessor?.CleanupAll();
        }

        private void OnGraphLoaded()
        {
            if (graphContext == null) return;

            var ctx = graphContext.Context;

            // Phase 8 Round B: HydrationPlanExecutor が _nodeRuntime 経由で processors を自動駆動
            // (SceneLoaderLifecycleProcessor.BeforeSetup + ModuleLifecycleProcessor.AfterSetup) するため、
            // 旧 ReinjectModulesAfterLoad の手動 processor 呼び出しは不要。Object3D の GraphState 観測
            // bind のみ本クラス側で実施 (Module 層に GraphState 依存を持ち込まないため)。
            // 旧 module instance の破棄は OnGraphLoadingHandler (load 開始時) で完了済。
            foreach (var node in ctx.Nodes.Values)
            {
                if (node is Object3DNode object3DNode)
                    BindObject3DProxyObservables(object3DNode);
            }

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

        private void OnDestroy()
        {
            // イベント購読解除
            if (graphSaveLoad != null)
            {
                graphSaveLoad.OnGraphLoading -= OnGraphLoadingHandler;
                graphSaveLoad.OnGraphLoaded -= OnGraphLoaded;
            }

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

            // Phase 8: dispose 順序 — subscribers (processor) を先に dispose し、source (EventBus) を後に。
            // 将来 ModuleLifecycleProcessor が GraphEventBus.OnNodeRemoved を購読した場合に、
            // 逆順だと disposed Subject に処理が走る race を防ぐ。
            _moduleProcessor?.Dispose();
            _moduleProcessor = null;

            // Phase 5 Round E で構築した EventBus を解放 (Codex review fix #8)。
            // applier / dispatcher / translator は IDisposable ではないため EventBus のみで OK。
            _phase5EventBus?.Dispose();
            _phase5EventBus = null;
        }

        private void OnScrollMenuNodeSelected(string nodeType)
        {
            if (graphContext == null || visualManager == null || controllerInput == null) return;
            if (_nodeSpawnService == null)
            {
                Debug.LogError($"[GameBootstrap] OnScrollMenuNodeSelected aborted ({nodeType}) — _nodeSpawnService not initialized.");
                return;
            }

            Debug.Log($"[GameBootstrap] OnScrollMenuNodeSelected: {nodeType}");

            // Phase 8 Round C: graph mutation は NodeSpawnService に委譲、visual 創出はここで実行。
            var headPos = _activeInput!.HeadPosition;
            var headFwd = _activeInput!.HeadForward;
            var spawnResult = _nodeSpawnService.TrySpawnFromMenu(nodeType, headPos, headFwd);
            if (spawnResult == null) return;

            // ノード生成後にスクロールメニューを閉じる
            scrollMenuInteraction?.CloseMenu();

            // Object3D の Proxy 観測 bind (visual 創出と同層、GraphState 必要)
            if (spawnResult.Node is Object3DNode obj3d) BindObject3DProxyObservables(obj3d);

            // ノード visual を生成 (本クラスの責務、UI 層への副作用)
            var visual = visualManager.CreateNodeVisual(spawnResult.Node, spawnResult.Position);
            if (visual != null)
                visual.transform.rotation = Quaternion.LookRotation(spawnResult.Position - headPos);

            // 入力ポートに Const/Toggle/Trigger を auto-spawn + プリコネクト
            SpawnInputVisuals(spawnResult.Node, spawnResult.Position, headPos);

            Debug.Log($"[GameBootstrap] Node setup complete: {spawnResult.Node.NodeType}");
        }

        /// <summary>
        /// Phase 8 Round C: NodeSpawnService が返す InputSpawnResult から visual を生成する。
        /// graph mutation は service 側で完了済。
        /// </summary>
        private void SpawnInputVisuals(NodeBase targetNode, Vector3 nodePos, Vector3 headPos)
        {
            if (visualManager == null || edgeVisualManager == null || _nodeSpawnService == null) return;

            var results = _nodeSpawnService.SpawnInputNodes(targetNode, nodePos, headPos);
            foreach (var r in results)
            {
                // Source ノード (Const/Toggle) の visual
                var visual = visualManager.CreateNodeVisual(r.Source, r.SourcePosition);
                if (visual != null)
                    visual.transform.rotation = Quaternion.LookRotation(r.SourcePosition - headPos);

                // Source → target 間の edge visual (接続成功時のみ)
                if (r.PrimaryEdge != null)
                    edgeVisualManager.CreateEdgeVisual(r.PrimaryEdge, r.PortType);

                // Trigger ノードがあれば visual + edge visual
                if (r.TriggerNode != null)
                {
                    var triggerVisual = visualManager.CreateNodeVisual(r.TriggerNode, r.TriggerPosition);
                    if (triggerVisual != null)
                        triggerVisual.transform.rotation = Quaternion.LookRotation(r.TriggerPosition - headPos);

                    if (r.TriggerEdge != null)
                        edgeVisualManager.CreateEdgeVisual(r.TriggerEdge, ParamType.Bool);
                }
            }
        }

    }
}
