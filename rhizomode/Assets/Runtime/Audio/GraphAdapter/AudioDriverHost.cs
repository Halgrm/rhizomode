#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Audio.Analysis;
using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Audio;
using UnityEngine;

namespace Rhizomode.Audio.GraphAdapter
{
    /// <summary>
    /// AudioAnalyzer と AudioTriggerNode / AudioDeviceNode / AudioMonitorNode /
    /// AudioBandNode / SpectrumMonitorNode を毎 tick で橋渡しする pure C# host。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10D: 旧 AudioDriverBehaviour (MonoBehaviour) から純粋ロジックを抽出。
    /// 将来 Phase 5/12 の VContainer ITickable 移行時、Bootstrap 側で
    /// <c>AudioDriverHostTickAdapter</c> として直接 wrap できる (Plan v5.3 line 327 の tick 順序 #2)。
    ///
    /// N3 fix (2026-05-16): UI / UI.Presentation 直依存を撤去。<see cref="GraphContextBehaviour"/> 経由
    /// だった graph アクセスを <see cref="GraphState"/> 直接注入に切り替え、asmdef refs から
    /// <c>Rhizomode.UI</c> / <c>Rhizomode.UI.Presentation</c> を削除した。
    ///
    /// P2-B (2026-05-25): AudioMonitor / SpectrumMonitor の inline UI 描画は毎フレ走らせると
    /// 60+ ノード時に main thread を圧迫する。<see cref="MonitorUpdateIntervalSec"/> ごとに
    /// 間引いて push する (既定 ~30Hz)。AudioTrigger / AudioBand は毎フレ駆動を維持
    /// (BeatDetector の rising edge 検出が漏れないように)。
    /// </remarks>
    public sealed class AudioDriverHost
    {
        /// <summary>monitor (Audio/Spectrum) inline UI 描画の間引き間隔 (秒)。既定 ~30Hz。</summary>
        public const float DefaultMonitorUpdateIntervalSec = 1f / 30f;

        private readonly AudioAnalyzer _analyzer;
        private readonly GraphState _graphState;
        private Func<float> _clock;
        private readonly List<AudioTriggerNode> _audioNodeBuffer = new();
        private readonly List<AudioDeviceNode> _deviceNodeBuffer = new();
        private readonly List<AudioMonitorNode> _monitorNodeBuffer = new();
        private readonly List<AudioBandNode> _bandNodeBuffer = new();
        private readonly List<SpectrumMonitorNode> _spectrumMonitorBuffer = new();
        private readonly float[] _waveformBuffer = new float[64];
        private readonly float[] _spectrumBuffer = new float[64];
        private float _monitorUpdateIntervalSec = DefaultMonitorUpdateIntervalSec;
        private float _nextMonitorPushTime;

        /// <summary>
        /// AudioMonitor / SpectrumMonitor の更新間隔 (秒)。0 にすると毎フレ駆動 (P2-B 前の挙動)。
        /// </summary>
        public float MonitorUpdateIntervalSec
        {
            get => _monitorUpdateIntervalSec;
            set => _monitorUpdateIntervalSec = value >= 0f && !float.IsInfinity(value) && !float.IsNaN(value)
                ? value
                : DefaultMonitorUpdateIntervalSec;
        }

        public AudioDriverHost(AudioAnalyzer analyzer, GraphState graphState)
        {
            _analyzer = analyzer;
            _graphState = graphState;
            _clock = () => Time.unscaledTime;
        }

        // テスト用 factory。clock を差し替えられる ctor を public/internal で並列に置くと
        // VContainer の reflection injector が最多引数 ctor を greedy に選んで Func<float> 解決に失敗する
        // (production scene の起動が壊れる)。ctor を 1 本に絞り、テスト時のみ static factory 経由で
        // clock を差し替える。
        internal static AudioDriverHost CreateForTests(AudioAnalyzer analyzer, GraphState graphState, Func<float> clock)
        {
            var host = new AudioDriverHost(analyzer, graphState);
            host._clock = clock;
            return host;
        }

        /// <summary>
        /// Bootstrap の ITickable adapter から呼ばれる。MonoBehaviour 経由でも Update から
        /// 呼べる。
        /// </summary>
        /// <remarks>
        /// Codex #4 fix (2026-05-16): scene shutdown 中の race で <see cref="GraphState"/> が
        /// 先に disposed になった状態で tick が走り得る (GraphContextBehaviour.OnDestroy と
        /// LifetimeScope.OnDestroy の順序は Unity が保証しない)。IsDisposed なら早期 return。
        /// </remarks>
        public void Tick()
        {
            if (_graphState.IsDisposed) return;

            // スナップショットでイテレーション (Tick 中にノード追加/削除される可能性への対策)
            SnapshotAudioNodes();

            // AudioDeviceNode: デバイスリスト注入 + 切り替え要求処理
            DriveDeviceNodes();

            // Analyzer が未初期化かつ音声ノードが存在する場合、最初のデバイスで自動初期化
            if (!_analyzer.IsInitialized)
            {
                // Device-node switches leave IsInitialized false during the LASP cooldown.
                if (_analyzer.IsInitializationTransitionActive)
                    return;
                AutoInitializeIfRequested();
                return;
            }

            // AudioTrigger / AudioBand は毎フレ駆動 (rising edge 検出 / reactive flow を維持)
            DriveAudioTriggers();
            DriveBandNodes();

            // Monitor 系 (inline UI repaint コスト大) は間引き
            if (ShouldDriveMonitorsThisTick())
            {
                DriveAudioMonitors();
                DriveSpectrumMonitors();
            }
        }

