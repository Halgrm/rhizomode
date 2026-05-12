#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// 円形ノブ風 VisualElement。Slider と同じ ChangeEvent&lt;float&gt; API を持ち、
    /// 垂直ドラッグで値を変える (Ableton/Live のノブ操作感)。
    /// 外周は border-radius:50% の単純な円。中央のインジケータが回転して値を示す。
    /// </summary>
    public class RadialKnobElement : VisualElement
    {
        // UXML から &lt;Rhizomode.UI.RadialKnobElement /&gt; として配置可能にする
        public new class UxmlFactory : UxmlFactory<RadialKnobElement, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlFloatAttributeDescription _low =
                new() { name = "low-value", defaultValue = 0f };
            private readonly UxmlFloatAttributeDescription _high =
                new() { name = "high-value", defaultValue = 1f };
            private readonly UxmlFloatAttributeDescription _value =
                new() { name = "value", defaultValue = 0f };
            private readonly UxmlFloatAttributeDescription _dragRange =
                new() { name = "drag-range-px", defaultValue = DefaultDragRangePx };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var k = (RadialKnobElement)ve;
                k.lowValue = _low.GetValueFromBag(bag, cc);
                k.highValue = _high.GetValueFromBag(bag, cc);
                k.dragRangePx = _dragRange.GetValueFromBag(bag, cc);
                k.SetValueWithoutNotify(_value.GetValueFromBag(bag, cc));
            }
        }

        private const float MinAngleDeg = -135f;     // 値=lowのときのインジケータ角度
        private const float MaxAngleDeg = 135f;      // 値=highのときのインジケータ角度
        private const float DefaultDragRangePx = 200f;

        private readonly VisualElement _indicatorHost;

        private float _lowValue;
        private float _highValue = 1f;
        private float _value;
        private float _dragRangePx = DefaultDragRangePx;

        private bool _isDragging;
        private Vector2 _dragStartPanelPos;
        private float _dragStartValue;

        /// <summary>値範囲の下限。</summary>
        public float lowValue
        {
            get => _lowValue;
            set
            {
                if (Mathf.Approximately(_lowValue, value)) return;
                _lowValue = value;
                ClampValueIntoRange();
                RefreshIndicator();
            }
        }

        /// <summary>値範囲の上限。</summary>
        public float highValue
        {
            get => _highValue;
            set
            {
                if (Mathf.Approximately(_highValue, value)) return;
                _highValue = value;
                ClampValueIntoRange();
                RefreshIndicator();
            }
        }

        /// <summary>現在値。setterは ChangeEvent を発火する。発火させたくないときは <see cref="SetValueWithoutNotify"/>。</summary>
        public float value
        {
            get => _value;
            set => SetValueInternal(value, notify: true);
        }

        /// <summary>フルレンジを動かすのに必要な垂直ドラッグ距離 (px)。大きいほど繊細。</summary>
        public float dragRangePx
        {
            get => _dragRangePx;
            set => _dragRangePx = Mathf.Max(10f, value);
        }

        public RadialKnobElement()
        {
            AddToClassList("rzm-knob");
            focusable = true;
            pickingMode = PickingMode.Position;

            // インジケータホスト: ノブと同じ寸法を絶対配置で重ね、回転で針の向きを示す
            _indicatorHost = new VisualElement { name = "indicator-host" };
            _indicatorHost.AddToClassList("rzm-knob__indicator-host");
            _indicatorHost.pickingMode = PickingMode.Ignore;

            var needle = new VisualElement { name = "needle" };
            needle.AddToClassList("rzm-knob__needle");
            needle.pickingMode = PickingMode.Ignore;
            _indicatorHost.Add(needle);

            Add(_indicatorHost);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);

            RefreshIndicator();
        }

        /// <summary>ChangeEvent を発火させずに値を設定する (外部から状態反映する用)。</summary>
        public void SetValueWithoutNotify(float v)
        {
            SetValueInternal(v, notify: false);
        }

        private void SetValueInternal(float v, bool notify)
        {
            var clamped = Mathf.Clamp(v, _lowValue, _highValue);
            if (Mathf.Approximately(clamped, _value)) return;

            var prev = _value;
            _value = clamped;
            RefreshIndicator();

            if (notify)
            {
                using var changeEvt = ChangeEvent<float>.GetPooled(prev, _value);
                changeEvt.target = this;
                SendEvent(changeEvt);
            }
        }

        private void ClampValueIntoRange()
        {
            var clamped = Mathf.Clamp(_value, _lowValue, _highValue);
            if (!Mathf.Approximately(clamped, _value))
                _value = clamped;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            _isDragging = true;
            _dragStartPanelPos = (Vector2)evt.position;
            _dragStartValue = _value;
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging) return;

            // 上方向のドラッグ = 値増加 (Ableton と同じ操作感)
            var dy = _dragStartPanelPos.y - evt.position.y;
            var range = _highValue - _lowValue;
            var deltaValue = (dy / Mathf.Max(_dragRangePx, 1f)) * range;
            SetValueInternal(_dragStartValue + deltaValue, notify: true);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging) return;
            _isDragging = false;
            if (this.HasPointerCapture(evt.pointerId))
                this.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            // キャプチャが外部解除された場合もドラッグ終了
            _isDragging = false;
        }

        private void RefreshIndicator()
        {
            var range = _highValue - _lowValue;
            var t = range > 0.0001f ? (_value - _lowValue) / range : 0f;
            t = Mathf.Clamp01(t);
            var angle = Mathf.Lerp(MinAngleDeg, MaxAngleDeg, t);
            _indicatorHost.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));
        }
    }
}
