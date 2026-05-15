#nullable enable

using System;
using Rhizomode.Bootstrap.Wiring;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Runtime;
using Rhizomode.Graph.Serialization;
using Rhizomode.Modules;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Observability.Runtime;
using Rhizomode.Persistence.Contracts;
using UnityEngine;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// <see cref="EntryPointBootstrapper.Launch"/> が VContainer から resolve した pure-C# サービスを
    /// 型付きで束ねた結果オブジェクト兼、scope GameObject の所有 handle。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §19 hard rule: VContainer 型を参照してよいのは Bootstrap asmdef のみ。GameBootstrap
    /// (XR asmdef) は <c>IObjectResolver</c> / <c>LifetimeScope</c> に触れられないため、resolve と
    /// scope 破棄は Bootstrap 内で完結させ、GameBootstrap には本クラスのプレーンな型付き API のみ晒す。
    ///
    /// V3b: GraphEventBus / Object3DPrefabMap は GameBootstrap が直接使わなくなった (NodeRuntime /
    /// ModuleLifecycleProcessor を container 化) ため公開を撤去。代わりに container 所有の
    /// <see cref="NodeRuntime"/> / <see cref="ModuleLifecycleProcessor"/> を公開する — GameBootstrap は
    /// NodeSpawnService / HydrationPlanExecutor 構築や OnGraphLoading の cleanup でこれらを参照する。
    /// 本クラスは GameBootstrap が手動 wiring を担う間の transitional な bag — V-final で縮小・廃止される。
    ///
    /// <see cref="Dispose"/> は scope GameObject を破棄し、VContainer container を Dispose させる。
    /// GameBootstrap.OnDestroy から、本クラスが渡したサービスへ依存する後段の手動 teardown より
    /// 先に呼ぶこと。
    /// </remarks>
    public sealed class CompositionRoot : IDisposable
    {
        /// <summary>CatalogInstaller が構築した NodeTypeRegistry (常に非 null)。</summary>
        public NodeTypeRegistry TypeRegistry { get; }

        /// <summary>ObservabilityInstaller が構築し container が所有する HealthAggregator。</summary>
        public HealthAggregator HealthAggregator { get; }

        /// <summary>GraphInstaller が構築した合成ノード factory (CompositeNodeFactory)。</summary>
        public INodeFactory NodeFactory { get; }

        /// <summary>PersistenceInstaller が構築した GraphData I/O リポジトリ。</summary>
        public IGraphRepository GraphRepository { get; }

        /// <summary>PersistenceInstaller が構築した GraphData → HydrationPlan 変換器。</summary>
        public GraphHydrator GraphHydrator { get; }

        /// <summary>PersistenceInstaller が構築した保存先パス provider。</summary>
        public ISavePathProvider SavePathProvider { get; }

        /// <summary>
        /// AbletonInstaller が構築した Ableton OSC wiring。<see cref="AbletonBootstrapWiring.Wire"/> は
        /// 入力ルーター / SharedRaycastService を要するため GameBootstrap が InteractionHandlers 初期化後に
        /// 駆動する (一時的 Plan v5.4 違反 — V3c で input/interaction が container 化したら解消)。
        /// </summary>
        public AbletonBootstrapWiring AbletonWiring { get; }

        /// <summary>
        /// NodesInstaller が container で組み立てた NodeRuntime。GameBootstrap は NodeSpawnService /
        /// SceneObjectRegistrationService / HydrationPlanExecutor の構築でこれを参照する。
        /// </summary>
        public NodeRuntime NodeRuntime { get; }

        /// <summary>
        /// ModulesInstaller が container 所有 (Lifetime.Singleton) で構築した ModuleLifecycleProcessor。
        /// GameBootstrap は DestroyInstance / Instances / CleanupAll の参照に使う (Dispose は container)。
        /// </summary>
        public ModuleLifecycleProcessor ModuleProcessor { get; }

        /// <summary>
        /// InteractionInstaller が構築した interaction handler wiring。
        /// <see cref="InteractionBootstrapWiring.Wire"/> は GraphContextBehaviour と ScrollMenu の
        /// ノード選択コールバックを要するため GameBootstrap が Awake で駆動する (一時的 Plan v5.4 違反 —
        /// V-final で解消)。<see cref="InteractionBootstrapWiring.ActiveInput"/> は Wire 後に確定する。
        /// </summary>
        public InteractionBootstrapWiring InteractionWiring { get; }

        /// <summary>
        /// UIInstaller が構築した vertical-slice UI / Cameras wiring。
        /// <see cref="VerticalSliceBootstrapWiring.Wire"/> は GraphContextBehaviour を transitional
        /// 引数で要するため GameBootstrap が CompositionRoot 経由で駆動する (一時的 Plan v5.4 違反 —
        /// V-final で解消)。
        /// </summary>
        public VerticalSliceBootstrapWiring VerticalSliceWiring { get; }

        private GameObject? _scopeObject;

        public CompositionRoot(
            GameObject scopeObject,
            NodeTypeRegistry typeRegistry,
            HealthAggregator healthAggregator,
            INodeFactory nodeFactory,
            IGraphRepository graphRepository,
            GraphHydrator graphHydrator,
            ISavePathProvider savePathProvider,
            AbletonBootstrapWiring abletonWiring,
            NodeRuntime nodeRuntime,
            ModuleLifecycleProcessor moduleProcessor,
            InteractionBootstrapWiring interactionWiring,
            VerticalSliceBootstrapWiring verticalSliceWiring)
        {
            _scopeObject = scopeObject;
            TypeRegistry = typeRegistry;
            HealthAggregator = healthAggregator;
            NodeFactory = nodeFactory;
            GraphRepository = graphRepository;
            GraphHydrator = graphHydrator;
            SavePathProvider = savePathProvider;
            AbletonWiring = abletonWiring;
            NodeRuntime = nodeRuntime;
            ModuleProcessor = moduleProcessor;
            InteractionWiring = interactionWiring;
            VerticalSliceWiring = verticalSliceWiring;
        }

        /// <summary>
        /// scope GameObject を破棄する。子の <c>RootLifetimeScope</c> (LifetimeScope) の OnDestroy が
        /// VContainer container を Dispose し、ObservabilityInstaller 産の HealthAggregator や
        /// EntryPointsInstaller の ITickable adapter 群、ModulesInstaller 産の ModuleLifecycleProcessor /
        /// GraphInstaller 産の GraphEventBus が停止・解放される。
        /// </summary>
        public void Dispose()
        {
            if (_scopeObject != null)
            {
                UnityEngine.Object.Destroy(_scopeObject);
                _scopeObject = null;
            }
        }
    }
}
