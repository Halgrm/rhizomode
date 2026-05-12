#nullable enable

using NUnit.Framework;
using Rhizomode.UI;
using UnityEngine.UIElements;

namespace Rhizomode.Core.Tests
{
    /// <summary>
    /// RadialKnobElement の値クランプ・ChangeEvent 発火・SetValueWithoutNotify の検証。
    /// VisualElement を生成して直接プロパティ操作するロジックテスト
    /// (PointerEvent のドラッグ挙動は VR 実機で検証)。
    /// </summary>
    public class RadialKnobElementTests
    {
        [Test]
        public void DefaultRange_IsZeroToOne()
        {
            var knob = new RadialKnobElement();
            Assert.AreEqual(0f, knob.lowValue);
            Assert.AreEqual(1f, knob.highValue);
            Assert.AreEqual(0f, knob.value);
        }

        [Test]
        public void SetValue_ClampsToRange()
        {
            var knob = new RadialKnobElement { lowValue = 0f, highValue = 1f };

            knob.value = 1.5f;
            Assert.AreEqual(1f, knob.value, "値が highValue を超えるとクランプされる");

            knob.value = -0.3f;
            Assert.AreEqual(0f, knob.value, "値が lowValue を下回るとクランプされる");
        }

        [Test]
        public void SetValueWithoutNotify_DoesNotFireChangeEvent()
        {
            var knob = new RadialKnobElement { lowValue = 0f, highValue = 1f };
            var fired = false;
            knob.RegisterCallback<ChangeEvent<float>>(_ => fired = true);

            knob.SetValueWithoutNotify(0.5f);

            Assert.AreEqual(0.5f, knob.value);
            Assert.IsFalse(fired, "SetValueWithoutNotify は ChangeEvent を発火しない");
        }

        [Test]
        public void DragRangePx_HasMinimumFloor()
        {
            var knob = new RadialKnobElement();
            knob.dragRangePx = 0f;
            Assert.GreaterOrEqual(knob.dragRangePx, 10f,
                "dragRangePx は 10 未満にならない (ゼロ除算とジッタ防止)");
        }

        [Test]
        public void ChangingLowValue_ClampsCurrentValue()
        {
            var knob = new RadialKnobElement { lowValue = 0f, highValue = 1f };
            knob.SetValueWithoutNotify(0.2f);

            knob.lowValue = 0.5f;

            Assert.AreEqual(0.5f, knob.value,
                "lowValue を上げると現在値もそこまで持ち上げられる");
        }

        [Test]
        public void ChangingHighValue_ClampsCurrentValue()
        {
            var knob = new RadialKnobElement { lowValue = 0f, highValue = 1f };
            knob.SetValueWithoutNotify(0.9f);

            knob.highValue = 0.6f;

            Assert.AreEqual(0.6f, knob.value,
                "highValue を下げると現在値もそこまで下げられる");
        }

        [Test]
        public void NonStandardRange_IsRespected()
        {
            var knob = new RadialKnobElement { lowValue = -10f, highValue = 10f };
            knob.SetValueWithoutNotify(7.5f);
            Assert.AreEqual(7.5f, knob.value);

            knob.value = -25f;
            Assert.AreEqual(-10f, knob.value, "負の範囲でも下限クランプされる");
        }
    }
}
