#nullable enable

using Rhizomode.Bootstrap.Wiring;
using Rhizomode.Bootstrap.EntryPoints;
using Rhizomode.UI;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — UI.GraphAdapter bounded context のサービスと wiring を登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>UIGraphAdapterInstaller</c>。以下を <see cref="Lifetime.Singleton"/> 登録する:
    /// <list type="bullet">
    ///   <item><see cref="GraphLoadCoordinator"/> — グラフロード後の visual 再構築 (UI.GraphAdapter asmdef)</item>
    ///   <item><see cref="MenuNodeSpawnCoordinator"/> — ScrollMenu spawn 後の visual 創出 (UI.GraphAdapter asmdef)</item>
    ///   <item><see cref="GraphSaveLoadBootstrapWiring"/> — SaveLoad 駆動 + Loading/Loaded 購読の wiring</item>
    /// </list>
    /// F-Vf-a.1 Phase A: 旧 Bootstrap.Services の 2 つの coordinator を UI.GraphAdapter asmdef に
    /// 移送した結果、本 Installer に集約された。
    /// </remarks>
    internal sealed class UIGraphAdapterInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<GraphLoadCoordinator>(Lifetime.Singleton);
            builder.Register<MenuNodeSpawnCoordinator>(Lifetime.Singleton);
            builder.Register<CameraStatePersistenceService>(Lifetime.Singleton)
                .As<ICameraStatePersistence>();
            builder.Register<GlitchDriverHost>(Lifetime.Singleton);
            builder.RegisterEntryPoint<GlitchDriverHostTickAdapter>(Lifetime.Singleton);
            builder.Register<GraphSaveLoadBootstrapWiring>(Lifetime.Singleton);
        }
    }
}
