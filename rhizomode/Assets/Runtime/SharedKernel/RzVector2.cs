#nullable enable

namespace Rhizomode.SharedKernel
{
    /// <summary>
    /// UnityEngine 非依存の 2D ベクトル DTO (X, Y の 2 float)。
    /// </summary>
    /// <remarks>
    /// 値ポリシー: clamp/normalize しない。等価性は field 完全一致。
    /// </remarks>
    public readonly record struct RzVector2(float X, float Y)
    {
        public static readonly RzVector2 Zero = new(0f, 0f);
        public static readonly RzVector2 One = new(1f, 1f);
    }
}
