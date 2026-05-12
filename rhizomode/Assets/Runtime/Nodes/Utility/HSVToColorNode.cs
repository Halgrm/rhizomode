#nullable enable

using R3;
using Rhizomode.Core;
using UnityEngine;

namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// H/S/V/Aの4つのfloat入力からColorを合成して出力する。HSV→RGB変換にはUnityEngine.Color.HSVToRGBを使用。
    /// </summary>
    public class HSVToColorNode : NodeBase
    {
        private readonly OutputPort<Color> _colorOut;
        private float _h;
        private float _s = 1f;
        private float _v = 1f;
        private float _a = 1f;

        public HSVToColorNode(string id) : base(id, "HSVToColor")
        {
            RegisterInput<float>("H", ParamType.Float);
            RegisterInput<float>("S", ParamType.Float);
            RegisterInput<float>("V", ParamType.Float);
            RegisterInput<float>("A", ParamType.Float);
            _colorOut = RegisterOutput<Color>("Color", ParamType.Color);
        }

        public override void Setup(GraphContext context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "H")
                    .Subscribe(v =>
                    {
                        _h = v;
                        EmitColor();
                    }));

            AddSubscription(
                context.GetInputObservable<float>(this, "S")
                    .Subscribe(v =>
                    {
                        _s = v;
                        EmitColor();
                    }));

            AddSubscription(
                context.GetInputObservable<float>(this, "V")
                    .Subscribe(v =>
                    {
                        _v = v;
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
            Color rgb = Color.HSVToRGB(_h, _s, _v);
            _colorOut.Emit(new Color(rgb.r, rgb.g, rgb.b, _a));
        }
    }
}
