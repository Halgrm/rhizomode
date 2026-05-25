#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;

using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.Nodes.Math
{
    /// <summary>
    /// BPM を 1 拍あたりの秒数に変換する (60 / bpm)。
    /// </summary>
    /// <remarks>
    /// bpm &lt;= 0 / NaN / Inf は 0 を返す (ゼロ除算回避)。
    /// </remarks>
    [NodeType("BpmToSec", "BPM → Sec", NodeCategory.Math)]
    public class BpmToSecNode : NodeBase
    {
        private readonly OutputPort<float> _secOut;

        public BpmToSecNode(string id) : base(id, "BpmToSec")
        {
            RegisterInput<float>("BPM", ParamType.Float, PortUnit.Bpm);
            _secOut = RegisterOutput<float>("Seconds", ParamType.Float, PortUnit.Seconds);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "BPM")
                    .Subscribe(bpm => _secOut.Emit(Compute(bpm))));
        }

        private static float Compute(float bpm)
        {
            if (!float.IsFinite(bpm) || bpm <= 0f) return 0f;
            return 60f / bpm;
        }
    }
}
