#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using R3;
using Rhizomode.Audio.Analysis;
using Rhizomode.Audio.GraphAdapter;
using Rhizomode.Observability.Runtime;
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
using Rhizomode.OscMidi.Transport;
using Rhizomode.OscMidi.GraphAdapter;
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
using Rhizomode.UI.Contracts;
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
    public partial class GameBootstrap : MonoBehaviour
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

        [Header("OSC / MIDI Transport")]
        [Tooltip("シーンに配置した OscServer。未設定なら OSC 入力ノードは非アクティブ。")]
        [SerializeField] private OscServer? oscServer;
        [Tooltip("シーンに配置した MidiServer。未設定なら MIDI 入力ノードは非アクティブ。")]
        [SerializeField] private MidiServer? midiServer;

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
        /// Phase 12B: IOscSourceConsumer / IMidiSourceConsumer に OSC/MIDI transport を注入する
        /// LifecycleProcessor。旧 OscServer.Instance / MidiServer.Instance singleton 直参照を解消。
        /// NodeRuntime の processors リストに登録され BeforeSetup で自動駆動。
        /// </summary>
        private OscMidiTransportLifecycleProcessor? _oscMidiTransportProcessor;

        /// <summary>
        /// Phase 12C: IAbletonLinkConsumer に AbletonLink を注入する LifecycleProcessor。
        /// 旧 AbletonLink.Instance singleton 直参照を解消。NodeRuntime の processors リストに
        /// 登録され BeforeSetup で自動駆動。
        /// </summary>
        private AbletonTransportLifecycleProcessor? _abletonTransportProcessor;

        /// <summary>
        /// Phase 12D: 各 system の IHealthMonitor を集約し、低頻度で Tick polling する。
        /// Audio / OSC / MIDI / Ableton の 4 monitor を Register。OnHealthChange の購読
        /// (StatusPanel 表示) は将来 Phase で配線予定。ITickable adapter 化は Phase 13
        /// (VContainer Installer) 待ち、現状は GameBootstrap.Update から駆動。
        /// </summary>
        private HealthAggregator? _healthAggregator;

        /// <summary>
        /// HealthAggregator.Tick の polling 間隔 (フレーム数)。CurrentSnapshot() が
        /// HealthSnapshot record を都度 alloc するため、毎フレームではなく低頻度で polling
        /// (90fps で ~30 frame ≒ 3Hz、Codex Phase 12 review MINOR の alloc 削減)。
        /// </summary>
        private const int HealthTickIntervalFrames = 30;
        private int _healthTickCounter;

        /// <summary>
        /// Phase 13C: HealthAggregator.OnHealthChange → StatusPanelController.SetHealth の
        /// 購読。OnDestroy で Dispose。
        /// </summary>
        private System.IDisposable? _healthSubscription;

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
        /// Phase 9 Round F1: グラフロード完了時の visual rebuild + プレイヤー方向回転を集約。
        /// </summary>
        private GraphLoadCoordinator? _graphLoadCoordinator;

        /// <summary>
        /// Phase 9 Round F2: ScrollMenu 選択時の visual 創出 + 入力ノード自動 spawn の visual 構築を集約。
        /// </summary>
        private MenuNodeSpawnCoordinator? _menuNodeSpawnCoordinator;

        /// <summary>
        /// Phase 8 Round D: SceneObjectBridge スキャン + SceneObjectNode 自動 spawn を集約。
        /// </summary>
        private SceneObjectRegistrationService? _sceneObjectService;

        /// <summary>
        /// Phase 8 Round F3: GraphAdapter (Translator + EventBus + Persistence) 統合 wiring。
        /// EnsureSharedFactoryAndEventBus + WireIntentSink + ConfigureSaveLoad の 3 ヘルパーを集約。
        /// </summary>
        private GraphAdapterWiring? _graphAdapterWiring;

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
            // Phase 9 prereq: 旧 private nested adapter classes (BootstrapModulePlacement /
            // BootstrapObject3DRegistry) を Rhizomode.Bootstrap asmdef に移送し、Func/Action
            // provider 経由で MonoBehaviour state を遅延解決する形に refactor (F-8.2, F-8.7 resolve)。
            _moduleProcessor = new ModuleLifecycleProcessor(
                _object3DPrefabMap,
                new Rhizomode.Bootstrap.BootstrapModulePlacement(() => _activeInput),
                new Rhizomode.Bootstrap.BootstrapObject3DRegistry(
                    proxy => object3DGrabHandler?.Register(proxy),
                    proxy => object3DGrabHandler?.Unregister(proxy)));

            // Phase 6 Round B: SceneLoaderLifecycleProcessor を初期化。
            // sceneLoader は [SerializeField] で MonoBehaviour に注入される。
            _sceneLoaderProcessor = new SceneLoaderLifecycleProcessor(sceneLoader);

            // Phase 12B: OscMidiTransportLifecycleProcessor を初期化。
            // oscServer / midiServer は [SerializeField]。MonoBehaviour は IOscSource /
            // IMidiSource を実装しているのでそのまま contract として渡る。
            _oscMidiTransportProcessor = new OscMidiTransportLifecycleProcessor(oscServer, midiServer);

            // Phase 12C: AbletonTransportLifecycleProcessor を初期化。
            // abletonLink は [SerializeField]。MonoBehaviour は IAbletonLink を実装。
            // AbletonOscBridge にも同じ link を注入 (旧 AbletonLink.Instance 直参照を解消)。
            _abletonTransportProcessor = new AbletonTransportLifecycleProcessor(abletonLink);
            if (abletonBridge != null)
                abletonBridge.Link = abletonLink;

            // Phase 8 Round B: NodeRuntime を eager 構築。EventBus + factory も lift して field 化。
            // processors 順序: SceneLoaderLifecycleProcessor (BeforeSetup で Loader 注入) →
            //                  ModuleLifecycleProcessor (AfterSetup で Prefab + Module 注入)
            // この設計で ScrollMenu Spawn / SceneObject 自動登録 / SpawnInputNodes 全てが
            // 自動的に Lifecycle を駆動する (旧来の手動 InjectModuleIfNeeded 呼び出しを撤廃)。
            if (graphContext != null)
            {
                // Phase 8 Round F3: Factory + EventBus + Dispatcher + Translator + Persistence の
                // wiring を GraphAdapterWiring に集約。
                _graphAdapterWiring = new GraphAdapterWiring(graphContext.Context);
                _sharedFactory = _graphAdapterWiring.Factory;
                _phase5EventBus = _graphAdapterWiring.EventBus;

                _nodeRuntime = new Rhizomode.Graph.Runtime.NodeRuntime(
                    graphContext.Context, _phase5EventBus,
                    new Rhizomode.Graph.Runtime.INodeLifecycleProcessor[]
                    {
                        _sceneLoaderProcessor, _oscMidiTransportProcessor,
                        _abletonTransportProcessor, _moduleProcessor
                    });

                // Phase 8 Round C: NodeSpawnService を初期化 (Plan v5.3 F-8.2 抽出 1/N)。
                _nodeSpawnService = new NodeSpawnService(graphContext.Context, _nodeRuntime);

                // Phase 9 Round F: visual coordinator を初期化 (F-8.2 抽出残)。
                if (visualManager != null && edgeVisualManager != null)
                {
                    _graphLoadCoordinator = new GraphLoadCoordinator(visualManager, edgeVisualManager);
                    _menuNodeSpawnCoordinator = new MenuNodeSpawnCoordinator(visualManager, edgeVisualManager, _nodeSpawnService);
                }

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
                var visual = visualManager.CreateNodeVisual(new NodeViewAdapter(r.Node), r.SpawnPosition);
                if (visual != null && controllerInput != null)
                {
                    var headPos = _activeInput!.HeadPosition;
                    visual.transform.rotation = Quaternion.LookRotation(r.SpawnPosition - headPos);
                }
            }
        }


        /// <summary>Phase 8 Round F3: GraphAdapterWiring.Translator を 3 handler に注入する薄い wrapper。</summary>
        private void WireIntentSink()
        {
            if (_graphAdapterWiring == null) return;
            var translator = _graphAdapterWiring.Translator;
            edgeDragHandler?.SetIntentSink(translator);
            edgeCutHandler?.SetIntentSink(translator);
            nodeDeleteHandler?.SetIntentSink(translator);
            Debug.Log("[GameBootstrap] Phase 5 Round E: IntentSink wired up for 3 handlers.");
        }

        /// <summary>Phase 8 Round F3: GraphAdapterWiring.ConfigureSaveLoad に delegate する薄い wrapper。</summary>
        private void ConfigureSaveLoad()
        {
            if (graphSaveLoad == null || _graphAdapterWiring == null || _nodeRuntime == null) return;
            _graphAdapterWiring.ConfigureSaveLoad(graphSaveLoad, _nodeRuntime);
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

            // Round F1: visual rebuild + プレイヤー方向への回転は GraphLoadCoordinator に委譲。
            _graphLoadCoordinator?.Rebuild(ctx, _activeInput);
        }

        private void Update()
        {
            // Phase 12D: HealthAggregator を低頻度で駆動 (各 system の状態を polling)。
            // CurrentSnapshot() の per-frame alloc を避けるため HealthTickIntervalFrames 間隔。
            // Phase 13 で VContainer ITickable adapter (HealthAggregatorTickAdapter) に移行予定。
            if (_healthAggregator != null && ++_healthTickCounter >= HealthTickIntervalFrames)
            {
                _healthTickCounter = 0;
                _healthAggregator.Tick();
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

            // Phase 13C: health → StatusPanel 購読を解放 (aggregator dispose より先に)。
            _healthSubscription?.Dispose();
            _healthSubscription = null;

            // Phase 12D: HealthAggregator を解放 (OnHealthChange Subject の dispose)。
            _healthAggregator?.Dispose();
            _healthAggregator = null;
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

            // Round F2: visual 創出 + 入力ノード自動 spawn の visual 構築は MenuNodeSpawnCoordinator に委譲。
            _menuNodeSpawnCoordinator?.CreatePrimaryVisual(spawnResult.Node, spawnResult.Position, headPos);
            _menuNodeSpawnCoordinator?.SpawnInputVisuals(spawnResult.Node, spawnResult.Position, headPos);

            Debug.Log($"[GameBootstrap] Node setup complete: {spawnResult.Node.NodeType}");
        }

    }
}
