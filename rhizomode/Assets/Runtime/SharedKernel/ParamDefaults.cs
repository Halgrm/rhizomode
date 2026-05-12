#nullable enable

using UnityEngine;

namespace Rhizomode.SharedKernel
{
    /// <summary>
    /// 型ごとのデフォルト値。エラー時のフォールバックに使用。
    /// </summary>
    public static class ParamDefaults
    {
        public const float Float = 0f;
        public static readonly Color Color = UnityEngine.Color.black;
        public const bool Bool = false;

        /// <summary>
        /// 指定された型のデフォルト値をboxedで返す。
        /// </summary>
        public static object GetDefault(ParamType type) => type switch
        {
            ParamType.Float => Float,
            ParamType.Color => Color,
            ParamType.Bool => Bool,
            _ => Float
        };
    }
}
