#nullable enable

using System;

namespace Rhizomode.Core
{
    /// <summary>
    /// 出力ポートのインターフェース。Subscribe経由で入力ポートと接続する。
    /// </summary>
    public interface IOutputPort
    {
        ParamType Type { get; }

        /// <summary>
        /// 入力ポートを購読し、値の伝播を開始する。
        /// 戻り値のIDisposableをDisposeすると接続が切れる。
        /// </summary>
        IDisposable Subscribe(IInputPort input);
    }
}
