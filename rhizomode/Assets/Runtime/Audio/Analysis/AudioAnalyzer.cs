#nullable enable

using System;
using Lasp;
using Rhizomode.Audio.Analysis.Capture;
using Rhizomode.Audio.Analysis.Infrastructure;
using Rhizomode.Audio.Analysis.Spectrum;
using UnityEngine;

namespace Rhizomode.Audio.Analysis
{
    /// <summary>
    /// LASP (libsoundio) 経由でオーディオ入力をキャプチャし FFT 解析を行う。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10C: 旧 395 行を Audio.Analysis.{Capture, Spectrum, Infrastructure}
    /// namespace に分割し、本 class はオーケストレータに専念:
    /// - Capture.AudioDeviceMap: device 列挙 + name → DeviceDescriptor lookup
    /// - Spectrum.SpectrumOps: downsample / band-level (pure static)
    /// - Spectrum.AudioGpuBuffer: GraphicsBuffer + shader globals (IDisposable)
    /// - Infrastructure.FftSizeValidator: FFT サイズ補正 (static)
    /// </remarks>
    public sealed class AudioAnalyzer : MonoBehaviour
    {
        [Header("FFT設定")]
        [SerializeField, Tooltip("FFTサンプル数（2の累乗: 256, 512, 1024, 2048）")]
        private int fftSize = 1024;

        // --- デバイス管理 (Capture namespace に委譲) ---
        private readonly AudioDeviceMap _deviceMap = new();
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

        // --- GPU バッファ (Spectrum namespace に委譲) ---
        private AudioGpuBuffer? _gpuBuffer;
        private const int WaveformBufferSize = 512;
        private const string PrefsKey = "Rhizomode_AudioDevice";

        // --- 公開 API ---
        public bool IsInitialized => _spectrum != null;
        public string? CurrentDevice => _currentDevice;
        public float Level => _levelTracker != null ? _levelTracker.normalizedLevel : 0f;
        public float LevelLow => _lowTracker != null ? _lowTracker.normalizedLevel : 0f;
        public float LevelMid => _midTracker != null ? _midTracker.normalizedLevel : 0f;
        public float LevelHigh => _highTracker != null ? _highTracker.normalizedLevel : 0f;

        public string[] AvailableDevices
        {
            get
            {
                _deviceMap.Refresh();
                return _deviceMap.Names();
            }
        }

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

        public float GetBandLevel(float freqMin, float freqMax)
        {
            if (_spectrum == null || _sampleRate == 0) return 0f;
            var spectrum = _spectrum.spectrumArray;
            if (!spectrum.IsCreated) return 0f;
            return SpectrumOps.BandLevel(spectrum, _sampleRate, freqMin, freqMax);
        }

        public void CopySpectrum(float[] dest)
        {
            if (_spectrum == null) return;
            var spectrumData = _spectrum.logSpectrumArray;
            if (!spectrumData.IsCreated) return;
            SpectrumOps.Downsample(spectrumData, dest);
        }

        public void CopyWaveform(float[] dest)
        {
            if (_levelTracker == null) return;
            var slice = _levelTracker.audioDataSlice;
            SpectrumOps.Downsample(slice, dest);
        }

        public void ReloadLastDevice()
        {
            var lastDevice = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(lastDevice)) return;

            try
            {
                _deviceMap.Refresh();
                if (_deviceMap.Devices.ContainsKey(lastDevice))
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
            _gpuBuffer = new AudioGpuBuffer(fftSize, WaveformBufferSize);
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

            // fail-open: LASP / GraphicsBuffer の transient 例外 (device dropout, buffer not ready 等) は
            // 1 frame skip して継続する。映像は決して止めない (memory: feedback_health_monitor)。
            // log は rate-limit して console spam を防止。
            try
            {
                UpdateGpuBuffers();
            }
            catch (Exception e)
            {
                if (Time.unscaledTime >= _nextUpdateWarningTime)
                {
                    Debug.LogWarning($"[AudioAnalyzer] Update failed (frame skipped): {e.Message}");
                    _nextUpdateWarningTime = Time.unscaledTime + UpdateWarningIntervalSec;
                }
            }
        }

        private const float UpdateWarningIntervalSec = 1.0f;
        private float _nextUpdateWarningTime;

        private void OnDestroy()
        {
            Shutdown();
            _gpuBuffer?.Dispose();
            _gpuBuffer = null;
        }

        private void OnValidate()
        {
            fftSize = FftSizeValidator.Snap(fftSize);
        }

        // --- 内部実装 ---

        private void InitializeInternal(string deviceName)
        {
            try
            {
                fftSize = FftSizeValidator.Snap(fftSize);
                _deviceMap.Refresh();

                if (!_deviceMap.TryGet(deviceName, out var descriptor))
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

                _spectrum = _laspGo.AddComponent<SpectrumAnalyzer>();
                _spectrum.deviceID = id;
                _spectrum.channel = 0;
                _spectrum.resolution = fftSize;
                _spectrum.autoGain = false;

                _levelTracker = CreateTracker(id, FilterType.Bypass);
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

        private void UpdateGpuBuffers()
        {
            if (_spectrum == null || _gpuBuffer == null) return;

            var spectrumData = _spectrum.logSpectrumArray;
            if (spectrumData.IsCreated)
                _gpuBuffer.WriteSpectrum(spectrumData);

            if (_levelTracker != null)
            {
                var slice = _levelTracker.audioDataSlice;
                _gpuBuffer.WriteWaveform(slice);
            }
        }
    }
}
