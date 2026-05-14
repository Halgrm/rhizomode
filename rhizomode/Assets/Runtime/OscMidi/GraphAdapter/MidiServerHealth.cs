#nullable enable

using Rhizomode.Observability.Contracts;
using Rhizomode.OscMidi.Contracts;

namespace Rhizomode.OscMidi.GraphAdapter
{
    /// <summary>
    /// MIDI transport の health を <see cref="IHealthMonitor"/> として公開する adapter。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 <c>MidiServer.Instance</c> singleton poll を解消し、
    /// 構築時に注入された <see cref="IMidiSource"/> 参照を直接保持する。
    /// </remarks>
    public sealed class MidiServerHealth : IHealthMonitor
    {
        public const string Id = "MIDI";

        private readonly IMidiSource? _source;

        public MidiServerHealth(IMidiSource? source)
        {
            _source = source;
        }

        public string SystemId => Id;

        public HealthSnapshot CurrentSnapshot()
        {
            if (_source == null)
                return new HealthSnapshot(Id, HealthStatus.Unknown, "MidiServer not running");

            return new HealthSnapshot(Id, HealthStatus.Healthy, "MidiServer active");
        }
    }
}
