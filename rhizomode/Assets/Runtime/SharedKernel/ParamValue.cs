#nullable enable

using System;

namespace Rhizomode.SharedKernel
{
    /// <summary>
    /// パラメータ値の discriminated union。
    /// <see cref="ParamType"/> ごとに対応する variant (Float / Color / Bool) を持つ。
    /// </summary>
    /// <remarks>
    /// boxing を避けるため struct で実装。Type プロパティでどの variant が有効か判別する。
    /// UnityEngine.Color への変換は呼び出し側 (UI.Presentation 等) で extension method として実装。
    /// </remarks>
    public readonly record struct ParamValue
    {
        public ParamType Type { get; }
        private readonly float _floatValue;
        private readonly RzColor _colorValue;
        private readonly bool _boolValue;

        private ParamValue(ParamType type, float f, RzColor c, bool b)
        {
            Type = type;
            _floatValue = f;
            _colorValue = c;
            _boolValue = b;
        }

        public static ParamValue Float(float v) => new(ParamType.Float, v, default, default);
        public static ParamValue Color(RzColor v) => new(ParamType.Color, default, v, default);
        public static ParamValue Bool(bool v) => new(ParamType.Bool, default, default, v);

        public float AsFloat => Type == ParamType.Float
            ? _floatValue
            : throw new InvalidOperationException($"ParamValue is {Type}, not Float");

        public RzColor AsColor => Type == ParamType.Color
            ? _colorValue
            : throw new InvalidOperationException($"ParamValue is {Type}, not Color");

        public bool AsBool => Type == ParamType.Bool
            ? _boolValue
            : throw new InvalidOperationException($"ParamValue is {Type}, not Bool");
    }
}
