#nullable enable

namespace Rhizomode.SharedKernel
{
    /// <summary>
    /// ポート値の物理単位（表示用メタデータ）。
    /// 接続バリデーションには使用せず、UI ラベル表示と単位変換ノードの意図表現のためだけに使う。
    /// </summary>
    public enum PortUnit
    {
        None,
        Hz,
        Bpm,
        Seconds,
        Milliseconds,
        Decibels,
        Normalized,
        Phase,
        Note,
        Degrees,
    }
}
