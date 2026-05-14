#nullable enable

using Unity.Collections;
using UnityEngine;

namespace Rhizomode.Audio.Analysis.Spectrum
{
    /// <summary>
    /// スペクトル / 波形データの downsample / band-level 計算ヘルパー (pure static)。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10C: AudioAnalyzer 内に散在していたバッファ操作 logic を抽出。
    /// LASP の <c>NativeSlice&lt;float&gt;</c> を入力として受け、Unity 依存度を最小化する。
    /// </remarks>
    public static class SpectrumOps
    {
        /// <summary>
        /// source の logSpectrum (NativeSlice) を dest 配列に downsample してコピーする。
        /// </summary>
        public static void Downsample(NativeSlice<float> source, float[] dest)
        {
            if (source.Length == 0 || dest.Length == 0) return;
            var step = (float)source.Length / dest.Length;
            for (var i = 0; i < dest.Length; i++)
            {
                var idx = Mathf.Min((int)(i * step), source.Length - 1);
                dest[i] = source[idx];
            }
        }

        /// <summary>
        /// 指定周波数帯域のスペクトルレベル平均を返す。
        /// </summary>
        /// <param name="spectrum">線形スペクトル (NativeSlice)。</param>
        /// <param name="sampleRate">capture device の sample rate。</param>
        /// <param name="freqMin">下限 Hz。</param>
        /// <param name="freqMax">上限 Hz。</param>
        public static float BandLevel(NativeSlice<float> spectrum, int sampleRate, float freqMin, float freqMax)
        {
            if (spectrum.Length == 0 || sampleRate == 0) return 0f;

            float binWidth = (sampleRate / 2f) / spectrum.Length;
            var binMin = Mathf.Clamp(Mathf.RoundToInt(freqMin / binWidth), 0, spectrum.Length - 1);
            var binMax = Mathf.Clamp(Mathf.RoundToInt(freqMax / binWidth), binMin, spectrum.Length - 1);

            var sum = 0f;
            for (var i = binMin; i <= binMax; i++)
                sum += spectrum[i];

            return sum / (binMax - binMin + 1);
        }
    }
}
