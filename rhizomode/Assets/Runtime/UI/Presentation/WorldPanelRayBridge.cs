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

        /// <summary>デフォルト pointer id (呼び出し元が指定しない場合)。</summary>
        public const int DefaultPointerId = 0;

        private static readonly BindingFlags SetterFlags =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static readonly Dictionary<(Type, string), PropertyInfo?> PropertyCache = new();

        /// <summary>
        /// pointer ごとの down/captured/hover 状態。複数 controller (左右手) が同一パネルに伸びても
        /// 状態が混ざらないように pointerId 別に保持する (P3 fix, 2026-05-16)。
        /// </summary>
        private sealed class PointerState
        {
            public bool IsDown;
            public bool IsHovering;
            public VisualElement? CapturedTarget;
            public Vector2 LastPosition;
        }

        private readonly Dictionary<int, PointerState> _pointers = new();

        private WorldPanelHost? _panelHost;
        private VisualElement? _hoveredElement;

        /// <summary>現在いずれかの pointer がホバー中かどうか。</summary>
        public bool IsHovering
        {
            get
            {
                foreach (var p in _pointers.Values)
                    if (p.IsHovering) return true;
                return false;
            }
        }

        private void Awake()
        {
            _panelHost = GetComponent<WorldPanelHost>();
        }

        private PointerState GetOrCreatePointer(int pointerId)
        {
            if (!_pointers.TryGetValue(pointerId, out var p))
            {
                p = new PointerState();
                _pointers[pointerId] = p;
            }
            return p;
        }

        /// <summary>指定 pointer が現在 down 状態かを問い合わせる (UIRaycastDriver の hover-out 対応用)。</summary>
        public bool IsPointerDown(int pointerId = DefaultPointerId) =>
            _pointers.TryGetValue(pointerId, out var p) && p.IsDown;

        /// <summary>
        /// レイがパネルに当たった時に呼ぶ。ホバー対象を更新する。
        /// PointerDown中はPointerMoveを送信（スライダー等のドラッグ操作対応）。
        /// </summary>
        public void NotifyHover(RaycastHit hit, int pointerId = DefaultPointerId)
        {
            if (_panelHost?.Root == null) return;
            var state = GetOrCreatePointer(pointerId);

            var newPos = _panelHost.RayHitToPanelPosition(hit);
            var posChanged = Vector2.Distance(newPos, state.LastPosition) > 0.5f;
            state.LastPosition = newPos;
            state.IsHovering = true;

            var picked = _panelHost.Root.panel?.Pick(state.LastPosition);
            UpdateHoveredElement(picked);

            if (state.IsDown && posChanged)
            {
                var target = state.CapturedTarget ?? picked ?? _panelHost.Root;
                SendPointerEvent<PointerMoveEvent>(target, state.LastPosition, pointerId);
            }
        }

        /// <summary>
        /// レイがパネルから離れた時に呼ぶ。
        /// </summary>
        /// <remarks>
        /// hover 解除のみ実行。pointer が down のまま hover 外に出た場合は呼び出し側
        /// (<c>UIRaycastDriver</c>) で <see cref="ForcePointerUp"/> を先に呼ぶこと。
        /// </remarks>
        public void NotifyHoverExit(int pointerId = DefaultPointerId)
        {
            if (_panelHost?.Root == null) return;
            if (!_pointers.TryGetValue(pointerId, out var state)) return;
            state.IsHovering = false;

            // 全 pointer が hover 外なら hover element を解除
            if (!IsHovering)
                UpdateHoveredElement(null);
        }

        /// <summary>
        /// パネル上でトリガーが押された時に呼ぶ。
        /// </summary>
        public void NotifyPointerDown(RaycastHit hit, int pointerId = DefaultPointerId)
        {
            if (_panelHost?.Root == null) return;
            var state = GetOrCreatePointer(pointerId);

            state.LastPosition = _panelHost.RayHitToPanelPosition(hit);
            state.IsDown = true;

            var picked = _panelHost.Root.panel?.Pick(state.LastPosition);
            var target = picked ?? _panelHost.Root;
            state.CapturedTarget = target;

            SendPointerEvent<PointerDownEvent>(target, state.LastPosition, pointerId);
        }

        /// <summary>
        /// パネル上でトリガーが離された時に呼ぶ。
        /// </summary>
        public void NotifyPointerUp(int pointerId = DefaultPointerId)
        {
            if (_panelHost?.Root == null) return;
            if (!_pointers.TryGetValue(pointerId, out var state) || !state.IsDown) return;
            state.IsDown = false;

            var picked = _panelHost.Root.panel?.Pick(state.LastPosition);
            var target = state.CapturedTarget ?? picked ?? _panelHost.Root;
            state.CapturedTarget = null;

            SendPointerEvent<PointerUpEvent>(target, state.LastPosition, pointerId);
        }

        /// <summary>
        /// pointer が down 状態のまま hover を抜ける際の防御。down なら PointerUp を強制発火する
        /// (P3 fix: スライダー操作中に ray が外れて UI が「掴まれっぱなし」になるバグの対策)。
        /// </summary>
        public void ForcePointerUp(int pointerId = DefaultPointerId)
        {
            if (!_pointers.TryGetValue(pointerId, out var state) || !state.IsDown) return;
            NotifyPointerUp(pointerId);
        }

        private void SendPointerEvent<T>(VisualElement target, Vector2 panelPos, int pointerId)
            where T : PointerEventBase<T>, new()
        {
            try
            {
                using var evt = PointerEventBase<T>.GetPooled();
                var screenPos = new Vector3(panelPos.x, panelPos.y, 0f);

                var baseType = typeof(PointerEventBase<T>);
                SetCachedProperty(baseType, evt, "pointerId", pointerId);
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
