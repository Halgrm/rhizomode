#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// float入力が閾値以上ならtrue、未満ならfalseを出力するゲートノード。
    /// </summary>
    [NodeType("Threshold", "Threshold", NodeCategory.Utility)]
    public class ThresholdNode : NodeBase
    {
        private const float DefaultThreshold = 0.5f;

        private readonly OutputPort<bool> _gateOut;
        private float _value;
        private float _threshold = DefaultThreshold;

        public ThresholdNode(string id) : base(id, "Threshold")
        {
            RegisterInput<float>("Value", ParamType.Float);
            RegisterInput<float>("Threshold", ParamType.Float);
            _gateOut = RegisterOutput<bool>("Gate", ParamType.Bool);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "Value")
                    .Subscribe(v =>
                    {
                        _value = v;
                        _gateOut.Emit(_value >= _threshold);
                    }));

            AddSubscription(
                context.GetInputObservable<float>(this, "Threshold")
                    .Subscribe(v =>
                    {
                        _threshold = v;
                        _gateOut.Emit(_value >= _threshold);
                    }));
        }
    }
}
