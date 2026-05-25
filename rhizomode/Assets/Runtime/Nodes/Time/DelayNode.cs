#nullable enable

using System;
using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Time
{
    /// <summary>
    /// 入力値を指定秒数だけ遅延して出力するノード。
    /// 循環バッファにタイムスタンプ付きで値を記録し、過去の値を再生する。
    /// </summary>
    [NodeType("Delay", "Delay", NodeCategory.Time)]
    public class DelayNode : NodeBase, INodeParamAccessor
    {
        private const float DefaultDelayTime = 0.5f;
        private const int BufferSize = 512;

        private readonly OutputPort<float> _valueOut;
        private readonly float[] _bufferValues = new float[BufferSize];
        private readonly float[] _bufferTimes = new float[BufferSize];
        private int _writeIndex;
        private int _count;
        private float _delayTime = DefaultDelayTime;
        private float _currentInput;

        public DelayNode(string id) : base(id, "Delay")
        {
            RegisterInput<float>("Input", ParamType.Float);
            RegisterInput<float>("DelayTime", ParamType.Float, PortUnit.Seconds);
            _valueOut = RegisterOutput<float>("Value", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "DelayTime")
                    .Subscribe(v => _delayTime = Mathf.Max(v, 0f)));

            AddSubscription(
                context.GetInputObservable<float>(this, "Input")
                    .Subscribe(v => _currentInput = v));

            AddSubscription(
                Observable.EveryUpdate()
                    .Subscribe(_ =>
                    {
                        var now = UnityEngine.Time.time;

                        // バッファに現在の値を書き込み
                        _bufferValues[_writeIndex] = _currentInput;
                        _bufferTimes[_writeIndex] = now;
                        _writeIndex = (_writeIndex + 1) % BufferSize;
                        if (_count < BufferSize) _count++;

                        // delayTime <= 0 の場合はパススルー
                        if (_delayTime <= 0f)
                        {
                            _valueOut.Emit(_currentInput);
                            return;
                        }

                        var targetTime = now - _delayTime;
                        _valueOut.Emit(ReadBufferAt(targetTime));
                    }));
        }

        /// <summary>
        /// 指定時刻に最も近いバッファ値を返す。バッファが空なら0を返す。
        /// </summary>
        private float ReadBufferAt(float targetTime)
        {
            if (_count == 0) return 0f;

            var bestValue = 0f;
            var bestDiff = float.MaxValue;
            var start = _count < BufferSize ? 0 : _writeIndex;

            for (int i = 0; i < _count; i++)
            {
                var idx = (start + i) % BufferSize;
                var diff = Mathf.Abs(_bufferTimes[idx] - targetTime);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestValue = _bufferValues[idx];
                }
                // バッファは時系列順なので、差が増加に転じたら打ち切り
                else if (diff > bestDiff)
                {
                    break;
                }
            }

            return bestValue;
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new DelayParams
            {
                delayTime = _delayTime
            });
            return data;
        }

        /// <inheritdoc />
        public override void RestoreParamsFromJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<DelayParams>(paramsJson);
                _delayTime = Mathf.Max(p.delayTime, 0f);
            }
            catch (Exception)
            {
                // 破損したJSONは無視、デフォルト値を維持
            }
        }

        [Serializable]
        private struct DelayParams
        {
            public float delayTime;
        }

        bool INodeParamAccessor.TrySetParam(string paramName, ParamValue value)
        {
            if (value.Type != ParamType.Float) return false;
            switch (paramName)
            {
                case "DelayTime": _delayTime = Mathf.Max(value.AsFloat, 0f); return true;
                default: return false;
            }
        }

        bool INodeParamAccessor.TryGetParam(string paramName, out ParamValue value)
        {
            switch (paramName)
            {
                case "DelayTime": value = ParamValue.Float(_delayTime); return true;
                default: value = default; return false;
            }
        }
    }
}
