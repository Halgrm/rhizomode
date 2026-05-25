#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;

using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.Nodes.Math
{
    /// <summary>
    /// 1 拍あたりの秒数を BPM に変換する (60 / sec)。
    /// </summary>
    /// <remarks>
    /// sec &lt;= 0 / NaN / Inf は 0 を返す (ゼロ除算回避)。
    /// </remarks>
    [NodeType("SecToBpm", "Sec → BPM", NodeCategory.Math)]
    public class SecToBpmNode : NodeBase
    {
        private readonly OutputPort<float> _bpmOut;

        public SecToBpmNode(string id) : base(id, "SecToBpm")
        {
            RegisterInput<float>("Seconds", ParamType.Float, PortUnit.Seconds);
            _bpmOut = RegisterOutput<float>("BPM", ParamType.Float, PortUnit.Bpm);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "Seconds")
                    .Subscribe(sec => _bpmOut.Emit(Compute(sec))));
        }

        private static float Compute(float sec)
        {
            if (!float.IsFinite(sec) || sec <= 0f) return 0f;
            return 60f / sec;
        }
    }
}
