#nullable enable

using Rhizomode.Modules;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — Modules bounded context の <see cref="ModuleLifecycleProcessor"/> を登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>ModulesInstaller</c>。V3b で GameBootstrap.Awake が直接 new していた
    /// <see cref="ModuleLifecycleProcessor"/> の構築をここへ移送。
    ///
    /// <see cref="IModulePlacementService"/> (BootstrapModulePlacement) と
    /// <see cref="IObject3DProxyRegistry"/> (BootstrapObject3DRegistry) は VR/Desktop 入力ルーターと
    /// Object3DGrabHandler (XR asmdef) への closure を要するため GameBootstrap が構築して
    /// <c>EntryPointBootstrapper.Launch</c> 経由で渡す (一時的 Plan v5.4 違反 — V3c/V-final で解消)。
    /// Object3D prefab 逆引きマップは CatalogInstaller が登録済 — ctor injection で解決される。
    ///
    /// <see cref="ModuleLifecycleProcessor"/> は IDisposable のため <see cref="Lifetime.Singleton"/> 登録で
    /// container が生成・所有・Dispose する。NodesInstaller が NodeRuntime の processor 配列へ組み込む。
    /// </remarks>
    internal sealed class ModulesInstaller : IInstaller
    {
        private readonly IModulePlacementService _placement;
        private readonly IObject3DProxyRegistry _object3DRegistry;

        public ModulesInstaller(IModulePlacementService placement, IObject3DProxyRegistry object3DRegistry)
        {
            _placement = placement;
            _object3DRegistry = object3DRegistry;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_placement);
            builder.RegisterInstance(_object3DRegistry);
            builder.Register<ModuleLifecycleProcessor>(Lifetime.Singleton);
        }
    }
}
