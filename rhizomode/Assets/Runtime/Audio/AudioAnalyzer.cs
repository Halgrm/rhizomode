#nullable enable

using System;
using System.Collections.Generic;
using Lasp;
using UnityEngine;

namespace Rhizomode.Audio
{
    /// <summary>
    /// LASP (libsoundio) 経由でオーディオ入力をキャプチャし FFT 解析を行う。
    /// スペクトル・波形データを GraphicsBuffer 経由でグローバルシェーダープロパティに配信する。
    /// </summary>
    public sealed class AudioAnalyzer : MonoBehaviour
    {
        [Header("FFT設定")]
        [SerializeField, Tooltip("FFTサンプル数（2の累乗: 256, 512, 1024, 2048）")]
        private int fftSize = 1024;

        private static readonly int[] ValidFftSizes = { 64, 128, 256, 512, 1024, 2048, 4096, 8192 };

        // --- デバイス管理 ---
        private readonly Dictionary<string, DeviceDescriptor> _deviceMap = new();
        private string? _currentDevice;
        private string? _pendingDevice;
        private bool _isShuttingDown;

        // --- LASP コンポーネント ---
        private GameObject? _laspGo;
        private SpectrumAnalyzer? _spectrum;
        private AudioLevelTracker? _levelTracker;
        private AudioLevelTracker? _lowTracker;
        private AudioLevelTracker? _midTracker;
        private AudioLevelTracker? _highTracker;
        private int _sampleRate;

        // --- GPU バッファ ---
        private GraphicsBuffer? _spectrumBuffer;
        private GraphicsBuffer? _waveformBuffer;
        private float[]? _waveformCpuBuffer;

        private static readonly int SpectrumPropertyId = Shader.PropertyToID("_RhizomodeAudioSpectrum");
        private static readonly int SpectrumSizePropertyId = Shader.PropertyToID("_RhizomodeAudioSpectrumSize");
        private static readonly int WaveformPropertyId = Shader.PropertyToID("_RhizomodeAudioWaveform");
        private static readonly int WaveformSizePropertyId = Shader.PropertyToID("_RhizomodeAudioWaveformSize");

        private const int WaveformBufferSize = 512;
        private const string PrefsKey = "Rhizomode_AudioDevice";

        // --- 公開 API ---

        /// <summary>現在初期化済みかどうか。</summary>
        public bool IsInitialized => _spectrum != null;

        /// <summary>現在使用中のオーディオデバイス名。未初期化なら null。</summary>
        public string? CurrentDevice => _currentDevice;

        /// <summary>全帯域の正規化レベル (0-1)。</summary>
        public float Level => _levelTracker != null ? _levelTracker.normalizedLevel : 0f;

        /// <summary>低域の正規化レベル (0-1)。</summary>
        public float LevelLow => _lowTracker != null ? _lowTracker.normalizedLevel : 0f;

        /// <summary>中域の正規化レベル (0-1)。</summary>
        public float LevelMid => _midTracker != null ? _midTracker.normalizedLevel : 0f;

        /// <summary>高域の正規化レベル (0-1)。</summary>
        public float LevelHigh => _highTracker != null ? _highTracker.normalizedLevel : 0f;

        /// <summary>利用可能なオーディオ入力デバイス一覧（libsoundio 経由）。</summary>
        public string[] AvailableDevices
        {
            get
            {
                RefreshDeviceMap();
                var names = new string[_deviceMap.Count];
                var i = 0;
                foreach (var name in _deviceMap.Keys)
                    names[i++] = name;
                return names;
            }
        }

        /// <summary>
        /// 指定デバイスでオーディオキャプチャを開始する。
        /// 既にキャプチャ中の場合、古いストリームの破棄を待って1フレーム後に初期化する。
        /// </summary>
        public void Initialize(string deviceName)
        {
            if (_spectrum != null)
            {
                Shutdown();
                _pendingDevice = deviceName;
                _isShuttingDown = true;
                return;
            }

            InitializeInternal(deviceName);
        }

