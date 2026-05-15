#nullable enable

using System;
using UnityEngine;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// <see cref="EntryPointBootstrapper.Launch"/> が生成した VContainer scope GameObject の所有 handle。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §19 hard rule: VContainer 型を参照してよいのは Bootstrap asmdef のみ。GameBootstrap
    /// (XR asmdef) は <c>IObjectResolver</c> / <c>LifetimeScope</c> に触れられないため、resolve と scope 破棄は
    /// Bootstrap 内で完結させ、外部には本クラスのプレーンな <see cref="IDisposable"/> ハンドルのみ晒す。
    ///
    /// V-final (Vf-a): 全 wiring を <see cref="EntryPointBootstrapper.Launch"/> 内で eager 駆動するように
    /// なったため、CompositionRoot は transitional bag から退役し、scope GameObject の Dispose handle 専用に
    /// 縮小した (Plan v5.4 §15 violation を解消)。GameBootstrap.OnDestroy は本クラスの Dispose を呼ぶだけ。
    /// </remarks>
    public sealed class CompositionRoot : IDisposable
    {
        private GameObject? _scopeObject;

        public CompositionRoot(GameObject scopeObject)
        {
            _scopeObject = scopeObject;
        }

        /// <summary>
        /// scope GameObject を破棄する。子の <c>RootLifetimeScope</c> (LifetimeScope) の OnDestroy が
        /// VContainer container を Dispose し、全 Lifetime.Singleton (HealthAggregator / ITickable adapter /
        /// ModuleLifecycleProcessor / GraphEventBus / 各 wiring) が解放される。
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
