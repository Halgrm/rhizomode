#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// 外部レイキャストのヒット情報をUIToolkitパネルの操作に変換する。
    /// WorldPanelHostと組み合わせて使用。XR層のUIRaycastDriverから呼び出される。
    /// Panel.Pick()でヒット要素を特定し、ボタンクリック等を直接実行する。
    /// </summary>
    [RequireComponent(typeof(WorldPanelHost))]
    public class WorldPanelRayBridge : MonoBehaviour
    {
        private const string HoverClass = "hover";

        private WorldPanelHost? _panelHost;
        private bool _isHovering;
        private Vector2 _lastPanelPosition;
        private VisualElement? _hoveredElement;

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
        /// PickされたButton要素があればクリックイベントを発火する。
        /// </summary>
        public void NotifyPointerDown(RaycastHit hit)
        {
            if (_panelHost?.Root == null) return;

            _lastPanelPosition = _panelHost.RayHitToPanelPosition(hit);
            var picked = _panelHost.Root.panel?.Pick(_lastPanelPosition);

            // Button要素を探して直接クリック
            var button = FindParentButton(picked);
            if (button != null)
            {
                try
                {
                    using var clickEvt = ClickEvent.GetPooled();
                    clickEvt.target = button;
                    button.SendEvent(clickEvt);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[WorldPanelRayBridge] Click dispatch failed: {e.Message}");
                }
            }
        }

        /// <summary>
        /// パネル上でトリガーが離された時に呼ぶ。
        /// </summary>
        public void NotifyPointerUp()
        {
            // ClickEventはPointerDownで発火済み
        }

        private void UpdateHoveredElement(VisualElement? newElement)
        {
            if (_hoveredElement == newElement) return;

            _hoveredElement?.RemoveFromClassList(HoverClass);
            _hoveredElement = newElement;
            _hoveredElement?.AddToClassList(HoverClass);
        }

        private static Button? FindParentButton(VisualElement? element)
        {
            var current = element;
            while (current != null)
            {
                if (current is Button button)
                    return button;
                current = current.parent;
            }
            return null;
        }
    }
}
