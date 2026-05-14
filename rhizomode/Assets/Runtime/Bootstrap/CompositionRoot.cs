#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Observability.Runtime;
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
    /// 公開するのは XR asmdef 側が必要とする最小限のみ — Installer や NodeRegistrationOrchestrator
    /// 等の Bootstrap-internal 型は晒さず、Object3D prefab map は BCL 型で渡す。
    /// V2a 時点で公開するのは CatalogInstaller / ObservabilityInstaller 産のサービスのみ。
    /// GraphInstaller / PersistenceInstaller 産のサービスは V2b で本クラスに追加される。
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

        private GameObject? _scopeObject;

        public CompositionRoot(
            GameObject scopeObject,
            NodeTypeRegistry typeRegistry,
            HealthAggregator healthAggregator,
            IReadOnlyDictionary<string, GameObject>? object3DPrefabMap)
        {
            _scopeObject = scopeObject;
            TypeRegistry = typeRegistry;
            HealthAggregator = healthAggregator;
            Object3DPrefabMap = object3DPrefabMap;
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
