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
    public class ThresholdNode : NodeBase, INodeParamAccessor
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

        bool INodeParamAccessor.TrySetParam(string paramName, ParamValue value)
        {
            if (paramName == "Threshold" && value.Type == ParamType.Float)
            {
                _threshold = value.AsFloat;
                // 注意: ConstFloat/Color と異なりここでは _gateOut.Emit を呼ばない。
                // 理由: Threshold ノードは "Value" 入力ポートから流れてきた値と比較する gate。
                // FreshSpawn 直後は _value=0 (未受信)、Setup 後の最初の Value 入力受信時に
                // gate emit される。Setup 前に emit すると未初期化値で誤発火する。
                return true;
            }
            return false;
        }

        bool INodeParamAccessor.TryGetParam(string paramName, out ParamValue value)
        {
            if (paramName == "Threshold")
            {
                value = ParamValue.Float(_threshold);
                return true;
            }
            value = default;
            return false;
        }
    }
}
