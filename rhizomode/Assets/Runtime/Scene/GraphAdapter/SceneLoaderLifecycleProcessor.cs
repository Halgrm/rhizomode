#nullable enable

using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using Rhizomode.Scene.Contracts;

namespace Rhizomode.Scene.GraphAdapter
{
    /// <summary>
    /// <see cref="ISceneLoaderConsumer"/> を実装するノードに <see cref="ISceneLoader"/> を注入する。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 6: GameBootstrap.OnScrollMenuNodeSelected / ReinjectModulesAfterLoad で
    /// switch(SceneSwitchNode/SceneTriggerNode) → loader 注入 を行っていたロジックを本クラスに集約。
    /// 具体ノード型を知らず、<see cref="ISceneLoaderConsumer"/> interface 経由で polymorphic に注入。
    ///
    /// 注: Phase 8 で NodeRuntime + Installer 経由の自動駆動に切替予定。
    /// </remarks>
    public sealed class SceneLoaderLifecycleProcessor : INodeLifecycleProcessor
    {
        private readonly ISceneLoader? _loader;

        public SceneLoaderLifecycleProcessor(ISceneLoader? loader)
        {
            _loader = loader;
        }

        public void BeforeSetup(NodeBase node, NodeInitMode mode)
        {
            // ISceneLoader 注入は Setup より前で済ませる (Setup 内で Loader 参照する node が
            // 将来現れた場合の正しい順序)。
            if (node is ISceneLoaderConsumer consumer)
            {
                consumer.Loader = _loader;
            }
        }

        public void AfterSetup(NodeBase node, NodeInitMode mode) { }

        public void AfterDeserialize(GraphState state) { }
    }
}
