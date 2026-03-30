#nullable enable

namespace Rhizomode.Core
{
    /// <summary>
    /// 入力ポートのインターフェース。OnNextで値を受け取る。
    /// </summary>
    public interface IInputPort
    {
        ParamType Type { get; }

        /// <summary>
        /// 値を受け取る。valueはParamTypeに対応する型でboxedされている。
        /// </summary>
        void OnNext(object value);
    }
}
