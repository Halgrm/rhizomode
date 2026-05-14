#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using R3;
using Rhizomode.Audio.Analysis;
using Rhizomode.Audio.GraphAdapter;
using Rhizomode.Observability.Runtime;
using Rhizomode.Cameras;
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
using Rhizomode.Bootstrap;

namespace Rhizomode.XR
{
    /// <summary>
    /// ゲーム起動時に全システムの初期化と相互接続を行う。
    /// </summary>
    public partial class GameBootstrap : MonoBehaviour
    {
        [Header("Composition Root")]
        [Tooltip("Plan v5.4 §15: scene 参照を集約する MonoBehaviour。各 Installer がここから参照を取る。")]
        [SerializeField] private XrSceneReferences? sceneRefs;

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
        [SerializeField] private ModuleDefinition[]? moduleDefinitions;
        [SerializeField] private CinemachineModule[]? cinemachineModules;
        [SerializeField] private PresetManager? presetManager;
        [SerializeField] private SpoutSenderController? spoutSender;
        [SerializeField] private NdiSenderController? ndiSender;

        [Header("Week 5 — 統合システム")]
        [SerializeField] private MirrorOutputController? mirrorOutput;
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

        // V3a: Audio / OSC / MIDI / Ableton の scene 参照は XrSceneReferences へ移送。
        // GameBootstrap は sceneRefs.OscServer / sceneRefs.AbletonLink 等を transitional に参照する
        // (LifecycleProcessor 構築は V3b で Installer 化)。

        private NodeTypeRegistry? _typeRegistry;

        /// <summary>
        /// Plan v5.4 §15 (V2a): VContainer composition root の所有 handle。LaunchCompositionRoot で
        /// 取得し、OnDestroy で最初に Dispose する (= scope GameObject 破棄 → container Dispose)。
        /// graphContext 未設定の degraded 起動では null。
        /// </summary>
        private CompositionRoot? _compositionRoot;

        /// <summary>実際に使用中の入力ルーター（VRまたはデスクトップ）。</summary>
        private IControllerInput? _activeInput;

        /// <summary>Object3Dプレハブ名→元プレハブの逆引き。Instantiate用。</summary>
        private readonly Dictionary<string, GameObject> _object3DPrefabMap = new();

        /// <summary>
        /// GraphEventBus。V2b で GraphInstaller が構築・container 登録するようになり、本フィールドは
        /// LaunchCompositionRoot で <see cref="CompositionRoot.EventBus"/> から受け取る。NodeRuntime
        /// ctor へ渡し、OnDestroy で Dispose する (Subject を複数保持するため Dispose 必須)。
        /// 所有を container 側へ完全移管するのは V3 (NodeRuntime の Installer 化と同時)。
        /// </summary>
        private Rhizomode.Graph.Events.GraphEventBus? _phase5EventBus;

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
        /// Audio / OSC / MIDI / Ableton の 4 monitor を Register。
        /// Plan v5.4 §15 (V2a): 構築・所有・Dispose は ObservabilityInstaller (VContainer の
        /// Lifetime.Singleton) に移行済。Tick 駆動も VContainer の HealthAggregatorTickAdapter
        /// (ITickable)。本クラスは LaunchCompositionRoot で resolve した参照に対し monitor 登録と
        /// OnHealthChange 購読のみを担う (Dispose は container 任せ)。
        /// </summary>
        private HealthAggregator? _healthAggregator;

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

        // Phase 8 Round E: NodeFactoryMap + RegisterNodeTypes + RegisterFactories +
        // RegisterModuleTypes + RegisterObject3DTypes は NodeRegistrationOrchestrator に移送済 (F-8.2 抽出 3/N)。
        // V2b: GraphAdapter wiring (旧 GraphAdapterWiring) は GraphInstaller / PersistenceInstaller に
        // 吸収。GameBootstrap は CompositionRoot 経由で resolve 済サービスを受け取る。

