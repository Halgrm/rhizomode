#nullable enable

using Rhizomode.SharedKernel;

namespace Rhizomode.UI.Contracts
{
    /// <summary>
    /// ノードパネル内にスペクトル（バー表示）を描画するためのインターフェース。
    /// </summary>
    public interface IInlineSpectrum
    {
        /// <summary>スペクトルバッファ。描画時に参照される。nullなら描画しない。</summary>
        float[]? SpectrumBuffer { get; }

        /// <summary>バッファ内の有効ビン数。</summary>
        int SpectrumLength { get; }

        /// <summary>表示用ラベル。</summary>
        string SpectrumLabel { get; }

        /// <summary>
        /// データ更新の世代番号 (実装側でデータ書き込みごとに increment)。
        /// </summary>
        /// <remarks>
        /// P2-B: NodeVisualController.LateUpdate が version 不変なら MarkDirtyRepaint を
        /// skip する。AudioDriverHost の throttle (30Hz) と連動して、実際の UI 再描画も間引かれる。
        /// </remarks>
        int SpectrumVersion { get; }
    }
}
