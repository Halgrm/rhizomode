#nullable enable

namespace Rhizomode.SharedKernel
{
    /// <summary>
    /// ノードのパラメータを名前で読み書きする contract。
    /// <see cref="ParamValue"/> 経由で型安全に値をやり取りする。
    /// </summary>
    /// <remarks>
    /// Phase 4 で各 NodeBase 派生クラスが実装する。Phase 6 の
    /// <c>NodeDefaultLifecycleProcessor</c> がノード具体型を知らずに default 値を適用するために使う。
    /// </remarks>
    public interface INodeParamAccessor
    {
        /// <summary>パラメータを設定する。未知の paramName は false を返す。</summary>
        bool TrySetParam(string paramName, ParamValue value);

        /// <summary>パラメータを取得する。未知の paramName は false を返す。</summary>
        bool TryGetParam(string paramName, out ParamValue value);
    }
}
