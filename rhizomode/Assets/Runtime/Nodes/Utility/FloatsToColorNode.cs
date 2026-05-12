#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// R/G/B/Aの4つのfloat入力からColorを合成して出力する。
    /// </summary>
    public class FloatsToColorNode : NodeBase
    {
        private readonly OutputPort<Color> _colorOut;
        private float _r;
        private float _g;
        private float _b;
        private float _a = 1f;

        public FloatsToColorNode(string id) : base(id, "FloatsToColor")
        {
            RegisterInput<float>("R", ParamType.Float);
            RegisterInput<float>("G", ParamType.Float);
            RegisterInput<float>("B", ParamType.Float);
            RegisterInput<float>("A", ParamType.Float);
            _colorOut = RegisterOutput<Color>("Color", ParamType.Color);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "R")
                    .Subscribe(v =>
                    {
                        _r = v;
                        EmitColor();
                    }));

            AddSubscription(
                context.GetInputObservable<float>(this, "G")
                    .Subscribe(v =>
                    {
                        _g = v;
                        EmitColor();
                    }));

            AddSubscription(
                context.GetInputObservable<float>(this, "B")
                    .Subscribe(v =>
                    {
                        _b = v;
                        EmitColor();
                    }));

            AddSubscription(
                context.GetInputObservable<float>(this, "A")
                    .Subscribe(v =>
                    {
                        _a = v;
                        EmitColor();
                    }));
        }

        private void EmitColor()
        {
            _colorOut.Emit(new Color(_r, _g, _b, _a));
        }
    }
}
