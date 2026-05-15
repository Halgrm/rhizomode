#nullable enable

using Rhizomode.Modules;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — Modules bounded context の <see cref="ModuleLifecycleProcessor"/> +
    /// <see cref="IModulePlacementService"/> / <see cref="IObject3DProxyRegistry"/> を登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>ModulesInstaller</c>。
    ///
    /// Vf-b で <see cref="BootstrapModulePlacement"/> / <see cref="BootstrapObject3DRegistry"/> の
    /// closure 依存を解消したため、両者を本 Installer 内で <see cref="Lifetime.Singleton"/> 登録できる
    /// ようになった。BootstrapModulePlacement は <c>InteractionBootstrapWiring.Wire</c> 完了後に
    /// <see cref="EntryPointBootstrapper.Launch"/> が <c>SetActiveInput</c> を呼ぶ。
    /// BootstrapObject3DRegistry は <see cref="XrSceneReferences"/> を ctor 注入で受け取る。
    ///
    /// <see cref="ModuleLifecycleProcessor"/> は IDisposable のため <see cref="Lifetime.Singleton"/> 登録で
    /// container が生成・所有・Dispose する。NodesInstaller が NodeRuntime の processor 配列へ組み込む。
    /// </remarks>
    internal sealed class ModulesInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<BootstrapModulePlacement>(Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();
            builder.Register<BootstrapObject3DRegistry>(Lifetime.Singleton)
                .AsImplementedInterfaces();
            builder.Register<ModuleLifecycleProcessor>(Lifetime.Singleton);
        }
    }
}
