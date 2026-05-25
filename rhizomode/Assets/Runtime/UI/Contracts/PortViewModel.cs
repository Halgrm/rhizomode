#nullable enable

using Rhizomode.SharedKernel;

namespace Rhizomode.UI.Contracts
{
    /// <summary>
    /// 1 ポートの描画用 DTO (入出力共通)。
    /// </summary>
    /// <remarks>
    /// <see cref="PortType"/> は <c>SharedKernel.ParamType</c> を流用 (Float/Color/Bool)。
    /// <see cref="Unit"/> は UI ラベル表示用のメタデータで、接続バリデーションには使用しない。
    /// </remarks>
    public sealed record PortViewModel(
        string PortName,
        ParamType PortType,
        bool IsConnected,
        PortUnit Unit = PortUnit.None);
}
