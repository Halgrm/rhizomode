#nullable enable

using System.Collections.Generic;
using Rhizomode.Bootstrap.Installers;
using Rhizomode.Bootstrap.Wiring;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Observability.Contracts;
using Rhizomode.Observability.Runtime;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// アプリ唯一の VContainer composition root。Plan v5.4 §15 — Bootstrap だけが VContainer を参照する。
    /// </summary>
    /// <remarks>
    /// V-final (Vf-c): GameBootstrap shim と EntryPointBootstrapper transitional shim を廃止し、本クラスを
    /// SampleScene の XR Origin に直接配置する composition root へ昇格。<see cref="sceneRefs"/> の
    /// <c>[SerializeField]</c> 注入で全 scene 参照を集約し、<see cref="Configure"/> が container を build、
    /// <see cref="Start"/> が全 wiring を eager 駆動する。LifetimeScope の Awake / OnDestroy chain が
    /// VContainer container の構築 / 破棄を自動で行うため、GameBootstrap や CompositionRoot の Dispose
    /// handle は不要になった。
    ///
    /// Configure → Start の Unity ライフサイクル:
    /// <list type="number">
    ///   <item>Awake (LifetimeScope.Awake): <see cref="Configure"/> 内で全 Installer 登録 + container build</item>
    ///   <item>Start: 本クラスの <see cref="Start"/> が全 wiring を順次 Wire する (旧
    ///     EntryPointBootstrapper.Launch の Phase 1-5 と同じ順序)</item>
    ///   <item>OnDestroy (LifetimeScope.OnDestroy): container.Dispose で全 Lifetime.Singleton 解放</item>
    /// </list>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RootLifetimeScope : LifetimeScope
    {
        [Header("Plan v5.4 §15: 唯一の composition root として scene 参照を集約")]
        [Tooltip("XR Origin (同じ GameObject) に置く XrSceneReferences を配線する。")]
        [SerializeField] private XrSceneReferences? sceneRefs;

        protected override void Configure(IContainerBuilder builder)
        {
            // 防御: SerializeField が未配線 / sceneRefs.GraphContext が未配線なら build skip。
            // GraphContext は composition root の前提条件 (degraded boot は許容しない)。
            if (sceneRefs == null)
            {
                Debug.LogError("[RootLifetimeScope] Configure aborted — sceneRefs is not assigned.");
                return;
            }
            var graphContext = sceneRefs.GraphContext;
            if (graphContext == null)
            {
                Debug.LogError(
                    "[RootLifetimeScope] Configure aborted — sceneRefs.GraphContext is not assigned.");
                return;
            }

            // XrSceneReferences と GraphContextBehaviour は wiring クラスの ctor injection に使う。
            builder.RegisterInstance(sceneRefs);
            builder.RegisterInstance(graphContext);

            // Vf-a (F-Vf-a.2 対処): NodeVisualManager / EdgeVisualManager が wiring の ctor 依存。
            // null の場合は登録をスキップせず、明示的に diagnostic を出してから skip する
            // (旧版は静かに skip して resolve 時に VContainer exception が露出していた)。
            if (sceneRefs.VisualManager != null)
                builder.RegisterInstance(sceneRefs.VisualManager);
            else
                Debug.LogWarning(
                    "[RootLifetimeScope] sceneRefs.VisualManager is null — visual-related wiring will fail to resolve.");
            if (sceneRefs.EdgeVisualManager != null)
                builder.RegisterInstance(sceneRefs.EdgeVisualManager);
            else
                Debug.LogWarning(
                    "[RootLifetimeScope] sceneRefs.EdgeVisualManager is null — edge-visual wiring will fail to resolve.");

            var graphState = graphContext.Context;

            new GraphInstaller(graphState).Install(builder);
            new CatalogInstaller(sceneRefs, graphState).Install(builder);
            new PersistenceInstaller().Install(builder);
            new ObservabilityInstaller().Install(builder);
            new AudioInstaller(sceneRefs).Install(builder);
            new SceneInstaller(sceneRefs).Install(builder);
            new OscMidiInstaller(sceneRefs).Install(builder);
            new AbletonInstaller(sceneRefs).Install(builder);
            new ModulesInstaller().Install(builder);
            new NodesInstaller().Install(builder);
            new InputInstaller(sceneRefs).Install(builder);
            new InteractionGraphAdapterInstaller().Install(builder);
            new InteractionInstaller().Install(builder);
            new UIInstaller().Install(builder);
            new UIGraphAdapterInstaller().Install(builder);
            new XRInstaller().Install(builder);
            new EntryPointsInstaller(includeAudioDriver: sceneRefs.AudioDriver != null).Install(builder);
        }

        /// <summary>
        /// VContainer build (= base.Awake) 後の Unity Start タイミングで全 wiring を eager 駆動する。
        /// </summary>
        /// <remarks>
        /// 旧 EntryPointBootstrapper.Launch の Phase 1-5 と同じ順序:
        /// <list type="number">
        ///   <item>NodeRegistrationOrchestrator.RegisterAll (factory + Object3D prefab dict)</item>
        ///   <item>HealthAggregator monitor 集約</item>
        ///   <item>visualManager / audioDriver の Initialize</item>
        ///   <item>AudioDeviceSelector wiring</item>
        ///   <item>Interaction wiring (activeInput 確定) + MenuSpawn / BootstrapModulePlacement に注入</item>
        ///   <item>activeInput 依存系: GraphSaveLoad / SceneObjects / Ableton / VerticalSlice</item>
        /// </list>
        /// </remarks>
        private void Start()
        {
            if (sceneRefs == null || Container == null) return;
            var graphContext = sceneRefs.GraphContext;
            if (graphContext == null) return;

            // === Phase 1: GraphState 初期化系の eager step ===
            Container.Resolve<NodeRegistrationOrchestrator>().RegisterAll();

            var healthAggregator = Container.Resolve<HealthAggregator>();
            foreach (var monitor in Container.Resolve<IReadOnlyList<IHealthMonitor>>())
                healthAggregator.Register(monitor);

            // === Phase 2: scene-ref 依存の Initialize ===
            var typeRegistry = Container.Resolve<NodeTypeRegistry>();
            if (sceneRefs.VisualManager != null)
                sceneRefs.VisualManager.Initialize(typeRegistry);
            if (sceneRefs.AudioDriver != null)
                sceneRefs.AudioDriver.Initialize(graphContext);

            // === Phase 3: AudioDeviceSelector wiring (依存なし) ===
            Container.Resolve<AudioDeviceSelectorWiring>().Wire();

            // === Phase 4: Interaction wiring (activeInput 確定) ===
            var menuSpawnWiring = Container.Resolve<MenuSpawnBootstrapWiring>();
            var interactionWiring = Container.Resolve<InteractionBootstrapWiring>();
            interactionWiring.Wire(graphContext, menuSpawnWiring.HandleSelection);
            var activeInput = interactionWiring.ActiveInput;
            menuSpawnWiring.SetActiveInput(activeInput);

            // BootstrapModulePlacement に activeInput を後付け注入 (Vf-b closure 解消の対称箇所)。
            Container.Resolve<BootstrapModulePlacement>().SetActiveInput(activeInput);

            // === Phase 5: activeInput 依存の wiring 群 ===
            Container.Resolve<GraphSaveLoadBootstrapWiring>().Wire(activeInput);
            Container.Resolve<SceneObjectsBootstrapWiring>().Wire(activeInput);
            Container.Resolve<AbletonBootstrapWiring>().Wire(activeInput, sceneRefs.SharedRaycastService);
            Container.Resolve<VerticalSliceBootstrapWiring>().Wire(graphContext);
        }
    }
}