        /// <summary>
        /// オーディオキャプチャを停止しリソースを解放する。
        /// </summary>
        public void Shutdown()
        {
            if (_laspGo != null)
            {
                Destroy(_laspGo);
                _laspGo = null;
            }

            _spectrum = null;
            _levelTracker = null;
            _lowTracker = null;
            _midTracker = null;
            _highTracker = null;
            _currentDevice = null;
            _sampleRate = 0;
        }

        /// <summary>
        /// 指定周波数帯域のスペクトルレベル平均を返す。
        /// </summary>
        public float GetBandLevel(float freqMin, float freqMax)
        {
            if (_spectrum == null || _sampleRate == 0)
                return 0f;

            var spectrum = _spectrum.spectrumArray;
            if (!spectrum.IsCreated || spectrum.Length == 0)
                return 0f;

            float binWidth = (_sampleRate / 2f) / spectrum.Length;
            var binMin = Mathf.Clamp(Mathf.RoundToInt(freqMin / binWidth), 0, spectrum.Length - 1);
            var binMax = Mathf.Clamp(Mathf.RoundToInt(freqMax / binWidth), binMin, spectrum.Length - 1);

            var sum = 0f;
            for (var i = binMin; i <= binMax; i++)
                sum += spectrum[i];

            return sum / (binMax - binMin + 1);
        }

        /// <summary>
        /// スペクトルデータ（対数スケール）をダウンサンプルして dest にコピーする。
        /// </summary>
        public void CopySpectrum(float[] dest)
        {
            if (_spectrum == null || dest.Length == 0) return;

            var spectrumData = _spectrum.logSpectrumArray;
            if (!spectrumData.IsCreated || spectrumData.Length == 0) return;

            var step = (float)spectrumData.Length / dest.Length;
            for (var i = 0; i < dest.Length; i++)
            {
                var idx = Mathf.Min((int)(i * step), spectrumData.Length - 1);
                dest[i] = spectrumData[idx];
            }
        }

        /// <summary>
        /// 波形データをダウンサンプルして dest にコピーする。
        /// </summary>
        public void CopyWaveform(float[] dest)
        {
            if (_levelTracker == null || dest.Length == 0) return;

            var slice = _levelTracker.audioDataSlice;
            if (slice.Length == 0) return;

            var step = (float)slice.Length / dest.Length;
            for (var i = 0; i < dest.Length; i++)
            {
                var idx = Mathf.Min((int)(i * step), slice.Length - 1);
                dest[i] = slice[idx];
            }
        }

        /// <summary>前回使用したデバイスを PlayerPrefs から復元する。</summary>
        public void ReloadLastDevice()
        {
            var lastDevice = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(lastDevice)) return;

            try
            {
                RefreshDeviceMap();
                if (_deviceMap.ContainsKey(lastDevice))
                {
                    Initialize(lastDevice);
                    Debug.Log($"[AudioAnalyzer] Restored device: '{lastDevice}'");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AudioAnalyzer] Failed to restore device: {e.Message}");
                PlayerPrefs.DeleteKey(PrefsKey);
            }
        }

        // --- Unity ライフサイクル ---

        private void Awake()
        {
            CreateGpuBuffers();
        }

        private void Update()
        {
            if (_isShuttingDown && _pendingDevice != null)
            {
                var device = _pendingDevice;
                _pendingDevice = null;
                _isShuttingDown = false;
                InitializeInternal(device);
                return;
            }

            UpdateGpuBuffers();
        }

        private void OnDestroy()
        {
            Shutdown();
            DisposeGpuBuffers();
        }

        private void OnValidate()
        {
            ValidateFftSize();
        }

        // --- 内部実装 ---