        private bool ShouldDriveMonitorsThisTick() => ShouldDriveMonitors(_clock());

        /// <summary>テスト用: 任意の <paramref name="now"/> で throttle 判定を回す internal entry。</summary>
        internal bool ShouldDriveMonitors(float now)
        {
            // interval=0 なら毎フレ駆動 (旧挙動)
            if (_monitorUpdateIntervalSec <= 0f) return true;

            // 起動直後 / 巨大ジャンプ guard: 異常な値は throttle window をリセットして 1 回だけ通す
            if (!float.IsFinite(now)) return true;
            if (now < _nextMonitorPushTime) return false;

            _nextMonitorPushTime = now + _monitorUpdateIntervalSec;
            return true;
        }

        private void SnapshotAudioNodes()
        {
            _audioNodeBuffer.Clear();
            _deviceNodeBuffer.Clear();
            _monitorNodeBuffer.Clear();
            _bandNodeBuffer.Clear();
            _spectrumMonitorBuffer.Clear();

            foreach (var node in _graphState.Nodes.Values)
            {
                switch (node)
                {
                    case AudioTriggerNode audioNode: _audioNodeBuffer.Add(audioNode); break;
                    case AudioDeviceNode deviceNode: _deviceNodeBuffer.Add(deviceNode); break;
                    case AudioMonitorNode monitorNode: _monitorNodeBuffer.Add(monitorNode); break;
                    case AudioBandNode bandNode: _bandNodeBuffer.Add(bandNode); break;
                    case SpectrumMonitorNode spectrumMonitor: _spectrumMonitorBuffer.Add(spectrumMonitor); break;
                }
            }
        }

        private void DriveDeviceNodes()
        {
            var devices = _analyzer.AvailableDevices;
            foreach (var deviceNode in _deviceNodeBuffer)
            {
                deviceNode.SetDeviceList(devices);

                var pending = deviceNode.ConsumePendingDevice();
                if (pending != null)
                {
                    _analyzer.Initialize(pending);
                    Debug.Log($"[AudioDriverHost] Device switched to '{pending}' via AudioDeviceNode");
                }
            }
        }

        private void AutoInitializeIfRequested()
        {
            bool hasAudioConsumer =
                _monitorNodeBuffer.Count > 0 ||
                _audioNodeBuffer.Count > 0 ||
                _bandNodeBuffer.Count > 0 ||
                _spectrumMonitorBuffer.Count > 0;

            if (!hasAudioConsumer) return;
            if (_analyzer.AvailableDevices.Length == 0) return;
            if (!string.IsNullOrEmpty(_analyzer.CurrentDevice)) return;

            var defaultDevice = _analyzer.AvailableDevices[0];
            _analyzer.Initialize(defaultDevice);
            Debug.Log($"[AudioDriverHost] Auto-initializing analyzer with '{defaultDevice}'");
        }

        private void DriveAudioTriggers()
        {
            foreach (var audioNode in _audioNodeBuffer)
            {
                var level = _analyzer.GetBandLevel(audioNode.FreqMin, audioNode.FreqMax);
                audioNode.SetLevel(level);
            }
        }

        private void DriveAudioMonitors()
        {
            if (_monitorNodeBuffer.Count == 0) return;

            _analyzer.CopyWaveform(_waveformBuffer);
            var level = _analyzer.GetBandLevel(20f, 20000f);
            foreach (var monitorNode in _monitorNodeBuffer)
            {
                monitorNode.SetWaveform(_waveformBuffer);
                monitorNode.SetLevel(level);
            }
        }

        private void DriveSpectrumMonitors()
        {
            if (_spectrumMonitorBuffer.Count == 0) return;

            _analyzer.CopySpectrum(_spectrumBuffer);
            var level = _analyzer.Level;
            foreach (var spectrumMonitor in _spectrumMonitorBuffer)
            {
                spectrumMonitor.SetSpectrum(_spectrumBuffer);
                spectrumMonitor.SetLevel(level);
            }
        }

        private void DriveBandNodes()
        {
            foreach (var bandNode in _bandNodeBuffer)
            {
                bandNode.SetBandLevels(
                    _analyzer.Level,
                    _analyzer.LevelLow,
                    _analyzer.LevelMid,
                    _analyzer.LevelHigh);
            }
        }
    }
}