        private void Awake()
        {
            // V2b: VContainer composition root を Awake 序盤で起動。Graph / Catalog / Persistence /
            // Observability / EntryPoints の各 Installer が pure-C# サービスを構築し、container から
            // resolve して _compositionRoot 経由で各 field に束ねる。
            LaunchCompositionRoot();

            // Phase 6 Round A: ModuleLifecycleProcessor を初期化
            // (CatalogInstaller の RegisterObject3DTypes で _object3DPrefabMap が populate された後)。
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
            // V3a: oscServer / midiServer は XrSceneReferences から取得。MonoBehaviour は
            // IOscSource / IMidiSource を実装しているのでそのまま contract として渡る。
            // (LifecycleProcessor の Installer 化は V3b。)
            _oscMidiTransportProcessor = new OscMidiTransportLifecycleProcessor(
                sceneRefs != null ? sceneRefs.OscServer : null,
                sceneRefs != null ? sceneRefs.MidiServer : null);

            // Phase 12C: AbletonTransportLifecycleProcessor を初期化。
            // V3a: abletonLink は XrSceneReferences から取得。AbletonOscBridge への link 注入は
            // AbletonBootstrapWiring.Wire へ移送済。
            _abletonTransportProcessor = new AbletonTransportLifecycleProcessor(
                sceneRefs != null ? sceneRefs.AbletonLink : null);

            // Phase 8 Round B: NodeRuntime を eager 構築。
            // processors 順序: SceneLoaderLifecycleProcessor (BeforeSetup で Loader 注入) →
            //                  ModuleLifecycleProcessor (AfterSetup で Prefab + Module 注入)
            // この設計で ScrollMenu Spawn / SceneObject 自動登録 / SpawnInputNodes 全てが
            // 自動的に Lifecycle を駆動する (旧来の手動 InjectModuleIfNeeded 呼び出しを撤廃)。
            // V2b: EventBus は GraphInstaller 産を CompositionRoot 経由で受け取る。
            if (graphContext != null && _compositionRoot != null)
            {
                _nodeRuntime = new Rhizomode.Graph.Runtime.NodeRuntime(
                    graphContext.Context, _compositionRoot.EventBus,
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
        /// Plan v5.4 §15 (V2b): VContainer composition root を起動し、Graph / Catalog / Persistence /
        /// Observability / EntryPoints の各 Installer が構築した pure-C# サービスを container から
        /// resolve して各 field (_typeRegistry / _healthAggregator / _phase5EventBus /
        /// _object3DPrefabMap) に束ねる。
        /// </summary>
        /// <remarks>
        /// VContainer 型には触れず、Bootstrap asmdef の <see cref="EntryPointBootstrapper"/> に scene
        /// 由来の値を渡し、戻り値の <c>CompositionRoot</c> から型付きでサービスを受け取る
        /// (Plan v5.4 §19: VContainer 参照は Bootstrap asmdef のみ)。生成される scope GameObject は
        /// 本コンポーネントの子なので GameBootstrap の破棄と同時に破棄され、LifetimeScope.OnDestroy が
        /// container を Dispose する (ObservabilityInstaller 産の HealthAggregator もこの時 Dispose)。
        ///
        /// graphContext 未設定の degraded 起動では scope を起動せず、空の NodeTypeRegistry のみ
        /// fallback で確保する (旧 Awake が常に new NodeTypeRegistry() していた挙動を保つ)。
        /// NodeFactory / IntentTranslator / GraphRepository 等は <c>_compositionRoot</c> 経由で
        /// 後段の WireIntentSink / ConfigureSaveLoad / NodeRuntime 構築から参照する。
        /// </remarks>
        private void LaunchCompositionRoot()
        {
            if (graphContext == null || sceneRefs == null)
            {
                Debug.LogWarning(
                    "[GameBootstrap] LaunchCompositionRoot skipped — graphContext / sceneRefs 未設定 (degraded 起動)。");
                _typeRegistry = new NodeTypeRegistry();
                return;
            }

            _compositionRoot = EntryPointBootstrapper.Launch(
                transform, sceneRefs, graphContext.Context,
                moduleDefinitions, object3DPrefabs);

            _typeRegistry = _compositionRoot.TypeRegistry;
            _healthAggregator = _compositionRoot.HealthAggregator;
            _phase5EventBus = _compositionRoot.EventBus;

            // Object3D prefab map は ModuleLifecycleProcessor の dependency として利用。
            if (_compositionRoot.Object3DPrefabMap != null)
            {
                foreach (var kvp in _compositionRoot.Object3DPrefabMap)
                    _object3DPrefabMap[kvp.Key] = kvp.Value;
            }
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

            // V3a: audioDriver は XrSceneReferences から取得。Initialize の Installer 化は後続 V で検討。
            var audioDriver = sceneRefs != null ? sceneRefs.AudioDriver : null;
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


        /// <summary>V2b: GraphInstaller 産の IntentTranslator を 3 handler に注入する薄い wrapper。</summary>
        private void WireIntentSink()
        {
            if (_compositionRoot == null) return;
            var translator = _compositionRoot.IntentTranslator;
            edgeDragHandler?.SetIntentSink(translator);
            edgeCutHandler?.SetIntentSink(translator);
            nodeDeleteHandler?.SetIntentSink(translator);
            Debug.Log("[GameBootstrap] IntentSink wired up for 3 handlers.");
        }

        /// <summary>
        /// V2b: PersistenceInstaller 産の Repository / Hydrator / SavePathProvider と GraphInstaller 産の
        /// NodeFactory を GraphSaveLoadManager に注入する。HydrationPlanExecutor のみ scene-ref 依存の
        /// NodeRuntime を要するためここで構築する (Installer 化は V3)。
        /// </summary>
        private void ConfigureSaveLoad()
        {
            if (graphSaveLoad == null || _compositionRoot == null || _nodeRuntime == null) return;

            var executor = new Rhizomode.Graph.Runtime.HydrationPlanExecutor(_nodeRuntime);
            graphSaveLoad.Configure(
                _compositionRoot.GraphRepository,
                _compositionRoot.GraphHydrator,
                executor,
                _compositionRoot.NodeFactory,
                _compositionRoot.SavePathProvider);
            Debug.Log("[GameBootstrap] SaveLoad configured (Repository + Hydrator + Executor).");
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

        private void OnDestroy()
        {
            // V2a: LaunchCompositionRoot で生成した VContainer scope GameObject を破棄する。
            // GameObject 階層ごと破棄される通常ケースでは子 scope も Unity が連鎖破棄するが、
            // GameBootstrap コンポーネント単独破棄のケースでは scope GameObject が孤児として残り
            // ITickable adapter が tick し続ける。それを防ぐため明示的に破棄をスケジュールする。
            // 子 RootLifetimeScope.OnDestroy が VContainer container を Dispose する
            // (ObservabilityInstaller 産の HealthAggregator もこの時 Dispose)。
            _compositionRoot?.Dispose();
            _compositionRoot = null;

            // イベント購読解除
            if (graphSaveLoad != null)
            {
                graphSaveLoad.OnGraphLoading -= OnGraphLoadingHandler;
                graphSaveLoad.OnGraphLoaded -= OnGraphLoaded;
            }

            if (scrollMenuVisual != null)
                scrollMenuVisual.OnNodeTypeSelected -= OnScrollMenuNodeSelected;

            // V3a: AudioDeviceSelector / Ableton Macro listener の購読解除は
            // AudioDeviceSelectorWiring / AbletonBootstrapWiring (container 所有 Lifetime.Singleton) の
            // Dispose が担う。_compositionRoot.Dispose() が scope を破棄した時点で両者も Dispose される。

            // Phase 8: dispose 順序 — subscribers (processor) を先に dispose し、source (EventBus) を後に。
            // 将来 ModuleLifecycleProcessor が GraphEventBus.OnNodeRemoved を購読した場合に、
            // 逆順だと disposed Subject に処理が走る race を防ぐ。
            _moduleProcessor?.Dispose();
            _moduleProcessor = null;

            // Phase 5 Round E で構築した EventBus を解放 (Codex review fix #8)。
            // applier / dispatcher / translator は IDisposable ではないため EventBus のみで OK。
            _phase5EventBus?.Dispose();
            _phase5EventBus = null;

            // Phase 13C: health → StatusPanel 購読を解放。
            // V2a transitional 非対称: HealthAggregator 自体の Dispose は VContainer
            // (ObservabilityInstaller の Lifetime.Singleton) が scope GameObject (本コンポーネントの子)
            // の OnDestroy で行う。通常は本 OnDestroy が先に走るため購読解放が source dispose より
            // 先で安全だが、scope GameObject が単独破棄された場合は順序が逆転し得る。R3 は disposed
            // Subject への購読解放を no-op として許容するため実害はないが、上記 EventBus の
            // 「subscriber を source より先に dispose」規約とは非対称。V3+ で HealthAggregator の
            // 所有・購読を Installer 側に完全移管した際に整理する。
            _healthSubscription?.Dispose();
            _healthSubscription = null;
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
