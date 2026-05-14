#nullable enable

using Rhizomode.Ableton.Transport;
using Rhizomode.Observability.Contracts;

namespace Rhizomode.Ableton.GraphAdapter
{
    /// <summary>
    /// <see cref="AbletonLink"/> の health を <see cref="IHealthMonitor"/> として公開する adapter。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10E: AbletonLink も singleton Instance ベース。Phase 12 で
    /// singleton 解消 + AbletonOscBridge 3 分割と同時に instance 直接保持に書き換える。
    /// </remarks>
    public sealed class AbletonLinkHealth : IHealthMonitor
    {
        public const string Id = "Ableton";

        public string SystemId => Id;

        public HealthSnapshot CurrentSnapshot()
        {
            var instance = AbletonLink.Instance;
            if (instance == null)
                return new HealthSnapshot(Id, HealthStatus.Unknown, "AbletonLink not running");

            return new HealthSnapshot(Id, HealthStatus.Healthy, "AbletonLink active");
        }
    }
}
