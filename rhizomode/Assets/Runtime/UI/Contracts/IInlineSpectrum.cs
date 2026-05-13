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
    }
}
