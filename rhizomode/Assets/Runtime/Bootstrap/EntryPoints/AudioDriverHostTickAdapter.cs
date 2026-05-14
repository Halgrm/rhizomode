#nullable enable

using Rhizomode.Audio.GraphAdapter;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.EntryPoints
{
    /// <summary>
    /// VContainer ITickable adapter — 毎フレーム AudioDriverHost の Tick を駆動する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 EntryPoints tick order #2。MainThreadCommandTicker の後、HealthAggregator の前。
    ///
    /// V1 transitional shape: AudioDriverHost は <see cref="AudioAnalyzer"/> の late-binding
    /// (SerializeField / ランタイム差替え) に対応するため <see cref="AudioDriverBehaviour"/> が
    /// lazy 構築する。本 adapter はその <c>Tick()</c> を呼ぶだけ。Installer が AudioDriverHost を
    /// 直接構築する形 (V2+) になったら本 adapter は host を直接保持する。
    /// 詳細は EntryPoints/TickOrder.md を参照。
    /// </remarks>
    public sealed class AudioDriverHostTickAdapter : ITickable
    {
        private readonly AudioDriverBehaviour _driver;

        public AudioDriverHostTickAdapter(AudioDriverBehaviour driver)
        {
            _driver = driver;
        }

        public void Tick() => _driver.Tick();
    }
}
