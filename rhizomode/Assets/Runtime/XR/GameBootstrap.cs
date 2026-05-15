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
        [SerializeField] private CinemachineModule[]? cinemachineModules;
        [SerializeField] private PresetManager? presetManager;
        [SerializeField] private SpoutSenderController? spoutSender;
        [SerializeField] private NdiSenderController? ndiSender;

        [Header("Week 5 — 統合システム")]
        [SerializeField] private MirrorOutputController? mirrorOutput;
        [SerializeField] private CameraManagerPanelController? cameraManagerPanel;
        [SerializeField] private DesktopMirrorBlitter? desktopBlitter;
        [SerializeField] private GraphSaveLoadManager? graphSaveLoad;

        [Header("Desktop Debug")]
        [SerializeField] private CinemachinePreviewMonitor? cinemachinePreview;

        // V3a-c: Audio / OSC / MIDI / Ableton / Scene / Modules / Nodes / Input / Interaction の
        // scene 参照は XrSceneReferences へ移送済。LifecycleProcessor / NodeRuntime / interaction
        // handler の構築・配線は各 Installer + InteractionBootstrapWiring が担当。GameBootstrap は
        // sceneRefs 経由で参照を取り、CompositionRoot 経由で resolve 済サービスを受け取る。

        private NodeTypeRegistry? _typeRegistry;

        /// <summary>
        /// Plan v5.4 §15 (V2a): VContainer composition root の所有 handle。LaunchCompositionRoot で
        /// 取得し、OnDestroy で最初に Dispose する (= scope GameObject 破棄 → container Dispose)。
        /// graphContext 未設定の degraded 起動では null。
        /// </summary>
        private CompositionRoot? _compositionRoot;

        /// <summary>実際に使用中の入力ルーター（VRまたはデスクトップ）。</summary>
        private IControllerInput? _activeInput;

        /// <summary>
        /// V3b: ModulesInstaller が container 所有 (Lifetime.Singleton) で構築した
        /// ModuleLifecycleProcessor。LaunchCompositionRoot で <see cref="CompositionRoot.ModuleProcessor"/>
        /// から受け取る。DestroyInstance / Instances / CleanupAll の参照に使う (Dispose は container 任せ)。
        /// </summary>
        private ModuleLifecycleProcessor? _moduleProcessor;

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
        /// GraphState ミューテーション (RegisterNode / AddEdge) の唯一窓口。processors 経由で
        /// BeforeSetup → Setup → AfterSetup を駆動。V3b: NodesInstaller が container で組み立てるようになり、
        /// LaunchCompositionRoot で <see cref="CompositionRoot.NodeRuntime"/> から受け取る。
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
            // VContainer composition root を Awake 序盤で起動。各 Installer が pure-C# サービスを
            // 構築し、container から resolve して _compositionRoot 経由で各 field に束ねる。
            // V3b: NodeRuntime / ModuleLifecycleProcessor / 4 LifecycleProcessor は Scene/OscMidi/
            // Ableton/Modules/Nodes Installer が container 化済。GameBootstrap は resolve するだけ。
            LaunchCompositionRoot();

            if (graphContext != null && _compositionRoot != null)
            {
                _nodeRuntime = _compositionRoot.NodeRuntime;
                _moduleProcessor = _compositionRoot.ModuleProcessor;

                // Phase 8 Round C: NodeSpawnService を初期化 (graph mutation 部、visual は GameBootstrap)。
                _nodeSpawnService = new NodeSpawnService(graphContext.Context, _nodeRuntime);

                // Phase 9 Round F: visual coordinator を初期化 (V3c: 参照は XrSceneReferences 経由)。
                var visualManager = sceneRefs != null ? sceneRefs.VisualManager : null;
                var edgeVisualManager = sceneRefs != null ? sceneRefs.EdgeVisualManager : null;
                if (visualManager != null && edgeVisualManager != null)
                {
                    _graphLoadCoordinator = new GraphLoadCoordinator(visualManager, edgeVisualManager);
                    _menuNodeSpawnCoordinator = new MenuNodeSpawnCoordinator(visualManager, edgeVisualManager, _nodeSpawnService);
                }

                // Phase 8 Round D: SceneObjectRegistrationService を初期化。
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
        /// Plan v5.4 §15: VContainer composition root を起動し、各 Installer が構築した pure-C#
        /// サービスを container から resolve して <c>_compositionRoot</c> 経由で受け取る。
        /// </summary>
        /// <remarks>
        /// VContainer 型には触れず、Bootstrap asmdef の <see cref="EntryPointBootstrapper"/> に scene
        /// 由来の値を渡し、戻り値の <c>CompositionRoot</c> から型付きでサービスを受け取る
        /// (Plan v5.4 §19: VContainer 参照は Bootstrap asmdef のみ)。生成される scope GameObject は
        /// 本コンポーネントの子なので GameBootstrap の破棄と同時に破棄され、LifetimeScope.OnDestroy が
        /// container を Dispose する。
        ///
        /// V3b: ModuleLifecycleProcessor が要する <c>IModulePlacementService</c> /
        /// <c>IObject3DProxyRegistry</c> は VR/Desktop 入力ルーターと object3DGrabHandler (XR asmdef) への
        /// closure を要するため、GameBootstrap が構築して Launch に渡す (§19: XrSceneReferences に XR 型は
        /// 置けない transitional shape — V-final で解消)。
        ///
        /// graphContext / sceneRefs 未設定の degraded 起動では scope を起動せず、空の NodeTypeRegistry
        /// のみ fallback で確保する。
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

            // ModuleLifecycleProcessor の placement / object3D registry は GameBootstrap が構築する
            // (BootstrapModulePlacement は Func で _activeInput を遅延解決 — _activeInput は
            // InteractionBootstrapWiring.Wire 後に確定する)。object3DGrabHandler は XrSceneReferences 経由。
            var object3DGrabHandler = sceneRefs.Object3DGrabHandler;
            var modulePlacement = new Rhizomode.Bootstrap.BootstrapModulePlacement(() => _activeInput);
            var object3DRegistry = new Rhizomode.Bootstrap.BootstrapObject3DRegistry(
                proxy => object3DGrabHandler?.Register(proxy),
                proxy => object3DGrabHandler?.Unregister(proxy));

            _compositionRoot = EntryPointBootstrapper.Launch(
                transform, sceneRefs, graphContext.Context, modulePlacement, object3DRegistry);

            _typeRegistry = _compositionRoot.TypeRegistry;
            _healthAggregator = _compositionRoot.HealthAggregator;
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

            // V3c: visualManager 参照は XrSceneReferences 経由 (Installer 化は V3d)。
            var visualManager = sceneRefs != null ? sceneRefs.VisualManager : null;
            if (visualManager != null)
                visualManager.Initialize(_typeRegistry);

            // V3a: audioDriver は XrSceneReferences から取得。Initialize の Installer 化は後続 V で検討。
            var audioDriver = sceneRefs != null ? sceneRefs.AudioDriver : null;
            if (audioDriver != null && graphContext != null)
                audioDriver.Initialize(graphContext);

            // V3c: interaction handler の選択・配線は InteractionBootstrapWiring へ移送。
            // GraphContextBehaviour と ScrollMenu のノード選択コールバック (OnScrollMenuNodeSelected) を
            // transitional に渡す (一時的 Plan v5.4 違反 — V-final で解消)。Wire 後に _activeInput が確定。
            _compositionRoot?.InteractionWiring.Wire(graphContext, OnScrollMenuNodeSelected);
            _activeInput = _compositionRoot?.InteractionWiring.ActiveInput;
        }

        /// <summary>
        /// シーン上の SceneObjectBridge を全検出し、対応するノードを自動生成する。
        /// Phase 8 Round D: graph 操作は SceneObjectRegistrationService に委譲、visual は本クラスで生成。
        /// </summary>
        private void RegisterSceneObjects()
        {
            var visualManager = sceneRefs != null ? sceneRefs.VisualManager : null;
            if (visualManager == null || _sceneObjectService == null) return;

            _sceneObjectService.RegisterTypeAndFactory();

            var bridges = FindObjectsByType<SceneObjectBridge>(FindObjectsSortMode.None);
            var results = _sceneObjectService.RegisterBridges(bridges);

            foreach (var r in results)
            {
                var visual = visualManager.CreateNodeVisual(new NodeViewAdapter(r.Node), r.SpawnPosition);
                if (visual != null && _activeInput != null)
                {
                    var headPos = _activeInput.HeadPosition;
                    visual.transform.rotation = Quaternion.LookRotation(r.SpawnPosition - headPos);
                }
            }
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

            // V3c: ScrollMenu の OnNodeTypeSelected 購読解除は InteractionBootstrapWiring.Dispose が担う
            // (container 所有 Lifetime.Singleton)。_compositionRoot.Dispose() で scope 破棄時に解放される。

            // V3a/V3b: AudioDeviceSelectorWiring / AbletonBootstrapWiring / InteractionBootstrapWiring /
            // ModuleLifecycleProcessor / GraphEventBus は全て container 所有 (Lifetime.Singleton)。
            // _compositionRoot.Dispose() が scope を破棄した時点で container が一括 Dispose するため、
            // GameBootstrap 側の手動 Dispose は不要。
            _moduleProcessor = null;
            _nodeRuntime = null;

            // Phase 13C: health → StatusPanel 購読を解放。
            // transitional 非対称: HealthAggregator 自体の Dispose は VContainer (ObservabilityInstaller の
            // Lifetime.Singleton) が scope GameObject (本コンポーネントの子) の OnDestroy で行う。通常は
            // 本 OnDestroy が先に走るため購読解放が source dispose より先で安全だが、scope GameObject が
            // 単独破棄された場合は順序が逆転し得る。R3 は disposed Subject への購読解放を no-op として
            // 許容するため実害はない。V3d で StatusPanel subscription を Installer 側へ移管した際に整理する。
            _healthSubscription?.Dispose();
            _healthSubscription = null;
            _healthAggregator = null;
        }

        private void OnScrollMenuNodeSelected(string nodeType)
        {
            if (graphContext == null || _activeInput == null) return;
            if (sceneRefs == null || sceneRefs.VisualManager == null) return;
            if (_nodeSpawnService == null)
            {
                Debug.LogError($"[GameBootstrap] OnScrollMenuNodeSelected aborted ({nodeType}) — _nodeSpawnService not initialized.");
                return;
            }

            Debug.Log($"[GameBootstrap] OnScrollMenuNodeSelected: {nodeType}");

            // Phase 8 Round C: graph mutation は NodeSpawnService に委譲、visual 創出はここで実行。
            var headPos = _activeInput.HeadPosition;
            var headFwd = _activeInput.HeadForward;
            var spawnResult = _nodeSpawnService.TrySpawnFromMenu(nodeType, headPos, headFwd);
            if (spawnResult == null) return;

            // ノード生成後にスクロールメニューを閉じる
            sceneRefs.ScrollMenuInteraction?.CloseMenu();

            // Object3D の Proxy 観測 bind (visual 創出と同層、GraphState 必要)
            if (spawnResult.Node is Object3DNode obj3d) BindObject3DProxyObservables(obj3d);

            // Round F2: visual 創出 + 入力ノード自動 spawn の visual 構築は MenuNodeSpawnCoordinator に委譲。
            _menuNodeSpawnCoordinator?.CreatePrimaryVisual(spawnResult.Node, spawnResult.Position, headPos);
            _menuNodeSpawnCoordinator?.SpawnInputVisuals(spawnResult.Node, spawnResult.Position, headPos);

            Debug.Log($"[GameBootstrap] Node setup complete: {spawnResult.Node.NodeType}");
        }

    }
}
