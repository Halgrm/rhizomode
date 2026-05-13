#nullable enable

namespace Rhizomode.SharedKernel
{
    /// <summary>
    /// UnityEngine 非依存の 3D ベクトル DTO (X, Y, Z の 3 float)。
    /// </summary>
    /// <remarks>
    /// 値ポリシー: clamp/normalize しない。等価性は field 完全一致。
    /// 近似比較は <see cref="RzMath.Approximately"/> を使う。
    /// </remarks>
    public readonly record struct RzVector3(float X, float Y, float Z)
    {
        public static readonly RzVector3 Zero = new(0f, 0f, 0f);
        public static readonly RzVector3 One = new(1f, 1f, 1f);
    }
}
