#nullable enable

using System;

namespace Rhizomode.SharedKernel
{
    /// <summary>
    /// UnityEngine 非依存のカラー値 DTO (R, G, B, A の 4 float)。
    /// </summary>
    /// <remarks>
    /// 値ポリシー (Plan v5.2-2):
    /// - 値をそのまま保持する。clamp/normalize しない。値域 0..1 を超えても拒否しない。
    /// - color space (linear/gamma) は型に持たない。呼び出し側が文脈で扱う。
    /// - 等価性は field 完全一致 (epsilon を組み込まない)。近似比較は <see cref="RzMath.Approximately"/>。
    /// </remarks>
    public readonly struct RzColor : IEquatable<RzColor>
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;
        public readonly float A;

        public RzColor(float r, float g, float b, float a)
        {
            R = r; G = g; B = b; A = a;
        }

        public static readonly RzColor Black = new(0f, 0f, 0f, 1f);
        public static readonly RzColor White = new(1f, 1f, 1f, 1f);
        public static readonly RzColor Transparent = new(0f, 0f, 0f, 0f);

        public bool Equals(RzColor other) => R == other.R && G == other.G && B == other.B && A == other.A;
        public override bool Equals(object? obj) => obj is RzColor c && Equals(c);
        public override int GetHashCode() => HashCode.Combine(R, G, B, A);
        public static bool operator ==(RzColor a, RzColor b) => a.Equals(b);
        public static bool operator !=(RzColor a, RzColor b) => !a.Equals(b);
    }
}
