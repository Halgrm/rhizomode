#nullable enable

using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Model
{
    /// <summary>
    /// ノードパネル内に波形表示を描画するためのインターフェース。
    /// 波形バッファを毎フレーム参照して描画する。
    /// </summary>
    public interface IInlineWaveform
    {
        /// <summary>波形バッファ。描画時に参照される。nullなら描画しない。</summary>
        float[]? WaveformBuffer { get; }

        /// <summary>バッファ内の有効サンプル数。</summary>
        int WaveformLength { get; }

        /// <summary>リングバッファの書き込み位置（読み取り開始オフセット）。</summary>
        int WaveformWriteIndex { get; }

        /// <summary>表示用ラベル（例: "Level: 0.42"）。</summary>
        string WaveformLabel { get; }
    }
}
