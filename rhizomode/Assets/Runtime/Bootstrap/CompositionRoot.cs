#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Serialization;
using Rhizomode.Interaction.GraphAdapter;
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
    /// V2a で Catalog / Observability、V2b で Graph / Persistence の Installer 産サービスを公開。
    /// GameBootstrap がまだ手動 wiring (NodeRuntime 構築 / WireIntentSink / ConfigureSaveLoad) を
    /// 担うため本クラスは一時的に多数のサービスを抱える transitional な bag — V3/V-final で
    /// GameBootstrap を解体すれば縮小・廃止される。Installer / NodeRegistrationOrchestrator 等の
    /// Bootstrap-internal 型は晒さず、Object3D prefab map は BCL 型で渡す。
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

        /// <summary>
        /// NodeRegistrationOrchestrator が populate した Object3D prefab の逆引きマップ
        /// (prefab 名 → 元 prefab)。GraphState 未配置の degraded 起動では null。
        /// </summary>
        public IReadOnlyDictionary<string, GameObject>? Object3DPrefabMap { get; }

        /// <summary>GraphInstaller が構築した合成ノード factory (CompositeNodeFactory)。</summary>
        public INodeFactory NodeFactory { get; }

        /// <summary>
        /// GraphInstaller が構築した GraphEventBus。NodeRuntime ctor に渡す。
        /// Dispose は当面 GameBootstrap が担う (V3 で所有移管予定)。
        /// </summary>
        public GraphEventBus EventBus { get; }

        /// <summary>GraphInstaller が構築した IntentSink (3 handler 用)。</summary>
        public SpatialIntentToCommandTranslator IntentTranslator { get; }

        /// <summary>PersistenceInstaller が構築した GraphData I/O リポジトリ。</summary>
        public IGraphRepository GraphRepository { get; }

        /// <summary>PersistenceInstaller が構築した GraphData → HydrationPlan 変換器。</summary>
        public GraphHydrator GraphHydrator { get; }

        /// <summary>PersistenceInstaller が構築した保存先パス provider。</summary>
        public ISavePathProvider SavePathProvider { get; }

        private GameObject? _scopeObject;

        public CompositionRoot(
            GameObject scopeObject,
            NodeTypeRegistry typeRegistry,
            HealthAggregator healthAggregator,
            IReadOnlyDictionary<string, GameObject>? object3DPrefabMap,
            INodeFactory nodeFactory,
            GraphEventBus eventBus,
            SpatialIntentToCommandTranslator intentTranslator,
            IGraphRepository graphRepository,
            GraphHydrator graphHydrator,
            ISavePathProvider savePathProvider)
        {
            _scopeObject = scopeObject;
            TypeRegistry = typeRegistry;
            HealthAggregator = healthAggregator;
            Object3DPrefabMap = object3DPrefabMap;
            NodeFactory = nodeFactory;
            EventBus = eventBus;
            IntentTranslator = intentTranslator;
            GraphRepository = graphRepository;
            GraphHydrator = graphHydrator;
            SavePathProvider = savePathProvider;
        }

        /// <summary>
        /// scope GameObject を破棄する。子の <c>RootLifetimeScope</c> (LifetimeScope) の OnDestroy が
        /// VContainer container を Dispose し、ObservabilityInstaller 産の HealthAggregator や
        /// EntryPointsInstaller の ITickable adapter 群が停止・解放される。
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
