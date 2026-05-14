#nullable enable

using System;

namespace Rhizomode.Audio.Contracts
{
    /// <summary>
    /// 1 frame の audio data を表す ref struct DTO。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10: ref struct で <see cref="ReadOnlySpan{T}"/> を保持することで
    /// audio buffer の zero-copy 共有を実現する。caller は frame を field に保持できないため、
    /// frame の lifetime は 1 tick (Audio.GraphAdapter から IAudioDrivenNode への dispatch
    /// scope) に限定される。
    ///
    /// **ref struct 制約**:
    /// - field に保持不可 (`AudioFrame _last;` は非ref struct class では使えない)
    /// - 型パラメータ不可 (`List&lt;AudioFrame&gt;` は使えない)
    /// - lambda capture 不可
    /// - async / iterator 不可
    /// - 受け渡しは値 / `in` / `ref` パラメータのみ
    /// </remarks>
    public readonly ref struct AudioFrame
    {
        /// <summary>
        /// 直近 N サンプルの波形 (時間領域、-1f..1f)。
        /// </summary>
        public readonly ReadOnlySpan<float> Waveform;

        /// <summary>
        /// 線形スペクトル (周波数領域、bin の正規化された power)。
        /// </summary>
        public readonly ReadOnlySpan<float> Spectrum;

        /// <summary>
        /// 対数 (dB) スペクトル (UI 表示・FFTOutput 用)。
        /// </summary>
        public readonly ReadOnlySpan<float> LogSpectrum;

        /// <summary>全帯域の正規化レベル (0-1)。</summary>
        public readonly float Level;

        /// <summary>低域 (LowPass) の正規化レベル (0-1)。</summary>
        public readonly float LevelLow;

        /// <summary>中域 (BandPass) の正規化レベル (0-1)。</summary>
        public readonly float LevelMid;

        /// <summary>高域 (HighPass) の正規化レベル (0-1)。</summary>
        public readonly float LevelHigh;

        /// <summary>capture device の sample rate (Hz)。</summary>
        public readonly int SampleRate;

        public AudioFrame(
            ReadOnlySpan<float> waveform,
            ReadOnlySpan<float> spectrum,
            ReadOnlySpan<float> logSpectrum,
            float level,
            float levelLow,
            float levelMid,
            float levelHigh,
            int sampleRate)
        {
            Waveform = waveform;
            Spectrum = spectrum;
            LogSpectrum = logSpectrum;
            Level = level;
            LevelLow = levelLow;
            LevelMid = levelMid;
            LevelHigh = levelHigh;
            SampleRate = sampleRate;
        }

        /// <summary>
        /// 全フィールドが zero の空 frame。device 未初期化時の default 値として使用。
        /// </summary>
        public static AudioFrame Empty => new AudioFrame(
            ReadOnlySpan<float>.Empty,
            ReadOnlySpan<float>.Empty,
            ReadOnlySpan<float>.Empty,
            0f, 0f, 0f, 0f, 0);

        /// <summary>
        /// 指定周波数帯域の Spectrum 平均値を返す。
        /// </summary>
        public float GetBandLevel(float freqMin, float freqMax)
        {
            if (Spectrum.IsEmpty || SampleRate == 0) return 0f;

            float binWidth = (SampleRate / 2f) / Spectrum.Length;
            int binMin = (int)(freqMin / binWidth);
            int binMax = (int)(freqMax / binWidth);
            if (binMin < 0) binMin = 0;
            if (binMax >= Spectrum.Length) binMax = Spectrum.Length - 1;
            if (binMax < binMin) return 0f;

            float sum = 0f;
            for (int i = binMin; i <= binMax; i++)
                sum += Spectrum[i];
            return sum / (binMax - binMin + 1);
        }
    }
}
