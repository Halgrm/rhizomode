#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace Rhizomode.UI
{
    /// <summary>
    /// MirrorOutput の RenderTexture を Screen Space Overlay の RawImage に表示し、
    /// デスクトップ (Display 0 / Game ビュー) で確認できるようにする。
    /// Overlay は HMD には描画されないため VR 視界は汚さない。XR ミラー (片眼) より上に重なる。
    /// </summary>
    public class DesktopMirrorBlitter : MonoBehaviour
    {
        // XR ミラーやその他 UI より確実に上に来るオーダー。
        private const int OverlaySortingOrder = 9999;

        [SerializeField] private RenderTexture? source;
        [SerializeField] private int sortingOrder = OverlaySortingOrder;

        private Canvas? _canvas;
        private RawImage? _rawImage;

        /// <summary>
        /// 転写元 RenderTexture を設定する。GameBootstrap から MirrorOutput.OutputTexture を渡す。
        /// </summary>
        public void SetSource(RenderTexture rt)
        {
            source = rt;
            if (_rawImage != null) _rawImage.texture = rt;
        }

        /// <summary>
        /// オーバーレイの表示/非表示を切り替える。
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.enabled = visible;
        }

        private void Awake()
        {
            CreateOverlay();
            if (source != null && _rawImage != null) _rawImage.texture = source;
        }

        private void CreateOverlay()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = sortingOrder;
            // XR ミラー上に表示するため OverrideSorting 不要 (ScreenSpaceOverlay は常に最上位)

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            // GraphicRaycaster はマウス操作を奪うので付けない

            var imgGo = new GameObject("MirrorImage", typeof(RectTransform));
            imgGo.transform.SetParent(transform, false);

            var rect = (RectTransform)imgGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _rawImage = imgGo.AddComponent<RawImage>();
            _rawImage.raycastTarget = false;
        }
    }
}
