#nullable enable

using System;
using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using UnityEngine;

namespace Rhizomode.Nodes.Time
{
    /// <summary>
    /// リセット可能なタイマーノード。経過秒数と周期的位相(0-1)を出力する。
    /// Resetの立ち上がりエッジでタイマーをリセットする。
    /// </summary>
    public class TimerNode : NodeBase
    {
        private const float DefaultDuration = 1f;

        private readonly OutputPort<float> _elapsedOut;
        private readonly OutputPort<float> _phaseOut;
        private float _elapsed;
        private float _duration = DefaultDuration;
        private bool _resetValue;
        private bool _prevReset;

        public TimerNode(string id) : base(id, "Timer")
        {
            RegisterInput<bool>("Reset", ParamType.Bool);
            RegisterInput<float>("Duration", ParamType.Float);
            _elapsedOut = RegisterOutput<float>("Elapsed", ParamType.Float);
            _phaseOut = RegisterOutput<float>("Phase", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<bool>(this, "Reset")
                    .Subscribe(v => { _resetValue = v; }));

            AddSubscription(
                context.GetInputObservable<float>(this, "Duration")
                    .Subscribe(v => _duration = Mathf.Max(v, 0f)));

            AddSubscription(
                Observable.EveryUpdate()
                    .Subscribe(_ =>
                    {
                        // 立ち上がりエッジ検出
                        if (_resetValue && !_prevReset)
                        {
                            _elapsed = 0f;
                        }
                        _prevReset = _resetValue;

                        _elapsed += UnityEngine.Time.deltaTime;
                        _elapsedOut.Emit(_elapsed);

                        // duration <= 0 のガード
                        if (_duration <= 0f)
                        {
                            _phaseOut.Emit(0f);
                            return;
                        }

                        var phase = (_elapsed % _duration) / _duration;
                        _phaseOut.Emit(phase);
                    }));
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new TimerParams
            {
                duration = _duration
            });
            return data;
        }

        /// <inheritdoc />
        public override void RestoreParamsFromJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<TimerParams>(paramsJson);
                _duration = Mathf.Max(p.duration, 0f);
            }
            catch (Exception)
            {
                // 破損したJSONは無視、デフォルト値を維持
            }
        }

        [Serializable]
        private struct TimerParams
        {
            public float duration;
        }
    }
}
