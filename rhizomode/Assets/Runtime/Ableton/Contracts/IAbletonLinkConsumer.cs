#nullable enable

namespace Rhizomode.Ableton.Contracts
{
    /// <summary>
    /// IAbletonLink を必要とするノードが実装する契約。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: AbletonTransportLifecycleProcessor が本 interface で型を判定し、
    /// Setup 前に AbletonLink を注入する。これにより node は具体 transport 型を知らずに済む。
    /// </remarks>
    public interface IAbletonLinkConsumer
    {
        IAbletonLink? Link { get; set; }
    }
}
