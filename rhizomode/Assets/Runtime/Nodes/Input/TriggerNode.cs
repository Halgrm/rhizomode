#nullable enable

using R3;
using Rhizomode.Core;

namespace Rhizomode.Nodes.Input
{
    /// <summary>
    /// ボタン押下で1フレームだけtrueを発行するトリガーノード。
    /// </summary>
    public class TriggerNode : NodeBase, IInlineButton
    {
        private readonly OutputPort<bool> _triggerOut;
        private bool _fired;

        public TriggerNode(string id) : base(id, "Trigger")
        {
            _triggerOut = RegisterOutput<bool>("Trigger", ParamType.Bool);
        }

        /// <inheritdoc />
        string IInlineButton.ButtonLabel => "TRIG";

        /// <inheritdoc />
        void IInlineButton.OnButtonPressed()
        {
            _triggerOut.Emit(true);
            _fired = true;
        }

        public override void Setup(GraphContext context)
        {
            AddSubscription(
                Observable.EveryUpdate()
                    .Subscribe(_ =>
                    {
                        if (!_fired) return;
                        _triggerOut.Emit(false);
                        _fired = false;
                    }));
        }
    }
}
