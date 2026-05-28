#nullable enable

using R3;
using Rhizomode.Graph.Model;
using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.SharedKernel;

namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// Shared logic for the Rector-style count nodes: each Trigger rising edge advances a
    /// one-hot step (1..N) and emits the matching bool output plus an Index float.
    /// </summary>
    /// <remarks>
    /// Concrete subclasses only differ in the step count (4 / 8 / 16). The step bool outputs
    /// are named "1".."N"; exactly one is true after each trigger so they can drive N mutually
    /// exclusive targets (e.g. CameraSwitch nodes) directly.
    /// </remarks>
    public abstract class StepCountNodeBase : NodeBase, INodeParamAccessor
    {
        private const int FirstStep = 1;

        private readonly OutputPort<bool>[] _stepOuts;
        private readonly OutputPort<float> _indexOut;
        private readonly int _steps;
        private int _step = FirstStep;
        private bool _prevTrigger;

        protected StepCountNodeBase(string id, string typeName, int steps) : base(id, typeName)
        {
            _steps = steps;
            RegisterInput<bool>("Trigger", ParamType.Bool);
            _stepOuts = new OutputPort<bool>[steps];
            for (int i = 0; i < steps; i++)
            {
                _stepOuts[i] = RegisterOutput<bool>((i + 1).ToString(), ParamType.Bool);
            }
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
                _step = _step >= _steps ? FirstStep : _step + 1;
            }

            _prevTrigger = trigger;
        }

        private void EmitStep(int step)
        {
            for (int i = 0; i < _stepOuts.Length; i++)
            {
                _stepOuts[i].Emit(step == i + 1);
            }
            _indexOut.Emit(step);
        }
    }
}
