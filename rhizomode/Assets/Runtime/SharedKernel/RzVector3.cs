#nullable enable

using System;

namespace Rhizomode.SharedKernel
{
    /// <summary>
    /// UnityEngine 非依存の 3D ベクトル DTO (X, Y, Z の 3 float)。
    /// </summary>
    /// <remarks>
    /// 値ポリシー: clamp/normalize しない。等価性は field 完全一致。
    /// 近似比較は <see cref="RzMath.Approximately"/> を使う。
    /// </remarks>
    public readonly struct RzVector3 : IEquatable<RzVector3>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public RzVector3(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
        }

        public static readonly RzVector3 Zero = new(0f, 0f, 0f);
        public static readonly RzVector3 One = new(1f, 1f, 1f);

        public bool Equals(RzVector3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object? obj) => obj is RzVector3 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public static bool operator ==(RzVector3 a, RzVector3 b) => a.Equals(b);
        public static bool operator !=(RzVector3 a, RzVector3 b) => !a.Equals(b);
    }
}
