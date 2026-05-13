#nullable enable

using Rhizomode.SharedKernel;

namespace Rhizomode.UI.Contracts
{
    /// <summary>
    /// 1 ポートの描画用 DTO (入出力共通)。
    /// </summary>
    /// <remarks>
    /// <see cref="PortType"/> は <c>SharedKernel.ParamType</c> を流用 (Float/Color/Bool)。
    /// </remarks>
    public sealed record PortViewModel(
        string PortName,
        ParamType PortType,
        bool IsConnected);
}