        private void InitializeInternal(string deviceName)
        {
            try
            {
                ValidateFftSize();
                RefreshDeviceMap();

                if (!_deviceMap.TryGetValue(deviceName, out var descriptor))
                {
                    Debug.LogWarning($"[AudioAnalyzer] Device not found: '{deviceName}'");
                    return;
                }

                if (!descriptor.IsValid)
                {
                    Debug.LogWarning($"[AudioAnalyzer] Invalid device: '{deviceName}'");
                    return;
                }

                _sampleRate = descriptor.SampleRate;
                var id = descriptor.ID;

                _laspGo = new GameObject("AudioAnalyzer_LASP");
                _laspGo.transform.SetParent(transform, false);

                // SpectrumAnalyzer
                _spectrum = _laspGo.AddComponent<SpectrumAnalyzer>();
                _spectrum.deviceID = id;
                _spectrum.channel = 0;
                _spectrum.resolution = fftSize;
                _spectrum.autoGain = false;

                // 全帯域レベル + 波形
                _levelTracker = CreateTracker(id, FilterType.Bypass);

                // 帯域別レベル
                _lowTracker = CreateTracker(id, FilterType.LowPass);
                _midTracker = CreateTracker(id, FilterType.BandPass);
                _highTracker = CreateTracker(id, FilterType.HighPass);

                _currentDevice = deviceName;
                PlayerPrefs.SetString(PrefsKey, deviceName);

                Debug.Log($"[AudioAnalyzer] Initialized with '{deviceName}' ({_sampleRate}Hz)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AudioAnalyzer] Initialize failed: {e.Message}");
                Shutdown();
            }
        }

        private AudioLevelTracker CreateTracker(string deviceId, FilterType filter)
        {
            var tracker = _laspGo!.AddComponent<AudioLevelTracker>();
            tracker.deviceID = deviceId;
            tracker.channel = 0;
            tracker.filterType = filter;
            tracker.autoGain = true;
            return tracker;
        }

        // --- GPU バッファ ---

        private void CreateGpuBuffers()
        {
            _spectrumBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured, fftSize, sizeof(float));
            Shader.SetGlobalBuffer(SpectrumPropertyId, _spectrumBuffer);
            Shader.SetGlobalInteger(SpectrumSizePropertyId, fftSize);

            _waveformBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured, WaveformBufferSize, sizeof(float));
            Shader.SetGlobalBuffer(WaveformPropertyId, _waveformBuffer);
            Shader.SetGlobalInteger(WaveformSizePropertyId, WaveformBufferSize);

            _waveformCpuBuffer = new float[WaveformBufferSize];
        }

        private void UpdateGpuBuffers()
        {
            if (_spectrum == null || _waveformCpuBuffer == null) return;

            // スペクトル → GPU
            var spectrumData = _spectrum.logSpectrumArray;
            if (spectrumData.IsCreated && spectrumData.Length > 0)
                _spectrumBuffer?.SetData(spectrumData);

            // 波形 → GPU
            if (_levelTracker != null)
            {
                var slice = _levelTracker.audioDataSlice;
                if (slice.Length > 0)
                {
                    var len = Mathf.Min(slice.Length, WaveformBufferSize);
                    for (var i = 0; i < len; i++)
                        _waveformCpuBuffer[i] = slice[i];

                    // 残りをゼロ埋め
                    for (var i = len; i < WaveformBufferSize; i++)
                        _waveformCpuBuffer[i] = 0f;

                    _waveformBuffer?.SetData(_waveformCpuBuffer);
                    Shader.SetGlobalInteger(WaveformSizePropertyId, len);
                }
            }
        }

        private void DisposeGpuBuffers()
        {
            _spectrumBuffer?.Dispose();
            _spectrumBuffer = null;
            _waveformBuffer?.Dispose();
            _waveformBuffer = null;
        }

        // --- デバイス列挙 ---

        private void RefreshDeviceMap()
        {
            _deviceMap.Clear();
            try
            {
                foreach (var device in AudioSystem.InputDevices)
                {
                    if (!device.IsValid) continue;
                    var name = device.Name;
                    if (_deviceMap.ContainsKey(name))
                        name = $"{name} ({device.ID})";
                    _deviceMap[name] = device;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AudioAnalyzer] Device enumeration failed: {e.Message}");
            }
        }

        private void ValidateFftSize()
        {
            var closest = ValidFftSizes[0];
            var minDiff = int.MaxValue;
            foreach (var valid in ValidFftSizes)
            {
                var diff = Mathf.Abs(fftSize - valid);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = valid;
                }
            }

            if (fftSize != closest)
            {
                Debug.LogWarning($"[AudioAnalyzer] fftSize {fftSize} → {closest} に補正");
                fftSize = closest;
            }
        }
    }
}
