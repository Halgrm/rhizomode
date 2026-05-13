#nullable enable

using System;
using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Generators
{
    /// <summary>
    /// 周期的波形を生成するノード。Sin/Saw/Square/Triangleの4モード切替対応。
    /// 位相アキュムレータ方式で周波数変更時の不連続を防止。
    /// </summary>
    [NodeType("LFO", "LFO", NodeCategory.Time)]
    public class LfoNode : NodeBase, IInlineButton, INodeParamAccessor
    {
        private const float DefaultFrequency = 1f;
        private const float DefaultAmplitude = 1f;
        private const int WaveformCount = 4;

        private readonly OutputPort<float> _valueOut;
        private float _frequency = DefaultFrequency;
        private float _amplitude = DefaultAmplitude;
        private float _phase;
        private int _waveformIndex;

        private static readonly string[] WaveformNames = { "Sin", "Saw", "Square", "Triangle" };

        /// <inheritdoc />
        string IInlineButton.ButtonLabel => WaveformNames[_waveformIndex];

        /// <inheritdoc />
        void IInlineButton.OnButtonPressed()
        {
            _waveformIndex = (_waveformIndex + 1) % WaveformCount;
        }

        public LfoNode(string id) : base(id, "LFO")
        {
            RegisterInput<float>("Frequency", ParamType.Float);
            RegisterInput<float>("Amplitude", ParamType.Float);
            _valueOut = RegisterOutput<float>("Value", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "Frequency")
                    .Subscribe(v => _frequency = Mathf.Max(v, 0f)));

            AddSubscription(
                context.GetInputObservable<float>(this, "Amplitude")
                    .Subscribe(v => _amplitude = Mathf.Clamp01(v)));

            AddSubscription(
                Observable.EveryUpdate()
                    .Subscribe(_ =>
                    {
                        _phase = ((_phase + _frequency * UnityEngine.Time.deltaTime) % 1f + 1f) % 1f;
                        var raw = EvaluateWaveform(_phase, _waveformIndex);
                        _valueOut.Emit(raw * _amplitude);
                    }));
        }

        /// <summary>
        /// 指定された波形モードで位相(0-1)から出力値(0-1)を算出する。
        /// </summary>
        private static float EvaluateWaveform(float phase, int mode)
        {
            return mode switch
            {
                0 => (Mathf.Sin(phase * Mathf.PI * 2f) + 1f) * 0.5f, // Sin: 0-1
                1 => phase,                                            // Saw: 0-1
                2 => phase < 0.5f ? 1f : 0f,                          // Square: 0 or 1
                3 => phase < 0.5f ? phase * 2f : 2f - phase * 2f,     // Triangle: 0-1
                _ => 0f
            };
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new LfoParams
            {
                waveform = _waveformIndex,
                frequency = _frequency,
                amplitude = _amplitude
            });
            return data;
        }

        /// <inheritdoc />
        public override void RestoreParamsFromJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<LfoParams>(paramsJson);
                _waveformIndex = Mathf.Clamp(p.waveform, 0, WaveformCount - 1);
                _frequency = Mathf.Max(p.frequency, 0f);
                _amplitude = Mathf.Clamp01(p.amplitude);
            }
            catch (Exception)
            {
                // 破損したJSONは無視、デフォルト値を維持
            }
        }

        [Serializable]
        private struct LfoParams
        {
            public int waveform;
            public float frequency;
            public float amplitude;
        }

        bool INodeParamAccessor.TrySetParam(string paramName, ParamValue value)
        {
            if (value.Type != ParamType.Float) return false;
            switch (paramName)
            {
                case "Frequency": _frequency = Mathf.Max(value.AsFloat, 0f); return true;
                case "Amplitude": _amplitude = value.AsFloat; return true;
                default: return false;
            }
        }

        bool INodeParamAccessor.TryGetParam(string paramName, out ParamValue value)
        {
            switch (paramName)
            {
                case "Frequency": value = ParamValue.Float(_frequency); return true;
                case "Amplitude": value = ParamValue.Float(_amplitude); return true;
                default: value = default; return false;
            }
        }
    }
}
