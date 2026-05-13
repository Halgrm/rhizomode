#nullable enable

using System;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

namespace Rhizomode.Nodes.Audio
{
    /// <summary>
    /// オーディオスペクトルをノードパネル内にバー表示するモニターノード。
    /// AudioDriverBehaviour が毎フレームスペクトルデータを注入する。
    /// </summary>
    public class SpectrumMonitorNode : NodeBase, IInlineSpectrum
    {
        private const int BufferSize = 64;

        private readonly OutputPort<float> _levelOut;
        private readonly float[] _spectrum = new float[BufferSize];
        private float _currentLevel;

        public SpectrumMonitorNode(string id) : base(id, "SpectrumMonitor")
        {
            _levelOut = RegisterOutput<float>("Level", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            _levelOut.Emit(0f);
        }

        /// <summary>
        /// AudioDriverBehaviour からスペクトルデータを注入する。
        /// </summary>
        public void SetSpectrum(float[] data)
        {
            if (data == null) return;
            var len = Mathf.Min(data.Length, BufferSize);
            Array.Copy(data, 0, _spectrum, 0, len);
            for (var i = len; i < BufferSize; i++)
                _spectrum[i] = 0f;
        }

        /// <summary>
        /// 全帯域レベルを設定し出力する。
        /// </summary>
        public void SetLevel(float level)
        {
            if (float.IsNaN(level) || float.IsInfinity(level))
                level = 0f;

            _currentLevel = level;
            _levelOut.Emit(level);
        }

        float[]? IInlineSpectrum.SpectrumBuffer => _spectrum;
        int IInlineSpectrum.SpectrumLength => BufferSize;
        string IInlineSpectrum.SpectrumLabel => _currentLevel.ToString("F3");
    }
}
