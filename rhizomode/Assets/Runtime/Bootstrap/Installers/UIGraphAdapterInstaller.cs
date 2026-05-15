#nullable enable

using Rhizomode.Bootstrap.Wiring;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — UI.GraphAdapter bounded context のサービスと wiring を登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>UIGraphAdapterInstaller</c>。V-final (Vf-a) で旧 GameBootstrap.ConfigureSaveLoad +
    /// OnGraphLoading/OnGraphLoaded + OnGraphLoaded 内 visual rebuild を移送した
    /// <see cref="GraphSaveLoadBootstrapWiring"/> と <see cref="GraphLoadCoordinator"/> を
    /// <see cref="Lifetime.Singleton"/> 登録する (container が生成・所有・Dispose)。
    ///
    /// V3d 時点で「UIGraphAdapter の concrete 登録内容がない」状態だったが、V-final で
    /// graphSaveLoad 系 + GraphLoadCoordinator の owner が確定 — UI ↔ Graph の adapter 層として
    /// 本 Installer に集約。
    /// </remarks>
    internal sealed class UIGraphAdapterInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<GraphLoadCoordinator>(Lifetime.Singleton);
            builder.Register<GraphSaveLoadBootstrapWiring>(Lifetime.Singleton);
        }
    }
}
