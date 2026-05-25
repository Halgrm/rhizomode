#nullable enable

using System;
using Lasp;
using Rhizomode.Audio.Analysis.Capture;
using Rhizomode.Audio.Analysis.Infrastructure;
using Rhizomode.Audio.Analysis.Spectrum;
using Rhizomode.Audio.Contracts;
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

        [Header("レイテンシ補正")]
        [SerializeField, Range(0f, 500f), Tooltip(
            "audio I/F のキャプチャ遅延 (ms)。BeatDetectorNode の時刻参照から差し引かれる。" +
            "実機リハで「映像が音から遅れる」場合に増やしてキャリブする。PlayerPrefs で永続化。")]
        private float audioLatencyOffsetMs;
        private const string LatencyOffsetPrefsKey = "Rhizomode_AudioLatencyOffsetMs";
        private const float MinLatencyOffsetMs = 0f;
        private const float MaxLatencyOffsetMs = 500f;
        private static AudioAnalyzer? _clockOwner;

        // --- デバイス管理 (Capture namespace に委譲) ---
        private readonly AudioDeviceMap _deviceMap = new();
        private readonly AudioAnalyzerInitializationCoordinator _initialization = new();
        private string? _currentDevice;

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
        internal bool IsInitializationTransitionActive => _initialization.IsTransitionActive;
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

        /// <summary>
        /// 指定デバイスでオーディオキャプチャを開始する (reentrant-safe)。
        /// </summary>
        /// <remarks>
        /// race guard 設計:
        /// <list type="bullet">
        ///   <item>空 / null name は no-op</item>
        ///   <item>既に同一 device で稼働中なら no-op (連打耐性)</item>
        ///   <item>shutdown 中 (pending flush 待ち) なら pending device を後勝ちで上書きするだけ
        ///     (同フレ内の Initialize 連打で LASP destroy/create が parallel に走らないようにする)</item>
        ///   <item>別 device で稼働中なら <see cref="ShutdownInternal"/> + pending 設定 → 次 Update で
        ///     新 device 初期化</item>
        ///   <item>未初期化なら即 <see cref="InitializeInternal"/></item>
        /// </list>
        /// LASP が destroy 直後に create で壊れる経験則があるため、native stream の sleep window を待って新装着する。
        /// </remarks>
        public void Initialize(string deviceName)
        {
            var action = _initialization.RequestInitialize(
                deviceName,
                _spectrum != null,
                _currentDevice,
                Time.frameCount);
            if (action == AudioAnalyzerInitializeAction.InitializeNow)
            {
                InitializeInternal(deviceName);
                return;
            }
            if (action == AudioAnalyzerInitializeAction.ShutdownBeforePending)
            {
                ShutdownInternal();
            }
        }

        /// <summary>
        /// 明示的に capture を停止し、pending init もキャンセルする (公開 API)。
        /// </summary>
        public void Shutdown()
        {
            _initialization.Clear();
            ShutdownInternal();
        }

        /// <summary>
        /// LASP リソースのみ破棄する内部版 (pending state には触らない、Initialize 経由の遷移用)。
        /// </summary>
        private void ShutdownInternal()
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

        /// <summary>
        /// 現在の audio I/F レイテンシ補正値 (秒)。<see cref="AudioClock.LatencyOffsetSeconds"/> と同期する。
        /// </summary>
        public float LatencyOffsetSeconds => audioLatencyOffsetMs * 0.001f;

        /// <summary>
        /// レイテンシ補正 (ms) を設定。<see cref="AudioClock"/> と PlayerPrefs に伝搬する。
        /// </summary>
        public void SetLatencyOffsetMs(float ms)
        {
            audioLatencyOffsetMs = ClampLatencyOffsetMs(ms);
            if (_clockOwner == null)
                _clockOwner = this;
            if (IsClockOwner)
                ApplyLatencyOffsetToClock();
            PlayerPrefs.SetFloat(LatencyOffsetPrefsKey, audioLatencyOffsetMs);
        }

        // --- Unity ライフサイクル ---

        private void Awake()
        {
            _gpuBuffer = new AudioGpuBuffer(fftSize, WaveformBufferSize);

            // PlayerPrefs から前回のキャリブ値を復元 (実機リハでの調整値を保存)
            if (PlayerPrefs.HasKey(LatencyOffsetPrefsKey))
                audioLatencyOffsetMs = PlayerPrefs.GetFloat(LatencyOffsetPrefsKey, audioLatencyOffsetMs);
            audioLatencyOffsetMs = ClampLatencyOffsetMs(audioLatencyOffsetMs);
            ClaimClockOwnership();
        }

        private void ApplyLatencyOffsetToClock()
        {
            AudioClock.LatencyOffsetSeconds = audioLatencyOffsetMs * 0.001f;
        }

        private void Update()
        {
            if (HandlePendingInitialization())
                return;

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

        private bool HandlePendingInitialization()
        {
            var (action, deviceName) = _initialization.RequestUpdate(
                _spectrum != null,
                Time.frameCount);
            if (action == AudioAnalyzerUpdateAction.None)
                return false;
            if (action == AudioAnalyzerUpdateAction.WaitForNextFrame)
                return true;
            return HandlePendingInitializationAction(action, deviceName);
        }

        private bool HandlePendingInitializationAction(
            AudioAnalyzerUpdateAction action,
            string? deviceName)
        {
            if (action == AudioAnalyzerUpdateAction.InitializePending && deviceName != null)
            {
                InitializeInternal(deviceName);
                return true;
            }
            if (action == AudioAnalyzerUpdateAction.ShutdownBeforePending)
            {
                Debug.LogWarning("[AudioAnalyzer] pending switch still had live capture; shutting down first.");
                ShutdownInternal();
                _initialization.BeginShutdown(Time.frameCount);
                return true;
            }
            Debug.LogWarning("[AudioAnalyzer] shutdown state had no pending device; clearing.");
            return false;
        }

        private void OnDestroy()
        {
            Shutdown();
            _gpuBuffer?.Dispose();
            _gpuBuffer = null;
            ReleaseClockOwnership();
        }

        private void OnValidate()
        {
            fftSize = FftSizeValidator.Snap(fftSize);
            audioLatencyOffsetMs = ClampLatencyOffsetMs(audioLatencyOffsetMs);
        }

        // --- 内部実装 ---

        private bool IsClockOwner => ReferenceEquals(_clockOwner, this);

        private static float ClampLatencyOffsetMs(float ms)
        {
            if (!float.IsFinite(ms)) return MinLatencyOffsetMs;
            return Mathf.Clamp(ms, MinLatencyOffsetMs, MaxLatencyOffsetMs);
        }

        private void ClaimClockOwnership()
        {
            _clockOwner = this;
            ApplyLatencyOffsetToClock();
        }

        private void ReleaseClockOwnership()
        {
            if (!IsClockOwner) return;
            _clockOwner = null;
            AudioClock.LatencyOffsetSeconds = 0f;
        }

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
