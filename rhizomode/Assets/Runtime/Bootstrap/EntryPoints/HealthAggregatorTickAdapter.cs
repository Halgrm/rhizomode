#nullable enable

using Rhizomode.Observability.Runtime;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.EntryPoints
{
    /// <summary>
    /// VContainer ITickable adapter — 低頻度で <see cref="HealthAggregator.Tick"/> を駆動する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 EntryPoints tick order #3 (最後)。
    ///
    /// <see cref="HealthAggregator"/> の Tick は各 monitor の <c>CurrentSnapshot</c> を都度参照し
    /// HealthSnapshot record を alloc し得るため、毎フレームではなく
    /// <see cref="TickIntervalFrames"/> 間隔で polling する (90fps で約 3Hz)。
    /// この throttle は Phase 12 で GameBootstrap.Update に入っていたものを本 adapter に移設した。
    /// 詳細は EntryPoints/TickOrder.md を参照。
    /// </remarks>
    public sealed class HealthAggregatorTickAdapter : ITickable
    {
        /// <summary>HealthAggregator.Tick の polling 間隔 (フレーム数)。</summary>
        private const int TickIntervalFrames = 30;

        private readonly HealthAggregator _aggregator;
        private int _frameCounter;

        public HealthAggregatorTickAdapter(HealthAggregator aggregator)
        {
            _aggregator = aggregator;
        }

        public void Tick()
        {
            if (++_frameCounter < TickIntervalFrames) return;
            _frameCounter = 0;
            _aggregator.Tick();
        }
    }
}
