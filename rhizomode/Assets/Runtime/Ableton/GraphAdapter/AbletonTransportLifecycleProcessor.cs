#nullable enable

using Rhizomode.Ableton.Contracts;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;

namespace Rhizomode.Ableton.GraphAdapter
{
    /// <summary>
    /// <see cref="IAbletonLinkConsumer"/> を実装するノードに AbletonLink を Setup 前に注入する
    /// LifecycleProcessor。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 <c>AbletonLink.Instance</c> singleton 直参照を解消。
    /// <c>SceneLoaderLifecycleProcessor</c> / <c>OscMidiTransportLifecycleProcessor</c> と
    /// 同構造で、具体ノード型 (<c>AbletonTransportNode</c> / <c>AbletonTempoNode</c> /
    /// <c>AbletonTrackVolumeNode</c> / <c>AbletonClipFireNode</c>) を知らずに polymorphic に注入する。
    /// NodeRuntime の processors 配列に登録され、BeforeSetup が自動駆動される。
    /// </remarks>
    public sealed class AbletonTransportLifecycleProcessor : INodeLifecycleProcessor
    {
        private readonly IAbletonLink? _link;

        public AbletonTransportLifecycleProcessor(IAbletonLink? link)
        {
            _link = link;
        }

        public void BeforeSetup(NodeBase node, NodeInitMode mode)
        {
            if (node is IAbletonLinkConsumer consumer)
                consumer.Link = _link;
        }

        public void AfterSetup(NodeBase node, NodeInitMode mode) { }

        public void AfterDeserialize(GraphState state) { }
    }
}
