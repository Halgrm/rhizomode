#nullable enable

namespace Rhizomode.Observability.Contracts
{
    /// <summary>
    /// 監視対象 system の health 状態。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10: 各 <c>*.GraphAdapter</c> が IHealthMonitor を実装、
    /// HealthAggregator が tick ごとに polling して状態変化を OnHealthChange に発火する。
    /// fail-open ポリシー: 監視対象が Failed でも Video は止まらない (映像継続原則)。
    /// </remarks>
    public enum HealthStatus
    {
        /// <summary>未確定 (初期化前、もしくは未報告)</summary>
        Unknown = 0,

        /// <summary>正常動作</summary>
        Healthy = 1,

        /// <summary>機能低下 (一部失敗、リトライ中、レイテンシ高など)</summary>
        Degraded = 2,

        /// <summary>致命的失敗 (機能停止、ただし fail-open で Video は継続)</summary>
        Failed = 3
    }
}
