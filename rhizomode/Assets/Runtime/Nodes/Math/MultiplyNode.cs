#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Math
{
    /// <summary>
    /// 2つのfloat入力を乗算して出力する。
    /// </summary>
    [NodeType("Multiply", "Multiply", NodeCategory.Math)]
    public class MultiplyNode : NodeBase
    {
        private readonly OutputPort<float> _resultOut;
        private float _a;
        private float _b;

        public MultiplyNode(string id) : base(id, "Multiply")
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
                        _resultOut.Emit(_a * _b);
                    }));

            AddSubscription(
                context.GetInputObservable<float>(this, "B")
                    .Subscribe(v =>
                    {
                        _b = v;
                        _resultOut.Emit(_a * _b);
                    }));
        }
    }
}
