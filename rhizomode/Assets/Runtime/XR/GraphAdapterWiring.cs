#nullable enable

using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Mutation;
using Rhizomode.Graph.Runtime;
using Rhizomode.Graph.Serialization;
using Rhizomode.Interaction.GraphAdapter;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Persistence.Contracts;
using Rhizomode.Persistence.Json;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// Phase 5/7 で導入された GraphAdapter (Translator / Dispatcher / Repository / Hydrator /
    /// Executor) の wiring を集約する builder。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 F-8.2 抽出 4/N (Round F3): GameBootstrap の EnsureSharedFactoryAndEventBus +
    /// WireIntentSink + ConfigureSaveLoad の 3 メソッドを統合。Phase 8 で VContainer Installer に
    /// 移行する際の足場として、まず手動 wiring を一箇所に集める。
    ///
    /// 配置: 暫定 Rhizomode.XR (Persistence.Json + Interaction.GraphAdapter + UI に依存)。
    /// Plan v5.3 の正規 location は Bootstrap/Installer asmdef。
    /// </remarks>
    public sealed class GraphAdapterWiring
    {
        public CompositeNodeFactory Factory { get; }
        public GraphEventBus EventBus { get; }
        public SpatialIntentToCommandTranslator Translator { get; }

        public GraphAdapterWiring(GraphState graphState)
        {
            var scanner = new NodeTypeAttributeScanner();
            var staticFactory = new AttributeScannerNodeFactory(scanner.Scan());
            Factory = new CompositeNodeFactory(new INodeFactory[] { staticFactory });
            EventBus = new GraphEventBus();

            var applier = new GraphMutationApplier(graphState, Factory, EventBus);
            var dispatcher = new GraphCommandDispatcher(applier);
            Translator = new SpatialIntentToCommandTranslator(dispatcher);
        }

        /// <summary>
        /// GraphSaveLoadManager に Persistence + Hydrator + Executor + Factory を注入する。
        /// </summary>
        public void ConfigureSaveLoad(GraphSaveLoadManager saveLoad, NodeRuntime nodeRuntime)
        {
            var pathProvider = new JsonSavePathProvider();
            var repository = new JsonGraphRepository(pathProvider);
            var hydrator = new GraphHydrator();
            var executor = new HydrationPlanExecutor(nodeRuntime);
            saveLoad.Configure(repository, hydrator, executor, Factory, pathProvider);
        }
    }
}
