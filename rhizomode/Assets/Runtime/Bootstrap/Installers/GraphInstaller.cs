#nullable enable

using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Mutation;
using Rhizomode.Interaction.GraphAdapter;
using Rhizomode.NodeCatalog.Runtime;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — Graph 系の pure-C# サービスを構築・登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>GraphInstaller</c>。V2b で GameBootstrap が new していた
    /// <c>GraphAdapterWiring</c> の構築責務を吸収した (GraphAdapterWiring.cs は削除)。
    ///
    /// <see cref="Install"/> は登録のみを行う pure な処理 — 構築する各オブジェクトの ctor は
    /// フィールド代入のみで副作用を持たない。依存チェーン
    /// (factory → applier → dispatcher → translator / queue) は手動で組み、GameBootstrap /
    /// EntryPointsInstaller が必要とするものだけを container に登録する:
    /// <list type="bullet">
    ///   <item><see cref="INodeFactory"/> — saveLoad.Configure / hydration 用</item>
    ///   <item><see cref="GraphEventBus"/> — NodeRuntime ctor 用 (Dispose は当面 GameBootstrap)</item>
    ///   <item><see cref="SpatialIntentToCommandTranslator"/> — 3 handler の IntentSink 用</item>
    ///   <item><see cref="MainThreadGraphCommandQueue"/> — EntryPointsInstaller の MainThreadCommandTicker 用</item>
    /// </list>
    /// <see cref="GraphMutationApplier"/> / <see cref="GraphCommandDispatcher"/> は外部から参照
    /// されない内部 plumbing のため登録しない。
    /// </remarks>
    internal sealed class GraphInstaller : IInstaller
    {
        private readonly GraphState _graphState;

        public GraphInstaller(GraphState graphState)
        {
            _graphState = graphState;
        }

        public void Install(IContainerBuilder builder)
        {
            var scanner = new NodeTypeAttributeScanner();
            var staticFactory = new AttributeScannerNodeFactory(scanner.Scan());
            INodeFactory factory = new CompositeNodeFactory(new INodeFactory[] { staticFactory });

            var eventBus = new GraphEventBus();
            var applier = new GraphMutationApplier(_graphState, factory, eventBus);
            var dispatcher = new GraphCommandDispatcher(applier);

            builder.RegisterInstance(factory);
            builder.RegisterInstance(eventBus);
            builder.RegisterInstance(new SpatialIntentToCommandTranslator(dispatcher));
            builder.RegisterInstance(new MainThreadGraphCommandQueue(dispatcher));
        }
    }
}
