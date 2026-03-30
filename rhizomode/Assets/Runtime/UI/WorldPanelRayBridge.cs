#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// 外部レイキャストのヒット情報をUIToolkitパネルのポインターイベントに変換する。
    /// WorldPanelHostと組み合わせて使用。XR層から呼び出される。
    /// </summary>
    [RequireComponent(typeof(WorldPanelHost))]
    public class WorldPanelRayBridge : MonoBehaviour
    {
        private WorldPanelHost? _panelHost;
        private bool _isHovering;
        private Vector2 _lastPanelPosition;

        /// <summary>現在ホバー中かどうか。</summary>
        public bool IsHovering => _isHovering;

        private void Awake()
        {
            _panelHost = GetComponent<WorldPanelHost>();
        }

        /// <summary>
        /// レイがパネルに当たった時に呼ぶ。ポインター移動イベントを注入する。
        /// </summary>
        public void NotifyHover(RaycastHit hit)
        {
            if (_panelHost?.Root == null) return;

            _lastPanelPosition = _panelHost.RayHitToPanelPosition(hit);
            _isHovering = true;

            SendPointerEvent<PointerMoveEvent>(_lastPanelPosition);
        }

        /// <summary>
        /// レイがパネルから離れた時に呼ぶ。
        /// </summary>
        public void NotifyHoverExit()
        {
            if (!_isHovering) return;
            _isHovering = false;

            SendPointerEvent<PointerLeaveEvent>(_lastPanelPosition);
        }

        /// <summary>
        /// パネル上でトリガーが押された時に呼ぶ。
        /// </summary>
        public void NotifyPointerDown(RaycastHit hit)
        {
            if (_panelHost?.Root == null) return;

            _lastPanelPosition = _panelHost.RayHitToPanelPosition(hit);
            SendPointerEvent<PointerDownEvent>(_lastPanelPosition);
        }

        /// <summary>
        /// パネル上でトリガーが離された時に呼ぶ。
        /// </summary>
        public void NotifyPointerUp()
        {
            if (_panelHost?.Root == null) return;

            SendPointerEvent<PointerUpEvent>(_lastPanelPosition);
        }

        private void SendPointerEvent<T>(Vector2 panelPosition) where T : PointerEventBase<T>, new()
        {
            var root = _panelHost?.Root;
            if (root?.panel == null) return;

            try
            {
                using var evt = PointerEventBase<T>.GetPooled();
                evt.target = root;
                // パネルローカル座標を設定
                // UIToolkitのイベントシステムに座標を伝える
                root.panel.visualTree.SendEvent(evt);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WorldPanelRayBridge] Event dispatch failed: {e.Message}");
            }
        }
    }
}
