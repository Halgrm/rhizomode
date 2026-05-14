#nullable enable

using System;
using Unity.Collections;
using UnityEngine;

namespace Rhizomode.Audio.Analysis.Spectrum
{
    /// <summary>
    /// スペクトル / 波形を GPU の GraphicsBuffer に流し、グローバルシェーダープロパティに
    /// bind するヘルパー。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10C: AudioAnalyzer に直に書かれていた GraphicsBuffer 生成 / SetData /
    /// Dispose を一カ所に集約。caller は <see cref="WriteSpectrum"/> / <see cref="WriteWaveform"/> /
    /// <see cref="Dispose"/> を呼ぶだけで GPU 配信が成立。
    /// </remarks>
    public sealed class AudioGpuBuffer : IDisposable
    {
        private static readonly int SpectrumPropertyId = Shader.PropertyToID("_RhizomodeAudioSpectrum");
        private static readonly int SpectrumSizePropertyId = Shader.PropertyToID("_RhizomodeAudioSpectrumSize");
        private static readonly int WaveformPropertyId = Shader.PropertyToID("_RhizomodeAudioWaveform");
        private static readonly int WaveformSizePropertyId = Shader.PropertyToID("_RhizomodeAudioWaveformSize");

        private readonly int _fftSize;
        private readonly int _waveformSize;
        private readonly float[] _waveformCpuBuffer;

        private GraphicsBuffer? _spectrumBuffer;
        private GraphicsBuffer? _waveformBuffer;
        private bool _isDisposed;

        public AudioGpuBuffer(int fftSize, int waveformBufferSize)
        {
            _fftSize = fftSize;
            _waveformSize = waveformBufferSize;
            _waveformCpuBuffer = new float[waveformBufferSize];

            _spectrumBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, fftSize, sizeof(float));
            Shader.SetGlobalBuffer(SpectrumPropertyId, _spectrumBuffer);
            Shader.SetGlobalInteger(SpectrumSizePropertyId, fftSize);

            _waveformBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, waveformBufferSize, sizeof(float));
            Shader.SetGlobalBuffer(WaveformPropertyId, _waveformBuffer);
            Shader.SetGlobalInteger(WaveformSizePropertyId, waveformBufferSize);
        }

        /// <summary>logSpectrum (NativeArray) を GraphicsBuffer に書き込む。</summary>
        /// <remarks>GraphicsBuffer.SetData は NativeArray (NativeSlice 不可) 専用。</remarks>
        public void WriteSpectrum(NativeArray<float> logSpectrum)
        {
            if (_isDisposed || _spectrumBuffer == null) return;
            if (logSpectrum.Length == 0) return;
            _spectrumBuffer.SetData(logSpectrum);
        }

        /// <summary>波形 slice を CPU 経由で GraphicsBuffer に書き込む。</summary>
        public void WriteWaveform(NativeSlice<float> waveform)
        {
            if (_isDisposed || _waveformBuffer == null) return;
            if (waveform.Length == 0) return;

            var len = Mathf.Min(waveform.Length, _waveformSize);
            for (var i = 0; i < len; i++)
                _waveformCpuBuffer[i] = waveform[i];
            for (var i = len; i < _waveformSize; i++)
                _waveformCpuBuffer[i] = 0f;

            _waveformBuffer.SetData(_waveformCpuBuffer);
            Shader.SetGlobalInteger(WaveformSizePropertyId, len);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _spectrumBuffer?.Dispose();
            _spectrumBuffer = null;
            _waveformBuffer?.Dispose();
            _waveformBuffer = null;
        }
    }
}
