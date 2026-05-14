#nullable enable

using Rhizomode.Ableton.Contracts;
using Rhizomode.Observability.Contracts;

namespace Rhizomode.Ableton.GraphAdapter
{
    /// <summary>
    /// Ableton transport の health を <see cref="IHealthMonitor"/> として公開する adapter。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 <c>AbletonLink.Instance</c> singleton poll を解消し、
    /// 構築時に注入された <see cref="IAbletonLink"/> 参照を直接保持する。
    /// </remarks>
    public sealed class AbletonLinkHealth : IHealthMonitor
    {
        public const string Id = "Ableton";

        private readonly IAbletonLink? _link;

        public AbletonLinkHealth(IAbletonLink? link)
        {
            _link = link;
        }

        public string SystemId => Id;

        public HealthSnapshot CurrentSnapshot()
        {
            if (_link == null)
                return new HealthSnapshot(Id, HealthStatus.Unknown, "AbletonLink not running");

            return new HealthSnapshot(Id, HealthStatus.Healthy, "AbletonLink active");
        }
    }
}
