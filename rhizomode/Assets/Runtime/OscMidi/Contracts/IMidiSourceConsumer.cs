#nullable enable

namespace Rhizomode.OscMidi.Contracts
{
    /// <summary>
    /// <see cref="IMidiSource"/> を必要とするノードが実装する契約。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: <c>OscMidi.GraphAdapter.OscMidiTransportLifecycleProcessor</c> が
    /// 本 interface で型を判定し、Setup 前に MIDI source を注入する。これにより node は
    /// 具体 transport 型 (<c>MidiServer</c>) を知らずに済む。
    /// </remarks>
    public interface IMidiSourceConsumer
    {
        IMidiSource? MidiSource { get; set; }
    }
}
