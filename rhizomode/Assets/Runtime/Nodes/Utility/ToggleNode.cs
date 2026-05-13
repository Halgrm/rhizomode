#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// boolトリガーの立ち上がりごとにtrue/falseをトグル出力する。
    /// </summary>
    [NodeType("Toggle", "Toggle", NodeCategory.Utility)]
    public class ToggleNode : NodeBase
    {
        private readonly OutputPort<bool> _stateOut;
        private bool _state;

        public ToggleNode(string id) : base(id, "Toggle")
        {
            RegisterInput<bool>("Trigger", ParamType.Bool);
            _stateOut = RegisterOutput<bool>("State", ParamType.Bool);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<bool>(this, "Trigger")
                    .Where(v => v)
                    .Subscribe(_ =>
                    {
                        _state = !_state;
                        _stateOut.Emit(_state);
                    }));
        }
    }
}
