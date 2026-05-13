#nullable enable

namespace Rhizomode.SharedKernel
{
    /// <summary>
    /// 型ごとのデフォルト値。エラー時のフォールバックに使用。
    /// </summary>
    public static class ParamDefaults
    {
        public const float Float = 0f;
        public static readonly RzColor Color = RzColor.Black;
        public const bool Bool = false;

        /// <summary>
        /// 指定された型のデフォルト値を <see cref="ParamValue"/> で返す。
        /// </summary>
        public static ParamValue GetDefault(ParamType type) => type switch
        {
            ParamType.Float => ParamValue.Float(Float),
            ParamType.Color => ParamValue.Color(Color),
            ParamType.Bool => ParamValue.Bool(Bool),
            _ => ParamValue.Float(Float)
        };

        /// <summary>
        /// 指定された型のデフォルト値を boxed object で返す (legacy 互換、Phase 4 で削除予定)。
        /// </summary>
        public static object GetDefaultBoxed(ParamType type) => type switch
        {
            ParamType.Float => (object)Float,
            ParamType.Color => Color,
            ParamType.Bool => (object)Bool,
            _ => Float
        };
    }
}
