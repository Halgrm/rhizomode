#nullable enable

namespace Rhizomode.OscMidi.Contracts
{
    /// <summary>
    /// <see cref="IOscSource"/> を必要とするノードが実装する契約。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: <c>OscMidi.GraphAdapter.OscMidiTransportLifecycleProcessor</c> が
    /// 本 interface で型を判定し、Setup 前に OSC source を注入する。これにより node は
    /// 具体 transport 型 (<c>OscServer</c>) を知らずに済む。
    /// </remarks>
    public interface IOscSourceConsumer
    {
        IOscSource? OscSource { get; set; }
    }
}
