#nullable enable

using System.Collections.Generic;
using Rhizomode.Audio.Analysis;
using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Audio;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.Audio.GraphAdapter
{
    /// <summary>
    /// AudioAnalyzer と AudioTriggerNode / AudioDeviceNode / AudioMonitorNode /
    /// AudioBandNode / SpectrumMonitorNode を毎 tick で橋渡しする pure C# host。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10D: 旧 AudioDriverBehaviour (MonoBehaviour) から純粋ロジックを抽出。
    /// MonoBehaviour 側 (<see cref="AudioDriverBehaviour"/>) は Update() で <see cref="Tick"/> を
    /// 呼ぶ thin wrapper となる。将来 Phase 5/12 の VContainer ITickable 移行時、Bootstrap
    /// 側で <c>AudioDriverHostTickAdapter</c> として直接 wrap できるようにする (Plan v5.3
    /// line 327 の tick 順序 #2)。
    ///
    /// 一時 Plan v5.3 違反 (Phase 5/12 で解消):
    /// - Audio.GraphAdapter が UI / UI.Presentation を参照 (旧 AudioDriverBehaviour 時点から、
    ///   メモリ project_refactor_v4.md 記録済)
    /// </remarks>
    public sealed class AudioDriverHost
    {
        private readonly AudioAnalyzer _analyzer;
        private readonly GraphContextBehaviour _graphContext;
        private readonly List<AudioTriggerNode> _audioNodeBuffer = new();
        private readonly List<AudioDeviceNode> _deviceNodeBuffer = new();
        private readonly List<AudioMonitorNode> _monitorNodeBuffer = new();
        private readonly List<AudioBandNode> _bandNodeBuffer = new();
        private readonly List<SpectrumMonitorNode> _spectrumMonitorBuffer = new();
        private readonly float[] _waveformBuffer = new float[64];
        private readonly float[] _spectrumBuffer = new float[64];

        public AudioDriverHost(AudioAnalyzer analyzer, GraphContextBehaviour graphContext)
        {
            _analyzer = analyzer;
            _graphContext = graphContext;
        }

        /// <summary>
        /// Bootstrap の ITickable adapter から呼ばれる。MonoBehaviour 経由でも Update から
        /// 呼べる。
        /// </summary>
        public void Tick()
        {
            // スナップショットでイテレーション (Tick 中にノード追加/削除される可能性への対策)
            SnapshotAudioNodes();

            // AudioDeviceNode: デバイスリスト注入 + 切り替え要求処理
            DriveDeviceNodes();

            // Analyzer が未初期化かつ音声ノードが存在する場合、最初のデバイスで自動初期化
            if (!_analyzer.IsInitialized)
            {
                AutoInitializeIfRequested();
                return;
            }

            DriveAudioTriggers();
            DriveAudioMonitors();
            DriveSpectrumMonitors();
            DriveBandNodes();
        }

        private void SnapshotAudioNodes()
        {
            _audioNodeBuffer.Clear();
            _deviceNodeBuffer.Clear();
            _monitorNodeBuffer.Clear();
            _bandNodeBuffer.Clear();
            _spectrumMonitorBuffer.Clear();

            foreach (var node in _graphContext.Context.Nodes.Values)
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
