#nullable enable

using Rhizomode.Bootstrap.Wiring;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — Interaction bounded context の wiring を登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>InteractionInstaller</c>。V3c で GameBootstrap.InitializeInteractionHandlers
    /// (~130 行) を移送した <see cref="InteractionBootstrapWiring"/> を <see cref="Lifetime.Singleton"/>
    /// 登録する (container が生成・所有・Dispose)。ctor に必要な XrSceneReferences / NodeTypeRegistry /
    /// ModuleLifecycleProcessor / SpatialIntentToCommandTranslator は他 Installer が登録済 — VContainer の
    /// ctor injection で解決される。
    ///
    /// <see cref="InteractionBootstrapWiring.Wire"/> は GraphContextBehaviour と ScrollMenu の
    /// ノード選択コールバックを transitional に要するため Build 後即時には駆動できない。GameBootstrap が
    /// CompositionRoot 経由で駆動する (一時的 Plan v5.4 違反 — V-final で解消)。
    /// </remarks>
    internal sealed class InteractionInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<InteractionBootstrapWiring>(Lifetime.Singleton);
        }
    }
}
