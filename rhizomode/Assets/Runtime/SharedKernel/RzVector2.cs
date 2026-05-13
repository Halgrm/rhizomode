#nullable enable

using System;

namespace Rhizomode.SharedKernel
{
    /// <summary>
    /// UnityEngine 非依存の 2D ベクトル DTO (X, Y の 2 float)。
    /// </summary>
    /// <remarks>
    /// 値ポリシー: clamp/normalize しない。等価性は field 完全一致。
    /// </remarks>
    public readonly struct RzVector2 : IEquatable<RzVector2>
    {
        public readonly float X;
        public readonly float Y;

        public RzVector2(float x, float y) { X = x; Y = y; }

        public static readonly RzVector2 Zero = new(0f, 0f);
        public static readonly RzVector2 One = new(1f, 1f);

        public bool Equals(RzVector2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is RzVector2 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static bool operator ==(RzVector2 a, RzVector2 b) => a.Equals(b);
        public static bool operator !=(RzVector2 a, RzVector2 b) => !a.Equals(b);
    }
}
