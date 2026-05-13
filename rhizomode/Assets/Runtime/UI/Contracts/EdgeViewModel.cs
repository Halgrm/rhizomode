#nullable enable

namespace Rhizomode.UI.Contracts
{
    /// <summary>
    /// 1 エッジの描画用 DTO。
    /// </summary>
    public sealed record EdgeViewModel(
        string EdgeId,
        string FromNodeId,
        string FromPortName,
        string ToNodeId,
        string ToPortName);
}
