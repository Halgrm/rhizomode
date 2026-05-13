#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;

namespace Rhizomode.Nodes.Audio
{
    /// <summary>
    /// 指定周波数帯のオーディオレベルを監視し、閾値超過でトリガーを発行する。
    /// AudioAnalyzer（Week 4後半）から毎フレーム駆動される想定。
    /// </summary>
    public class AudioTriggerNode : NodeBase
    {
        private const float DefaultFreqMin = 60f;
        private const float DefaultFreqMax = 250f;
        private const float DefaultThreshold = 0.5f;

        private readonly InputPort<float> _freqMinIn;
        private readonly InputPort<float> _freqMaxIn;
        private readonly InputPort<float> _thresholdIn;
        private readonly OutputPort<float> _levelOut;
        private readonly OutputPort<bool> _triggerOut;

        private float _freqMin = DefaultFreqMin;
        private float _freqMax = DefaultFreqMax;
        private float _threshold = DefaultThreshold;
        private bool _wasAbove;

        /// <summary>現在の周波数帯下限。</summary>
        public float FreqMin => _freqMin;

        /// <summary>現在の周波数帯上限。</summary>
        public float FreqMax => _freqMax;

        /// <summary>外部（AudioAnalyzer）からレベル値を注入する。</summary>
        public void SetLevel(float level)
        {
            // NaN/Infinity防止（ランタイムフォールバック原則）
            if (float.IsNaN(level) || float.IsInfinity(level))
                level = 0f;

            _levelOut.Emit(level);

            var isAbove = level >= _threshold;
            if (isAbove && !_wasAbove)
            {
                _triggerOut.Emit(true);
            }
            else if (!isAbove && _wasAbove)
            {
                _triggerOut.Emit(false);
            }
            _wasAbove = isAbove;
        }

        public AudioTriggerNode(string id) : base(id, "AudioTrigger")
        {
            _freqMinIn = RegisterInput<float>("FreqMin", ParamType.Float);
            _freqMaxIn = RegisterInput<float>("FreqMax", ParamType.Float);
            _thresholdIn = RegisterInput<float>("Threshold", ParamType.Float);
            _levelOut = RegisterOutput<float>("Level", ParamType.Float);
            _triggerOut = RegisterOutput<bool>("Trigger", ParamType.Bool);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "FreqMin")
                    .Subscribe(v => { _freqMin = v; }));

            AddSubscription(
                context.GetInputObservable<float>(this, "FreqMax")
                    .Subscribe(v => { _freqMax = v; }));

            AddSubscription(
                context.GetInputObservable<float>(this, "Threshold")
                    .Subscribe(v => { _threshold = v; }));
        }
    }
}
