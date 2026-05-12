#nullable enable

using R3;
using Rhizomode.Core;
using UnityEngine;

namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// Color入力をH/S/V/Aに変換して出力する。RGB→HSV変換にはUnityEngine.Color.RGBToHSVを使用。
    /// </summary>
    public class ColorToHSVNode : NodeBase
    {
        private readonly OutputPort<float> _hOut;
        private readonly OutputPort<float> _sOut;
        private readonly OutputPort<float> _vOut;
        private readonly OutputPort<float> _aOut;

        public ColorToHSVNode(string id) : base(id, "ColorToHSV")
        {
            RegisterInput<Color>("Color", ParamType.Color);
            _hOut = RegisterOutput<float>("H", ParamType.Float);
            _sOut = RegisterOutput<float>("S", ParamType.Float);
            _vOut = RegisterOutput<float>("V", ParamType.Float);
            _aOut = RegisterOutput<float>("A", ParamType.Float);
        }

        public override void Setup(GraphContext context)
        {
            AddSubscription(
                context.GetInputObservable<Color>(this, "Color")
                    .Subscribe(c =>
                    {
                        Color.RGBToHSV(c, out float h, out float s, out float v);
                        _hOut.Emit(h);
                        _sOut.Emit(s);
                        _vOut.Emit(v);
                        _aOut.Emit(c.a);
                    }));
        }
    }
}
