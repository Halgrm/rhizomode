#nullable enable

using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Mutation;
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
    /// V3b: §15「Installer は登録のみ」へ寄せ、依存チェーンの手動構築を撤廃。
    /// <see cref="GraphState"/> / <see cref="INodeFactory"/> を instance 登録し、
    /// <see cref="GraphEventBus"/> / <see cref="GraphMutationApplier"/> /
    /// <see cref="GraphCommandDispatcher"/> / <see cref="SpatialIntentToCommandTranslator"/> /
    /// <see cref="MainThreadGraphCommandQueue"/> は <see cref="Lifetime.Singleton"/> 登録で
    /// VContainer の ctor injection に解決させる。これにより <see cref="GraphEventBus"/> (IDisposable) の
    /// 所有が container 側に移り、GameBootstrap の OnDestroy での手動 Dispose が不要になる
    /// (V2b の一時的 Plan v5.4 違反 2 件 — 手動構築 / EventBus 非 container 所有 — を解消)。
    ///
    /// <see cref="INodeFactory"/> は <c>scanner.Scan()</c> という method 呼び出しを要するため
    /// 引き続き手動構築し instance 登録する。
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

            builder.RegisterInstance(factory);
            builder.RegisterInstance(_graphState);
            builder.Register<GraphEventBus>(Lifetime.Singleton);
            builder.Register<GraphMutationApplier>(Lifetime.Singleton);

            // GraphCommandDispatcher は optional 引数 (maxHistorySize=64) を持つ。VContainer は
            // C# の既定引数を尊重せず全引数を resolve しようとして失敗するため、factory delegate で
            // 既定値を明示する。SpatialIntentToCommandTranslator の登録は V3c で
            // InteractionGraphAdapterInstaller へ移送済 (本来の bounded context)。
            builder.Register(r => new GraphCommandDispatcher(r.Resolve<GraphMutationApplier>()),
                Lifetime.Singleton);

            builder.Register<MainThreadGraphCommandQueue>(Lifetime.Singleton);
        }
    }
}
