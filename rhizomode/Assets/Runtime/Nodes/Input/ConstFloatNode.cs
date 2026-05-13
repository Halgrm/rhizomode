#nullable enable

using System;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Input
{
    /// <summary>
    /// 固定float値を出力するノード。スライダーUI内蔵。
    /// レンジプリセットをボタンでサイクル切替可能。
    /// </summary>
    [NodeType("ConstFloat", "Const Float", NodeCategory.Input)]
    public class ConstFloatNode : NodeBase, IInlineSlider, IInlineButton
    {
        private static readonly (float min, float max)[] RangePresets =
        {
            (0f, 1f),
            (0f, 10f),
            (0f, 100f),
            (0f, 1000f),
            (-1f, 1f),
            (-100f, 100f),
        };

        private const float DefaultValue = 0.5f;

        private readonly OutputPort<float> _valueOut;
        private float _value = DefaultValue;
        private int _rangeIndex;

        /// <summary>現在の値。UIスライダーから設定される。</summary>
        public float Value
        {
            get => _value;
            set
            {
                var (min, max) = RangePresets[_rangeIndex];
                _value = Mathf.Clamp(value, min, max);
                _valueOut.Emit(_value);
            }
        }

        /// <inheritdoc />
        float IInlineSlider.SliderValue
        {
            get => _value;
            set => Value = value;
        }

        /// <inheritdoc />
        float IInlineSlider.SliderMin => RangePresets[_rangeIndex].min;

        /// <inheritdoc />
        float IInlineSlider.SliderMax => RangePresets[_rangeIndex].max;

        /// <inheritdoc />
        string IInlineSlider.SliderLabel => "Value";

        /// <inheritdoc />
        string IInlineButton.ButtonLabel => $"{RangePresets[_rangeIndex].min}~{RangePresets[_rangeIndex].max}";

        /// <inheritdoc />
        void IInlineButton.OnButtonPressed()
        {
            // 現在のスライダー正規化位置を保持
            var (oldMin, oldMax) = RangePresets[_rangeIndex];
            float oldRange = oldMax - oldMin;
            float normalized = oldRange > Mathf.Epsilon ? (_value - oldMin) / oldRange : 0.5f;

            // 次のプリセットへ
            _rangeIndex = (_rangeIndex + 1) % RangePresets.Length;

            // 新レンジで値を再計算
            var (newMin, newMax) = RangePresets[_rangeIndex];
            _value = newMin + normalized * (newMax - newMin);
            _valueOut.Emit(_value);
        }

        public ConstFloatNode(string id) : base(id, "ConstFloat")
        {
            _valueOut = RegisterOutput<float>("Value", ParamType.Float);
        }

        /// <inheritdoc />
        public override void RestoreParamsFromJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<ConstFloatParams>(paramsJson);
                _rangeIndex = Mathf.Clamp(p.rangeIndex, 0, RangePresets.Length - 1);
                var (min, max) = RangePresets[_rangeIndex];
                _value = Mathf.Clamp(p.value, min, max);
            }
            catch (Exception)
            {
                // 破損したJSONは無視、デフォルト値を維持
            }
        }

        public override void Setup(GraphState context)
        {
            // 初期値を発行
            _valueOut.Emit(_value);
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new ConstFloatParams { value = _value, rangeIndex = _rangeIndex });
            return data;
        }

        [Serializable]
        private struct ConstFloatParams
        {
            public float value;
            public int rangeIndex;
        }
    }
}
