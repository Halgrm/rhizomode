#nullable enable

using System.Collections.Generic;
using Rhizomode.Audio.Analysis;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Audio;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.Audio.GraphAdapter
{
    /// <summary>
    /// AudioAnalyzerとAudioTriggerNode/AudioDeviceNodeを毎フレーム橋渡しする。
    /// GraphContext内の全AudioTriggerNodeにスペクトルレベルを注入し、
    /// AudioDeviceNodeのデバイス切り替え要求をAudioAnalyzerに適用する。
    /// </summary>
    public class AudioDriverBehaviour : MonoBehaviour
    {
        [SerializeField] private AudioAnalyzer? audioAnalyzer;

        private GraphContextBehaviour? _graphContext;
        private readonly List<AudioTriggerNode> _audioNodeBuffer = new();
        private readonly List<AudioDeviceNode> _deviceNodeBuffer = new();
        private readonly List<AudioMonitorNode> _monitorNodeBuffer = new();
        private readonly List<AudioBandNode> _bandNodeBuffer = new();
        private readonly List<SpectrumMonitorNode> _spectrumMonitorBuffer = new();
        private readonly float[] _waveformBuffer = new float[64];
        private readonly float[] _spectrumBuffer = new float[64];

        /// <summary>
        /// 依存関係を設定する。
        /// </summary>
        public void Initialize(GraphContextBehaviour graphContext)
        {
            _graphContext = graphContext;
        }

        /// <summary>
        /// AudioAnalyzerを外部から設定する（ランタイム生成時用）。
        /// </summary>
        public AudioAnalyzer? Analyzer
        {
            get => audioAnalyzer;
            set => audioAnalyzer = value;
        }

        private void Update()
        {
            if (audioAnalyzer == null) return;
            if (_graphContext == null) return;

            // スナップショットでイテレーション（Update中にノード追加/削除される可能性への対策）
            _audioNodeBuffer.Clear();
            _deviceNodeBuffer.Clear();
            _monitorNodeBuffer.Clear();
            _bandNodeBuffer.Clear();
            _spectrumMonitorBuffer.Clear();
            foreach (var node in _graphContext.Context.Nodes.Values)
            {
                if (node is AudioTriggerNode audioNode)
                    _audioNodeBuffer.Add(audioNode);
                else if (node is AudioDeviceNode deviceNode)
                    _deviceNodeBuffer.Add(deviceNode);
                else if (node is AudioMonitorNode monitorNode)
                    _monitorNodeBuffer.Add(monitorNode);
                else if (node is AudioBandNode bandNode)
                    _bandNodeBuffer.Add(bandNode);
                else if (node is SpectrumMonitorNode spectrumMonitor)
                    _spectrumMonitorBuffer.Add(spectrumMonitor);
            }

            // AudioDeviceNode: デバイスリスト注入 + 切り替え要求処理
            DriveDeviceNodes();

            // Analyzerが未初期化かつ音声ノードが存在する場合、最初のデバイスで自動初期化
            if (!audioAnalyzer.IsInitialized)
            {
                if ((_monitorNodeBuffer.Count > 0 || _audioNodeBuffer.Count > 0
                    || _bandNodeBuffer.Count > 0 || _spectrumMonitorBuffer.Count > 0)
                    && audioAnalyzer.AvailableDevices.Length > 0
                    && string.IsNullOrEmpty(audioAnalyzer.CurrentDevice))
                {
                    var defaultDevice = audioAnalyzer.AvailableDevices[0];
                    audioAnalyzer.Initialize(defaultDevice);
                    Debug.Log($"[AudioDriver] Auto-initializing analyzer with '{defaultDevice}'");
                }
                return;
            }

            foreach (var audioNode in _audioNodeBuffer)
            {
                var level = audioAnalyzer.GetBandLevel(
                    audioNode.FreqMin, audioNode.FreqMax);
                audioNode.SetLevel(level);
            }

            // AudioMonitorNode: 波形データ + 全帯域の平均レベルを注入
            if (_monitorNodeBuffer.Count > 0)
                audioAnalyzer.CopyWaveform(_waveformBuffer);

            foreach (var monitorNode in _monitorNodeBuffer)
            {
                monitorNode.SetWaveform(_waveformBuffer);
                var level = audioAnalyzer.GetBandLevel(20f, 20000f);
                monitorNode.SetLevel(level);
            }

            // SpectrumMonitorNode: スペクトルデータ + レベルを注入
            if (_spectrumMonitorBuffer.Count > 0)
                audioAnalyzer.CopySpectrum(_spectrumBuffer);

            foreach (var spectrumMonitor in _spectrumMonitorBuffer)
            {
                spectrumMonitor.SetSpectrum(_spectrumBuffer);
                spectrumMonitor.SetLevel(audioAnalyzer.Level);
            }

            // AudioBandNode: 帯域レベル注入
            foreach (var bandNode in _bandNodeBuffer)
            {
                bandNode.SetBandLevels(
                    audioAnalyzer.Level,
                    audioAnalyzer.LevelLow,
                    audioAnalyzer.LevelMid,
                    audioAnalyzer.LevelHigh);
            }
        }

        private void DriveDeviceNodes()
        {
            var devices = audioAnalyzer!.AvailableDevices;

            foreach (var deviceNode in _deviceNodeBuffer)
            {
                deviceNode.SetDeviceList(devices);

                var pending = deviceNode.ConsumePendingDevice();
                if (pending != null)
                {
                    audioAnalyzer.Initialize(pending);
                    Debug.Log($"[AudioDriver] Device switched to '{pending}' via AudioDeviceNode");
                }
            }
        }
    }
}
