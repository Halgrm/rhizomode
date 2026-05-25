#nullable enable

using System;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.UI.Contracts;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Audio
{
    /// <summary>
    /// オーディオ入力の波形をリアルタイム表示するモニターノード。
    /// AudioDriverBehaviourが毎フレームスペクトルデータを注入する。
    /// </summary>
    [NodeType("AudioMonitor", "Audio Monitor", NodeCategory.Utility)]
    public class AudioMonitorNode : NodeBase, IInlineWaveform
    {
        private const int BufferSize = 64;

        private readonly OutputPort<float> _levelOut;
        private readonly float[] _waveform = new float[BufferSize];
        private int _writeIndex;
        private float _currentLevel;
        private int _waveformVersion;

        public AudioMonitorNode(string id) : base(id, "AudioMonitor")
        {
            _levelOut = RegisterOutput<float>("Level", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            _levelOut.Emit(0f);
        }

        /// <summary>
        /// LASP から取得した波形データでバッファを上書きする。
        /// AudioDriverBehaviour が CopyWaveform で取得したデータを渡す。
        /// </summary>
        public void SetWaveform(float[] data)
        {
            if (data == null) return;
            var len = Mathf.Min(data.Length, BufferSize);
            Array.Copy(data, 0, _waveform, 0, len);
            _writeIndex = len % BufferSize;
            unchecked { _waveformVersion++; } // P2-B: NodeVisualController が MarkDirtyRepaint 判定に使う
        }

        /// <summary>
        /// AudioDriverBehaviourから毎フレーム呼ばれる。
        /// 全帯域の平均レベルを受け取り出力する。
        /// </summary>
        public void SetLevel(float level)
        {
            if (float.IsNaN(level) || float.IsInfinity(level))
                level = 0f;

            _currentLevel = level;
            _levelOut.Emit(level);
        }

        float[]? IInlineWaveform.WaveformBuffer => _waveform;
        int IInlineWaveform.WaveformLength => BufferSize;
        int IInlineWaveform.WaveformWriteIndex => _writeIndex;
        string IInlineWaveform.WaveformLabel => _currentLevel.ToString("F3");
        int IInlineWaveform.WaveformVersion => _waveformVersion;
    }
}
