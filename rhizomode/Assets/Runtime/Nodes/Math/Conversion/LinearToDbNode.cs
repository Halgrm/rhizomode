#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.Nodes.Math
{
    /// <summary>
    /// 線形振幅を dBFS (0 dB = unity gain) に変換する。
    /// </summary>
    /// <remarks>
    /// 公式: db = 20 * log10(max(linear, ε))
    /// linear &lt;= 0 / NaN / Inf は -120 dB (silent floor) を返す。
    /// </remarks>
    [NodeType("LinearToDb", "Linear → dB", NodeCategory.Math)]
    public class LinearToDbNode : NodeBase
    {
        private const float SilentFloorDb = -120f;
        private const float Epsilon = 1e-6f;

        private readonly OutputPort<float> _dbOut;

        public LinearToDbNode(string id) : base(id, "LinearToDb")
        {
            RegisterInput<float>("Linear", ParamType.Float, PortUnit.Normalized);
            _dbOut = RegisterOutput<float>("dB", ParamType.Float, PortUnit.Decibels);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "Linear")
                    .Subscribe(linear => _dbOut.Emit(Compute(linear))));
        }

        private static float Compute(float linear)
        {
            if (!float.IsFinite(linear) || linear <= Epsilon) return SilentFloorDb;
            return 20f * Mathf.Log10(linear);
        }
    }
}
