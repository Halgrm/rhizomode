#nullable enable

using R3;
using Rhizomode.Graph.Model;
using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.SharedKernel;

namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// Converts Trigger rising edges into 1-4 one-hot bool outputs and an Index float.
    /// </summary>
    [NodeType("Count", "Count", NodeCategory.Utility)]
    public class CountNode : NodeBase, INodeParamAccessor
    {
        private const int FirstStep = 1;
        private const int LastStep = 4;

        private readonly OutputPort<bool> _oneOut;
        private readonly OutputPort<bool> _twoOut;
        private readonly OutputPort<bool> _threeOut;
        private readonly OutputPort<bool> _fourOut;
        private readonly OutputPort<float> _indexOut;
        private int _step = FirstStep;
        private bool _prevTrigger;

        public CountNode(string id) : base(id, "Count")
        {
            RegisterInput<bool>("Trigger", ParamType.Bool);
            _oneOut = RegisterOutput<bool>("1", ParamType.Bool);
            _twoOut = RegisterOutput<bool>("2", ParamType.Bool);
            _threeOut = RegisterOutput<bool>("3", ParamType.Bool);
            _fourOut = RegisterOutput<bool>("4", ParamType.Bool);
            _indexOut = RegisterOutput<float>("Index", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<bool>(this, "Trigger")
                    .Subscribe(OnTrigger));
        }

        bool INodeParamAccessor.TrySetParam(string paramName, ParamValue value) => false;

        bool INodeParamAccessor.TryGetParam(string paramName, out ParamValue value)
        {
            value = default;
            return false;
        }

        private void OnTrigger(bool trigger)
        {
            if (!_prevTrigger && trigger)
            {
                EmitStep(_step);
                _step = _step >= LastStep ? FirstStep : _step + 1;
            }

            _prevTrigger = trigger;
        }

        private void EmitStep(int step)
        {
            _oneOut.Emit(step == 1);
            _twoOut.Emit(step == 2);
            _threeOut.Emit(step == 3);
            _fourOut.Emit(step == 4);
            _indexOut.Emit(step);
        }
    }
}
