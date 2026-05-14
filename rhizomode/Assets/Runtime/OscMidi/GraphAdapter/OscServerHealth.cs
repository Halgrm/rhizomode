#nullable enable

using Rhizomode.Observability.Contracts;
using Rhizomode.OscMidi.Contracts;

namespace Rhizomode.OscMidi.GraphAdapter
{
    /// <summary>
    /// OSC transport の health を <see cref="IHealthMonitor"/> として公開する adapter。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 <c>OscServer.Instance</c> singleton poll を解消し、
    /// 構築時に注入された <see cref="IOscSource"/> 参照を直接保持する。
    /// 最小実装: source の有無で Healthy/Unknown 判定。
    /// </remarks>
    public sealed class OscServerHealth : IHealthMonitor
    {
        public const string Id = "OSC";

        private readonly IOscSource? _source;

        public OscServerHealth(IOscSource? source)
        {
            _source = source;
        }

        public string SystemId => Id;

        public HealthSnapshot CurrentSnapshot()
        {
            if (_source == null)
                return new HealthSnapshot(Id, HealthStatus.Unknown, "OscServer not running");

            return new HealthSnapshot(Id, HealthStatus.Healthy, "OscServer active");
        }
    }
}
