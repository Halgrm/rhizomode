#nullable enable

namespace Rhizomode.SharedKernel
{
    /// <summary>
    /// UnityEngine 非依存の四元数 DTO (X, Y, Z, W の 4 float)。
    /// </summary>
    /// <remarks>
    /// 値ポリシー: normalized を保証しない (呼び出し側で正規化)。等価性は field 完全一致。
    /// </remarks>
    public readonly record struct RzQuaternion(float X, float Y, float Z, float W)
    {
        public static readonly RzQuaternion Identity = new(0f, 0f, 0f, 1f);
    }
}
