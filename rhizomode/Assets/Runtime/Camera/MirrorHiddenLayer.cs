#nullable enable

using UnityEngine;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// VR HMD には見せるが Mirror カメラ (Spout/NDI/Desktop 配信先) には隠したい
    /// 系の GameObject を分離する専用 Layer ("MirrorHidden") の管理ヘルパー。
    /// MirrorOutputController.SetUIVisible(false) で本 Layer の bit を cullingMask から落とす。
    /// TagManager.asset の index 8 に登録されている前提。
    /// </summary>
    /// <remarks>
    /// Phase 1 (2026-05-19): "PerformerUI" → "MirrorHidden" rename。layer index 8 は不変なので
    /// シーン上の GameObject の保存値 (m_Layer: 8) は影響を受けない。
    /// Phase 2 で本クラスは <c>Rhizomode.Presentation.Layering</c> asmdef に移送予定。
    /// </remarks>
    public static class MirrorHiddenLayer
    {
        public const string LayerName = "MirrorHidden";

        // -2 = uninitialized, -1 = not found (TagManager 未設定時)。
        // SceneManager をまたいでも layer 番号は不変なので一度引いたら使い回す。
        private static int _cachedLayer = -2;

        public static int Layer
        {
            get
            {
                if (_cachedLayer == -2)
                    _cachedLayer = LayerMask.NameToLayer(LayerName);
                return _cachedLayer;
            }
        }

        public static int LayerMaskBit
        {
            get
            {
                var layer = Layer;
                return layer >= 0 ? (1 << layer) : 0;
            }
        }

        /// <summary>
        /// <paramref name="root"/> と全子孫 GameObject の layer を MirrorHidden に設定する。
        /// Layer 未登録 (-1) のときは no-op。
        /// </summary>
        public static void ApplyRecursive(GameObject? root)
        {
            if (root == null) return;
            var layer = Layer;
            if (layer < 0) return;
            SetLayerRecursive(root.transform, layer);
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t)
                SetLayerRecursive(child, layer);
        }
    }
}
