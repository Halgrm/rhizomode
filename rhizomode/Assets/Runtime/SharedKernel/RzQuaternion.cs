#nullable enable

using System;

namespace Rhizomode.SharedKernel
{
    /// <summary>
    /// UnityEngine 非依存の四元数 DTO (X, Y, Z, W の 4 float)。
    /// </summary>
    /// <remarks>
    /// 値ポリシー: normalized を保証しない (呼び出し側で正規化)。等価性は field 完全一致。
    /// </remarks>
    public readonly struct RzQuaternion : IEquatable<RzQuaternion>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly float W;

        public RzQuaternion(float x, float y, float z, float w)
        {
            X = x; Y = y; Z = z; W = w;
        }

        public static readonly RzQuaternion Identity = new(0f, 0f, 0f, 1f);

        public bool Equals(RzQuaternion other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;
        public override bool Equals(object? obj) => obj is RzQuaternion q && Equals(q);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
        public static bool operator ==(RzQuaternion a, RzQuaternion b) => a.Equals(b);
        public static bool operator !=(RzQuaternion a, RzQuaternion b) => !a.Equals(b);
    }
}
