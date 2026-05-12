#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// Color入力をR/G/B/Aの4つのfloat値に分解して出力する。
    /// </summary>
    public class ColorToFloatsNode : NodeBase
    {
        private readonly OutputPort<float> _rOut;
        private readonly OutputPort<float> _gOut;
        private readonly OutputPort<float> _bOut;
        private readonly OutputPort<float> _aOut;

        public ColorToFloatsNode(string id) : base(id, "ColorToFloats")
        {
            RegisterInput<Color>("Color", ParamType.Color);
            _rOut = RegisterOutput<float>("R", ParamType.Float);
            _gOut = RegisterOutput<float>("G", ParamType.Float);
            _bOut = RegisterOutput<float>("B", ParamType.Float);
            _aOut = RegisterOutput<float>("A", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<Color>(this, "Color")
                    .Subscribe(c =>
                    {
                        _rOut.Emit(c.r);
                        _gOut.Emit(c.g);
                        _bOut.Emit(c.b);
                        _aOut.Emit(c.a);
                    }));
        }
    }
}
