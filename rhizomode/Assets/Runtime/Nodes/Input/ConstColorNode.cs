#nullable enable

using System;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.UI.Contracts;
using Rhizomode.Graph.Serialization;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Input
{
    /// <summary>
    /// 固定Color値を出力するノード。HSVカラーピッカーUI内蔵。
    /// </summary>
    [NodeType("ConstColor", "Const Color", NodeCategory.Input)]
    public class ConstColorNode : NodeBase, IInlineColorPicker, INodeParamAccessor
    {
        private static readonly Color DefaultColor = Color.white;

        private readonly OutputPort<Color> _valueOut;
        private Color _color = DefaultColor;

        /// <summary>現在の色。UIピッカーから設定される。</summary>
        public Color Value
        {
            get => _color;
            set
            {
                _color = value;
                _valueOut.Emit(_color);
            }
        }

        /// <inheritdoc />
        Color IInlineColorPicker.PickerColor
        {
            get => _color;
            set => Value = value;
        }

        public ConstColorNode(string id) : base(id, "ConstColor")
        {
            _valueOut = RegisterOutput<Color>("Value", ParamType.Color);
        }

        /// <inheritdoc />
        public override void RestoreParamsFromJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<ConstColorParams>(paramsJson);
                _color = new Color(p.r, p.g, p.b, p.a);
            }
            catch (Exception)
            {
                // 破損したJSONは無視、デフォルト値を維持
            }
        }

        public override void Setup(GraphState context)
        {
            // 初期値を発行
            _valueOut.Emit(_color);
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new ConstColorParams
            {
                r = _color.r,
                g = _color.g,
                b = _color.b,
                a = _color.a
            });
            return data;
        }

        [Serializable]
        private struct ConstColorParams
        {
            public float r;
            public float g;
            public float b;
            public float a;
        }

        bool INodeParamAccessor.TrySetParam(string paramName, ParamValue value)
        {
            if (paramName == "Value" && value.Type == ParamType.Color)
            {
                var rz = value.AsColor;
                Value = new Color(rz.R, rz.G, rz.B, rz.A);
                return true;
            }
            return false;
        }

        bool INodeParamAccessor.TryGetParam(string paramName, out ParamValue value)
        {
            if (paramName == "Value")
            {
                value = ParamValue.Color(new RzColor(_color.r, _color.g, _color.b, _color.a));
                return true;
            }
            value = default;
            return false;
        }
    }
}
