#nullable enable

using Rhizomode.Graph.Mutation;
using Rhizomode.Interaction.GraphAdapter;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — Interaction.GraphAdapter bounded context の
    /// <see cref="SpatialIntentToCommandTranslator"/> を登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>InteractionGraphAdapterInstaller</c>。V3c で GraphInstaller が暫定登録していた
    /// <see cref="SpatialIntentToCommandTranslator"/> を、本来の bounded context である本 Installer へ移送。
    ///
    /// <see cref="SpatialIntentToCommandTranslator"/> は optional な id provider 引数を持つため
    /// (VContainer は C# 既定引数を尊重しない)、factory delegate で <see cref="GraphCommandDispatcher"/>
    /// のみ渡して既定の GUID provider を使わせる。<see cref="GraphCommandDispatcher"/> は GraphInstaller が登録済。
    /// </remarks>
    internal sealed class InteractionGraphAdapterInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register(r => new SpatialIntentToCommandTranslator(r.Resolve<GraphCommandDispatcher>()),
                Lifetime.Singleton);

            // F-Vf-d.2: NodeSpawnService を Rhizomode.Interaction.GraphAdapter へ移送 (Codex review #4 解消)。
            builder.Register<NodeSpawnService>(Lifetime.Singleton);
        }
    }
}
