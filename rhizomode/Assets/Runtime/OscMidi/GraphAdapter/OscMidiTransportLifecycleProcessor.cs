#nullable enable

using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using Rhizomode.OscMidi.Contracts;

namespace Rhizomode.OscMidi.GraphAdapter
{
    /// <summary>
    /// <see cref="IOscSourceConsumer"/> / <see cref="IMidiSourceConsumer"/> を実装するノードに
    /// OSC / MIDI transport を Setup 前に注入する LifecycleProcessor。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 <c>OscServer.Instance</c> / <c>MidiServer.Instance</c> singleton
    /// 直参照を解消。<see cref="SceneLoaderLifecycleProcessor"/> と同構造で、具体ノード型
    /// (<c>OscReceiverNode</c> / <c>MidiCCNode</c>) を知らずに polymorphic に注入する。
    /// NodeRuntime の processors 配列に登録され、BeforeSetup が自動駆動される。
    /// </remarks>
    public sealed class OscMidiTransportLifecycleProcessor : INodeLifecycleProcessor
    {
        private readonly IOscSource? _oscSource;
        private readonly IMidiSource? _midiSource;

        public OscMidiTransportLifecycleProcessor(IOscSource? oscSource, IMidiSource? midiSource)
        {
            _oscSource = oscSource;
            _midiSource = midiSource;
        }

        public void BeforeSetup(NodeBase node, NodeInitMode mode)
        {
            if (node is IOscSourceConsumer oscConsumer)
                oscConsumer.OscSource = _oscSource;
            if (node is IMidiSourceConsumer midiConsumer)
                midiConsumer.MidiSource = _midiSource;
        }

        public void AfterSetup(NodeBase node, NodeInitMode mode) { }

        public void AfterDeserialize(GraphState state) { }
    }
}
