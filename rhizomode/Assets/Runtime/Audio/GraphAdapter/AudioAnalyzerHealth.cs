#nullable enable

using Rhizomode.Audio.Analysis;
using Rhizomode.Observability.Contracts;

namespace Rhizomode.Audio.GraphAdapter
{
    /// <summary>
    /// <see cref="AudioAnalyzer"/> の health を <see cref="IHealthMonitor"/> として公開する adapter。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10E: HealthAggregator が tick で polling し、状態変化があれば
    /// OnHealthChange に emit。fail-open 原則 (Failed でも Video 継続) を維持。
    ///
    /// 現状の最小実装: IsInitialized + CurrentDevice の有無で Healthy/Unknown を判定。
    /// Phase 13 以降で packet rate / latency 等の詳細指標を追加可能。
    /// </remarks>
    public sealed class AudioAnalyzerHealth : IHealthMonitor
    {
        public const string Id = "Audio";

        private readonly AudioAnalyzer _analyzer;

        public AudioAnalyzerHealth(AudioAnalyzer analyzer)
        {
            _analyzer = analyzer;
        }

        public string SystemId => Id;

        public HealthSnapshot CurrentSnapshot()
        {
            if (!_analyzer.IsInitialized)
                return new HealthSnapshot(Id, HealthStatus.Unknown, "Analyzer not initialized");

            var device = _analyzer.CurrentDevice;
            if (string.IsNullOrEmpty(device))
                return new HealthSnapshot(Id, HealthStatus.Unknown, "No device assigned");

            return new HealthSnapshot(Id, HealthStatus.Healthy, $"Device: {device}");
        }
    }
}
