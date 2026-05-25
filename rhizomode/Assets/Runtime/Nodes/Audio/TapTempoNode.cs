#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.UI.Contracts;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Audio
{
    /// <summary>
    /// コントローラーのタップから BPM を推定する。外部から <see cref="Tap"/> を呼ぶことで駆動。
    /// </summary>
    /// <remarks>
    /// 時刻は <see cref="UnityEngine.Time.unscaledTime"/> を直接読む (controller タップ遅延は
    /// audio I/F とは独立。<c>AudioClock.LatencyOffsetSeconds</c> を引かない)。
    /// BPM 計算と phase は <see cref="TempoTracker"/> に集約。
    /// </remarks>
    [NodeType("TapTempo", "Tap Tempo", NodeCategory.Input)]
    public class TapTempoNode : NodeBase, IInlineButton
    {
        private readonly OutputPort<float> _bpmOut;
        private readonly OutputPort<float> _phaseOut;
        private readonly OutputPort<bool> _beatOut;

        private TempoTracker _tracker;
        private bool _beatEmitted;
        private float _lastTickTime;

        public TapTempoNode(string id) : base(id, "TapTempo")
        {
            _bpmOut = RegisterOutput<float>("BPM", ParamType.Float);
            _phaseOut = RegisterOutput<float>("Phase", ParamType.Float);
            _beatOut = RegisterOutput<bool>("Beat", ParamType.Bool);
        }

        /// <inheritdoc />
        string IInlineButton.ButtonLabel => "TAP";

        /// <inheritdoc />
        void IInlineButton.OnButtonPressed() => Tap();

        /// <summary>
        /// 外部 (コントローラー入力) からタップを受け取る。
        /// </summary>
        public void Tap()
        {
            var now = UnityEngine.Time.unscaledTime;
            if (_tracker.OnTap(now))
                _bpmOut.Emit(_tracker.Bpm);
        }

        public override void Setup(GraphState context)
        {
            _lastTickTime = UnityEngine.Time.unscaledTime;
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

            var now = UnityEngine.Time.unscaledTime;
            var dt = now - _lastTickTime;
            _lastTickTime = now;
            if (dt < 0f) dt = 0f;

            var (phase, isBeat) = _tracker.Tick(now, dt);
            if (_tracker.BeatInterval <= TempoTracker.MinBeatIntervalSec) return;

            _phaseOut.Emit(phase);
            if (isBeat)
            {
                _beatOut.Emit(true);
                _beatEmitted = true;
            }
        }
    }
}
