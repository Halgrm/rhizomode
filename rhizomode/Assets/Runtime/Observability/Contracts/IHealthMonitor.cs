#nullable enable

namespace Rhizomode.Observability.Contracts
{
    /// <summary>
    /// 1 system の health 状態を報告する monitor contract。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10: Audio / OscMidi / Ableton / Scene 等の各 GraphAdapter が
    /// 自 system の adapter を実装し、Bootstrap が HealthAggregator に register する。
    /// CurrentSnapshot は tick ごとに呼ばれる (poll model) ため、内部で長時間ブロックしない。
    /// </remarks>
    public interface IHealthMonitor
    {
        /// <summary>system identifier (例: "Audio", "OSC", "MIDI", "Ableton")。一意。</summary>
        string SystemId { get; }

        /// <summary>現在の health 状態を返す。HealthAggregator.Tick から polling される。</summary>
        HealthSnapshot CurrentSnapshot();
    }
}
