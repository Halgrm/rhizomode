#nullable enable

namespace Rhizomode.SharedKernel
{
    /// <summary>
    /// UnityEngine 非依存のカラー値 DTO (R, G, B, A の 4 float)。
    /// </summary>
    /// <remarks>
    /// 値ポリシー (Plan v5.2-2):
    /// - 値をそのまま保持する。clamp/normalize しない。値域 0..1 を超えても拒否しない。
    /// - color space (linear/gamma) は型に持たない。呼び出し側が文脈で扱う。
    /// - 等価性は record struct の field 完全一致 (epsilon を組み込まない)。
    /// - 近似比較が必要な場合は <see cref="RzMath.Approximately"/> を使う。
    /// </remarks>
    public readonly record struct RzColor(float R, float G, float B, float A)
    {
        public static readonly RzColor Black = new(0f, 0f, 0f, 1f);
        public static readonly RzColor White = new(1f, 1f, 1f, 1f);
        public static readonly RzColor Transparent = new(0f, 0f, 0f, 0f);
    }
}
