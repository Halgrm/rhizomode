#nullable enable

using Rhizomode.Bootstrap.Wiring;
using Rhizomode.Interaction;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — Interaction bounded context の wiring + service を登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>InteractionInstaller</c>。V3c で GameBootstrap.InitializeInteractionHandlers
    /// (~130 行) を移送した <see cref="InteractionBootstrapWiring"/> を <see cref="Lifetime.Singleton"/>
    /// 登録する (container が生成・所有・Dispose)。ctor に必要な XrSceneReferences / NodeTypeRegistry /
    /// ModuleLifecycleProcessor / SpatialIntentToCommandTranslator は他 Installer が登録済 — VContainer の
    /// ctor injection で解決される。
    ///
    /// F-Vf-a.1 Phase D: 旧 Bootstrap.Services.NodeSpawnService を Rhizomode.Interaction へ移送した
    /// <see cref="NodeSpawnService"/> も本 Installer で登録 (XRInstaller から移動)。
    /// </remarks>
    internal sealed class InteractionInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<InteractionBootstrapWiring>(Lifetime.Singleton);
            builder.Register<NodeSpawnService>(Lifetime.Singleton);
        }
    }
}
