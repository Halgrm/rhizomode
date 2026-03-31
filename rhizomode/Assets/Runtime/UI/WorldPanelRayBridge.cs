#nullable enable

using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// 外部レイキャストのヒット情報をUIToolkitパネルの操作に変換する。
    /// WorldPanelHostと組み合わせて使用。XR層のUIRaycastDriverから呼び出される。
    /// Panel.Pick()でヒット要素を特定し、PointerDown/Upをリフレクション経由で注入する。
    /// </summary>
    [RequireComponent(typeof(WorldPanelHost))]
    public class WorldPanelRayBridge : MonoBehaviour
    {
        private const string HoverClass = "hover";

        private static readonly BindingFlags SetterFlags =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private WorldPanelHost? _panelHost;
        private bool _isHovering;
        private Vector2 _lastPanelPosition;
        private VisualElement? _hoveredElement;
        private bool _isPointerDown;

        /// <summary>現在ホバー中かどうか。</summary>
        public bool IsHovering => _isHovering;

        private void Awake()
        {
            _panelHost = GetComponent<WorldPanelHost>();
        }

        /// <summary>
        /// レイがパネルに当たった時に呼ぶ。ホバー対象を更新する。
        /// </summary>
        public void NotifyHover(RaycastHit hit)
        {
            if (_panelHost?.Root == null) return;

            _lastPanelPosition = _panelHost.RayHitToPanelPosition(hit);
            _isHovering = true;

            var picked = _panelHost.Root.panel?.Pick(_lastPanelPosition);
            UpdateHoveredElement(picked);
        }

        /// <summary>
        /// レイがパネルから離れた時に呼ぶ。
        /// </summary>
        public void NotifyHoverExit()
        {
            if (!_isHovering) return;
            _isHovering = false;
            UpdateHoveredElement(null);
        }

        /// <summary>
        /// パネル上でトリガーが押された時に呼ぶ。
        /// </summary>
        public void NotifyPointerDown(RaycastHit hit)
        {
            if (_panelHost?.Root == null) return;

            _lastPanelPosition = _panelHost.RayHitToPanelPosition(hit);
            _isPointerDown = true;

            var picked = _panelHost.Root.panel?.Pick(_lastPanelPosition);
            var target = picked ?? _panelHost.Root;

            SendPointerEventWithReflection<PointerDownEvent>(target, _lastPanelPosition);
        }

        /// <summary>
        /// パネル上でトリガーが離された時に呼ぶ。
        /// </summary>
        public void NotifyPointerUp()
        {
            if (_panelHost?.Root == null || !_isPointerDown) return;
            _isPointerDown = false;

            var picked = _panelHost.Root.panel?.Pick(_lastPanelPosition);
            var target = picked ?? _panelHost.Root;

            SendPointerEventWithReflection<PointerUpEvent>(target, _lastPanelPosition);
        }

        private void SendPointerEventWithReflection<T>(VisualElement target, Vector2 panelPos)
            where T : PointerEventBase<T>, new()
        {
            try
            {
                using var evt = PointerEventBase<T>.GetPooled();
                var screenPos = new Vector3(panelPos.x, panelPos.y, 0f);

                // UIToolkit PointerEventBaseのsetterはprotected。リフレクションで設定。
                var baseType = typeof(PointerEventBase<T>);
                SetProperty(baseType, evt, "pointerId", 0);
                SetProperty(baseType, evt, "position", screenPos);
                SetProperty(baseType, evt, "localPosition", screenPos);
                SetProperty(baseType, evt, "button", 0);
                SetProperty(baseType, evt, "pressedButtons", 1);

                evt.target = target;
                _panelHost!.Root!.panel!.visualTree.SendEvent(evt);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WorldPanelRayBridge] Event dispatch failed: {e.Message}");
            }
        }

        private static void SetProperty<TEvt>(Type baseType, TEvt evt, string propName, object value)
        {
            baseType.GetProperty(propName, SetterFlags)?.SetValue(evt, value);
        }

        private void UpdateHoveredElement(VisualElement? newElement)
        {
            if (_hoveredElement == newElement) return;

            _hoveredElement?.RemoveFromClassList(HoverClass);
            _hoveredElement = newElement;
            _hoveredElement?.AddToClassList(HoverClass);
        }
    }
}
