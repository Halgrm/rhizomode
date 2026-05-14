#nullable enable

namespace Rhizomode.Observability.Contracts
{
    /// <summary>
    /// 監視対象 system の health 1 サンプル。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10: HealthAggregator が tick で IHealthMonitor.CurrentSnapshot() を
    /// 集めて状態管理。record class なので structural equality で前回値との dedupe が可能。
    /// </remarks>
    public sealed record HealthSnapshot(
        string SystemId,
        HealthStatus Status,
        string? Message);
}
