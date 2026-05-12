#nullable enable

using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;

namespace Rhizomode.Nodes.Input
{
    /// <summary>
    /// LASP フィルタベースの帯域レベル (Level/Low/Mid/High) を出力する。
    /// AudioDriverBehaviour が毎フレーム SetBandLevels() で値を注入する。
    /// </summary>
    public class AudioBandNode : NodeBase
    {
        private readonly OutputPort<float> _levelOut;
        private readonly OutputPort<float> _lowOut;
        private readonly OutputPort<float> _midOut;
        private readonly OutputPort<float> _highOut;

        public AudioBandNode(string id) : base(id, "AudioBand")
        {
            _levelOut = RegisterOutput<float>("Level", ParamType.Float);
            _lowOut = RegisterOutput<float>("Low", ParamType.Float);
            _midOut = RegisterOutput<float>("Mid", ParamType.Float);
            _highOut = RegisterOutput<float>("High", ParamType.Float);
        }

        /// <summary>
        /// AudioDriverBehaviour から毎フレーム呼ばれる。
        /// </summary>
        public void SetBandLevels(float level, float low, float mid, float high)
        {
            _levelOut.Emit(Sanitize(level));
            _lowOut.Emit(Sanitize(low));
            _midOut.Emit(Sanitize(mid));
            _highOut.Emit(Sanitize(high));
        }

        private static float Sanitize(float v)
        {
            return float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
        }

        public override void Setup(GraphState context)
        {
            // 外部駆動のみ — Observable チェーン不要
        }
    }
}
