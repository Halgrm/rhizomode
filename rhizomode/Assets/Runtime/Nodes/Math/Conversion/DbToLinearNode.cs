#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.Nodes.Math
{
    /// <summary>
    /// dBFS を線形振幅に変換する (0 dB = 1.0)。
    /// </summary>
    /// <remarks>
    /// 公式: linear = 10^(db / 20)
    /// NaN / +Inf は 0、-Inf は 0 (silent) を返す。
    /// </remarks>
    [NodeType("DbToLinear", "dB → Linear", NodeCategory.Math)]
    public class DbToLinearNode : NodeBase
    {
        private readonly OutputPort<float> _linearOut;

        public DbToLinearNode(string id) : base(id, "DbToLinear")
        {
            RegisterInput<float>("dB", ParamType.Float, PortUnit.Decibels);
            _linearOut = RegisterOutput<float>("Linear", ParamType.Float, PortUnit.Normalized);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "dB")
                    .Subscribe(db => _linearOut.Emit(Compute(db))));
        }

        private static float Compute(float db)
        {
            if (float.IsNaN(db) || float.IsPositiveInfinity(db)) return 0f;
            if (float.IsNegativeInfinity(db)) return 0f;
            return Mathf.Pow(10f, db / 20f);
        }
    }
}
