#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Audio
{
    /// <summary>
    /// コントローラーのタップからBPMを推定する。外部からTap()を呼ぶことで駆動。
    /// BeatDetectorNodeと同じPhase/Beat出力を持つ。
    /// </summary>
    [NodeType("TapTempo", "Tap Tempo", NodeCategory.Input)]
    public class TapTempoNode : NodeBase, IInlineButton
    {
        private const float DefaultBPM = 120f;
        private const int MaxTapHistory = 8;
        private const float TapTimeout = 3f;

        private readonly OutputPort<float> _bpmOut;
        private readonly OutputPort<float> _phaseOut;
        private readonly OutputPort<bool> _beatOut;

        private readonly float[] _tapTimes = new float[MaxTapHistory];
        private int _tapCount;
        private float _lastTapTime;
        private float _bpm = DefaultBPM;
        private float _beatInterval;
        private float _phaseOrigin;
        private bool _beatEmitted;

        public TapTempoNode(string id) : base(id, "TapTempo")
        {
            _bpmOut = RegisterOutput<float>("BPM", ParamType.Float);
            _phaseOut = RegisterOutput<float>("Phase", ParamType.Float);
            _beatOut = RegisterOutput<bool>("Beat", ParamType.Bool);
            _beatInterval = 60f / DefaultBPM;
        }

        /// <inheritdoc />
        string IInlineButton.ButtonLabel => "TAP";

        /// <inheritdoc />
        void IInlineButton.OnButtonPressed() => Tap();

        /// <summary>
        /// 外部（コントローラー入力）からタップを受け取る。
        /// </summary>
        public void Tap()
        {
            var now = UnityEngine.Time.time;

            if (now - _lastTapTime > TapTimeout)
                _tapCount = 0;

            if (_tapCount < MaxTapHistory)
            {
                _tapTimes[_tapCount] = now;
                _tapCount++;
            }
            else
            {
                for (var i = 1; i < MaxTapHistory; i++)
                    _tapTimes[i - 1] = _tapTimes[i];
                _tapTimes[MaxTapHistory - 1] = now;
            }

            _lastTapTime = now;

            if (_tapCount >= 2)
            {
                var totalInterval = _tapTimes[_tapCount - 1] - _tapTimes[0];
                var avgInterval = totalInterval / (_tapCount - 1);
                if (avgInterval > 0.001f)
                {
                    _bpm = 60f / avgInterval;
                    _beatInterval = avgInterval;
                    _phaseOrigin = now;
                    _bpmOut.Emit(_bpm);
                }
            }
        }

        public override void Setup(GraphState context)
        {
            // 毎フレームPhaseとBeatを発行
            AddSubscription(
                Observable.EveryUpdate()
                    .Subscribe(_ => UpdatePhase()));
        }

        private void UpdatePhase()
        {
            if (_beatEmitted)
            {
                _beatOut.Emit(false);
                _beatEmitted = false;
            }

            if (_beatInterval <= 0.001f) return;

            var elapsed = UnityEngine.Time.time - _phaseOrigin;
            var phase = (elapsed % _beatInterval) / _beatInterval;
            _phaseOut.Emit(phase);

            var prevElapsed = elapsed - UnityEngine.Time.deltaTime;
            if (prevElapsed < 0) prevElapsed = 0;
            var prevPhase = (prevElapsed % _beatInterval) / _beatInterval;
            if (phase < prevPhase)
            {
                _beatOut.Emit(true);
                _beatEmitted = true;
            }
        }
    }
}
