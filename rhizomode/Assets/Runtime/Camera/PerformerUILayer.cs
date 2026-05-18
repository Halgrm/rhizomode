#nullable enable

using UnityEngine;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// VR HMD には見せるが Mirror カメラ (Spout/NDI/Desktop 配信先) には隠したい
    /// UI 系 GameObject を分離する専用 Layer ("PerformerUI") の管理ヘルパー。
    /// MirrorOutputController.SetUIVisible(false) で本 Layer の bit を cullingMask から落とす。
    /// TagManager.asset の index 8 に登録されている前提。
    /// </summary>
    public static class PerformerUILayer
    {
        public const string LayerName = "PerformerUI";

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
        /// <paramref name="root"/> と全子孫 GameObject の layer を PerformerUI に設定する。
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
