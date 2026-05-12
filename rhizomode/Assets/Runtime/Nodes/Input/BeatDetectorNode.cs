#nullable enable

using R3;
using Rhizomode.Core;
using UnityEngine;

namespace Rhizomode.Nodes.Input
{
    /// <summary>
    /// トリガー入力からBPMを推定し、Phase(0〜1)とBeatパルスを出力する。
    /// 直近のトリガー間隔からBPMを計算するシンプルな実装。
    /// </summary>
    public class BeatDetectorNode : NodeBase
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

        public BeatDetectorNode(string id) : base(id, "BeatDetector")
        {
            RegisterInput<bool>("Trigger", ParamType.Bool);
            _bpmOut = RegisterOutput<float>("BPM", ParamType.Float);
            _phaseOut = RegisterOutput<float>("Phase", ParamType.Float);
            _beatOut = RegisterOutput<bool>("Beat", ParamType.Bool);
            _beatInterval = 60f / DefaultBPM;
        }

        public override void Setup(GraphContext context)
        {
            // トリガー入力の立ち上がりでBPM更新
            AddSubscription(
                context.GetInputObservable<bool>(this, "Trigger")
                    .Where(v => v)
                    .Subscribe(_ => OnTrigger()));

            // 毎フレームPhaseとBeatを発行
            AddSubscription(
                Observable.EveryUpdate()
                    .Subscribe(_ => UpdatePhase()));
        }

        private void OnTrigger()
        {
            var now = UnityEngine.Time.time;

            // タイムアウト判定: 前回から長すぎたらリセット
            if (now - _lastTapTime > TapTimeout)
            {
                _tapCount = 0;
            }

            // 履歴に追加（リングバッファ）
            if (_tapCount < MaxTapHistory)
            {
                _tapTimes[_tapCount] = now;
                _tapCount++;
            }
            else
            {
                // シフト
                for (var i = 1; i < MaxTapHistory; i++)
                    _tapTimes[i - 1] = _tapTimes[i];
                _tapTimes[MaxTapHistory - 1] = now;
            }

            _lastTapTime = now;

            // 2つ以上のタップがあればBPM計算
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

            // ビート検出: phaseが0に近い瞬間
            var prevElapsed = elapsed - UnityEngine.Time.deltaTime;
            if (prevElapsed < 0) prevElapsed = 0;
            var prevPhase = (prevElapsed % _beatInterval) / _beatInterval;
            var isBeat = phase < prevPhase;
            if (isBeat)
            {
                _beatOut.Emit(true);
                _beatEmitted = true;
            }
        }
    }
}
