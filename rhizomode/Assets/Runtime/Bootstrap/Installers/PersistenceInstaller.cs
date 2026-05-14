#nullable enable

using Rhizomode.Graph.Serialization;
using Rhizomode.Persistence.Contracts;
using Rhizomode.Persistence.Json;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — Persistence 系の pure-C# サービスを構築・登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>PersistenceInstaller</c>。V2b で GameBootstrap が
    /// <c>GraphAdapterWiring.ConfigureSaveLoad</c> 内で new していた I/O 層の構築を吸収した。
    ///
    /// 登録するのは scene 非依存の 3 つ:
    /// <list type="bullet">
    ///   <item><see cref="ISavePathProvider"/> (JsonSavePathProvider)</item>
    ///   <item><see cref="IGraphRepository"/> (JsonGraphRepository)</item>
    ///   <item><see cref="GraphHydrator"/></item>
    /// </list>
    /// <c>HydrationPlanExecutor</c> は scene-ref 依存の <c>NodeRuntime</c> を必要とするため当面
    /// GameBootstrap が構築する (NodeRuntime の Installer 化は V3)。GraphSaveLoadManager への
    /// 注入 (Configure 呼び出し) も scene MonoBehaviour 操作のため GameBootstrap 残置。
    /// </remarks>
    internal sealed class PersistenceInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            var pathProvider = new JsonSavePathProvider();
            builder.RegisterInstance<ISavePathProvider>(pathProvider);
            builder.RegisterInstance<IGraphRepository>(new JsonGraphRepository(pathProvider));
            builder.RegisterInstance(new GraphHydrator());
        }
    }
}
