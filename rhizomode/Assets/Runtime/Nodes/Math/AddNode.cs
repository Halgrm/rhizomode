#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;

namespace Rhizomode.Nodes.Math
{
    /// <summary>
    /// 2つのfloat入力を加算して出力する。
    /// </summary>
    public class AddNode : NodeBase
    {
        private readonly OutputPort<float> _resultOut;
        private float _a;
        private float _b;

        public AddNode(string id) : base(id, "Add")
        {
            RegisterInput<float>("A", ParamType.Float);
            RegisterInput<float>("B", ParamType.Float);
            _resultOut = RegisterOutput<float>("Result", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "A")
                    .Subscribe(v =>
                    {
                        _a = v;
                        _resultOut.Emit(_a + _b);
                    }));

            AddSubscription(
                context.GetInputObservable<float>(this, "B")
                    .Subscribe(v =>
                    {
                        _b = v;
                        _resultOut.Emit(_a + _b);
                    }));
        }
    }
}
