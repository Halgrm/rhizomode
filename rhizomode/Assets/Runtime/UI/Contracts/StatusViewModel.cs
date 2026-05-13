#nullable enable

namespace Rhizomode.UI.Contracts
{
    /// <summary>
    /// StatusPanel 表示用の集約 DTO (Phase 5)。
    /// </summary>
    /// <remarks>
    /// 旧 StatusPanelController は <c>GraphState</c> を直接参照していた。Phase 5 で
    /// <c>GraphStateToViewModelProjector</c> が本 DTO に集約し、UI.Presentation 側が
    /// Graph 直触りを停止する。
    /// </remarks>
    public sealed record StatusViewModel(
        int NodeCount,
        int EdgeCount,
        float Bpm,
        float FrameRate,
        string AudioDeviceName,
        bool AudioConnected);
}
