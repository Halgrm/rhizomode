#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// 外部レイキャストのヒット情報をUIToolkitパネルの操作に変換する。
    /// WorldPanelHostと組み合わせて使用。XR層のUIRaycastDriverから呼び出される。
    /// Panel.Pick()でヒット要素を特定し、PointerDown/Up/Moveをリフレクション経由で注入する。
    /// </summary>
    [RequireComponent(typeof(WorldPanelHost))]
    public class WorldPanelRayBridge : MonoBehaviour
    {
        private const string HoverClass = "hover";

        private static readonly BindingFlags SetterFlags =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static readonly Dictionary<(Type, string), PropertyInfo?> PropertyCache = new();

        private WorldPanelHost? _panelHost;
        private bool _isHovering;
        private Vector2 _lastPanelPosition;
        private VisualElement? _hoveredElement;
        private bool _isPointerDown;
        // PointerDown 時の対象を保持し、ドラッグ中はピックし直さず同じ要素にイベントを送る
        // (Knob のような小さい要素から外側に出ても操作を継続するため)
        private VisualElement? _capturedTarget;

        /// <summary>現在ホバー中かどうか。</summary>
        public bool IsHovering => _isHovering;

        private void Awake()
        {
            _panelHost = GetComponent<WorldPanelHost>();
        }

        /// <summary>
        /// レイがパネルに当たった時に呼ぶ。ホバー対象を更新する。
        /// PointerDown中はPointerMoveを送信（スライダー等のドラッグ操作対応）。
        /// </summary>
        public void NotifyHover(RaycastHit hit)
        {
            if (_panelHost?.Root == null) return;

            var newPos = _panelHost.RayHitToPanelPosition(hit);
            var posChanged = Vector2.Distance(newPos, _lastPanelPosition) > 0.5f;
            _lastPanelPosition = newPos;
            _isHovering = true;

            var picked = _panelHost.Root.panel?.Pick(_lastPanelPosition);
            UpdateHoveredElement(picked);

            if (_isPointerDown && posChanged)
            {
                // ドラッグ中はキャプチャ対象に送る (なければピック先 → root)
                var target = _capturedTarget ?? picked ?? _panelHost.Root;
                SendPointerEvent<PointerMoveEvent>(target, _lastPanelPosition);
            }
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
            _capturedTarget = target;

            SendPointerEvent<PointerDownEvent>(target, _lastPanelPosition);
        }

        /// <summary>
        /// パネル上でトリガーが離された時に呼ぶ。
        /// </summary>
        public void NotifyPointerUp()
        {
            if (_panelHost?.Root == null || !_isPointerDown) return;
            _isPointerDown = false;

            var picked = _panelHost.Root.panel?.Pick(_lastPanelPosition);
            var target = _capturedTarget ?? picked ?? _panelHost.Root;
            _capturedTarget = null;

            SendPointerEvent<PointerUpEvent>(target, _lastPanelPosition);
        }

        private void SendPointerEvent<T>(VisualElement target, Vector2 panelPos)
            where T : PointerEventBase<T>, new()
        {
            try
            {
                using var evt = PointerEventBase<T>.GetPooled();
                var screenPos = new Vector3(panelPos.x, panelPos.y, 0f);

                var baseType = typeof(PointerEventBase<T>);
                SetCachedProperty(baseType, evt, "pointerId", 0);
                SetCachedProperty(baseType, evt, "position", screenPos);
                SetCachedProperty(baseType, evt, "localPosition", screenPos);
                SetCachedProperty(baseType, evt, "button", 0);
                SetCachedProperty(baseType, evt, "pressedButtons", 1);

                evt.target = target;
                _panelHost!.Root!.panel!.visualTree.SendEvent(evt);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WorldPanelRayBridge] Event dispatch failed: {e.Message}");
            }
        }

        private static void SetCachedProperty<TEvt>(Type baseType, TEvt evt, string propName, object value)
        {
            var key = (baseType, propName);
            if (!PropertyCache.TryGetValue(key, out var prop))
            {
                prop = baseType.GetProperty(propName, SetterFlags);
                PropertyCache[key] = prop;
            }
            prop?.SetValue(evt, value);
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
