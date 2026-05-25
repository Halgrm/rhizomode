#nullable enable

using R3;
using Rhizomode.Audio.Contracts;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Audio
{
    /// <summary>
    /// トリガー入力 (オーディオ立上り 等) から BPM を推定し、Phase(0〜1) と Beat パルスを出力する。
    /// </summary>
    /// <remarks>
    /// 時刻は <see cref="AudioClock.Now"/> を使う (LatencyOffsetSeconds による audio I/F 遅延補正が
    /// 適用される)。BPM 計算と phase は <see cref="TempoTracker"/> に集約。
    /// </remarks>
    [NodeType("BeatDetector", "Beat Detector", NodeCategory.Input)]
    public class BeatDetectorNode : NodeBase
    {
        private readonly OutputPort<float> _bpmOut;
        private readonly OutputPort<float> _phaseOut;
        private readonly OutputPort<bool> _beatOut;

        private TempoTracker _tracker;
        private bool _beatEmitted;
        private float _lastTickTime;

        public BeatDetectorNode(string id) : base(id, "BeatDetector")
        {
            RegisterInput<bool>("Trigger", ParamType.Bool);
            _bpmOut = RegisterOutput<float>("BPM", ParamType.Float);
            _phaseOut = RegisterOutput<float>("Phase", ParamType.Float);
            _beatOut = RegisterOutput<bool>("Beat", ParamType.Bool);
        }

        public override void Setup(GraphState context)
        {
            // トリガー入力の立ち上がりで BPM 更新
            AddSubscription(
                context.GetInputObservable<bool>(this, "Trigger")
                    .Where(v => v)
                    .Subscribe(_ => OnTrigger()));

            // 毎フレーム Phase と Beat を発行
            _lastTickTime = AudioClock.Now;
            AddSubscription(
                Observable.EveryUpdate()
                    .Subscribe(_ => UpdatePhase()));
        }

        private void OnTrigger()
        {
            var now = AudioClock.Now;
            if (_tracker.OnTap(now))
                _bpmOut.Emit(_tracker.Bpm);
        }

        private void UpdatePhase()
        {
            // 前フレームで発射した Beat=true を 1 frame 後に false に戻す (pulse 化)
            if (_beatEmitted)
            {
                _beatOut.Emit(false);
                _beatEmitted = false;
            }

            var now = AudioClock.Now;
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
