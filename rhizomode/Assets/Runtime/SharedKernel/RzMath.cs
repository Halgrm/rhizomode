#nullable enable

using System;

namespace Rhizomode.SharedKernel
{
    /// <summary>
    /// SharedKernel 配下の最小 math ヘルパ。
    /// </summary>
    /// <remarks>
    /// 許可された public method は <see cref="Approximately"/> と <see cref="IsFinite"/> のみ (Plan v5.3-3 でホワイトリスト化)。
    /// Lerp / SmoothStep / Clamp / HSV 変換 / Quaternion ヘルパ / Cross / Dot などは禁止。
    /// 必要な場合は呼び出し側 (UI.Presentation 等) で extension method を実装すること。
    /// </remarks>
    public static class RzMath
    {
        public static bool Approximately(float a, float b, float epsilon = 1e-6f)
        {
            return MathF.Abs(a - b) <= epsilon;
        }

        public static bool Approximately(RzVector3 a, RzVector3 b, float epsilon = 1e-6f)
        {
            return Approximately(a.X, b.X, epsilon)
                && Approximately(a.Y, b.Y, epsilon)
                && Approximately(a.Z, b.Z, epsilon);
        }

        public static bool Approximately(RzVector2 a, RzVector2 b, float epsilon = 1e-6f)
        {
            return Approximately(a.X, b.X, epsilon)
                && Approximately(a.Y, b.Y, epsilon);
        }

        public static bool Approximately(RzColor a, RzColor b, float epsilon = 1e-6f)
        {
            return Approximately(a.R, b.R, epsilon)
                && Approximately(a.G, b.G, epsilon)
                && Approximately(a.B, b.B, epsilon)
                && Approximately(a.A, b.A, epsilon);
        }

        public static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        public static bool IsFinite(RzVector3 v) => IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);

        public static bool IsFinite(RzVector2 v) => IsFinite(v.X) && IsFinite(v.Y);

        public static bool IsFinite(RzColor c) =>
            IsFinite(c.R) && IsFinite(c.G) && IsFinite(c.B) && IsFinite(c.A);
    }
}
