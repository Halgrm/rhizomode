#nullable enable

using Rhizomode.Observability.Contracts;
using Rhizomode.OscMidi.Transport;

namespace Rhizomode.OscMidi.GraphAdapter
{
    /// <summary>
    /// <see cref="MidiServer"/> の health を <see cref="IHealthMonitor"/> として公開する adapter。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10E: MidiServer も singleton Instance ベース。Phase 12 で
    /// singleton 解消後に instance を直接保持するよう書き換える。
    /// </remarks>
    public sealed class MidiServerHealth : IHealthMonitor
    {
        public const string Id = "MIDI";

        public string SystemId => Id;

        public HealthSnapshot CurrentSnapshot()
        {
            var instance = MidiServer.Instance;
            if (instance == null)
                return new HealthSnapshot(Id, HealthStatus.Unknown, "MidiServer not running");

            return new HealthSnapshot(Id, HealthStatus.Healthy, "MidiServer active");
        }
    }
}
