#nullable enable

using Rhizomode.Observability.Contracts;
using Rhizomode.OscMidi.Transport;

namespace Rhizomode.OscMidi.GraphAdapter
{
    /// <summary>
    /// <see cref="OscServer"/> の health を <see cref="IHealthMonitor"/> として公開する adapter。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10E: 現状 OscServer は singleton Instance ベース。Phase 12 で
    /// singleton 解消後は instance を直接保持するように書き換える。
    ///
    /// 最小実装: <c>OscServer.Instance != null</c> で Healthy/Unknown 判定。
    /// </remarks>
    public sealed class OscServerHealth : IHealthMonitor
    {
        public const string Id = "OSC";

        public string SystemId => Id;

        public HealthSnapshot CurrentSnapshot()
        {
            var instance = OscServer.Instance;
            if (instance == null)
                return new HealthSnapshot(Id, HealthStatus.Unknown, "OscServer not running");

            return new HealthSnapshot(Id, HealthStatus.Healthy, "OscServer active");
        }
    }
}
