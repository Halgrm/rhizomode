#nullable enable

using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// ミラー出力のRenderTextureをNDIプロトコルでネットワーク経由で外部アプリに送信する。
    /// KlakNDIパッケージが未インストールの場合はコンパイルから除外される。
    /// </summary>
    public class NdiSenderController : MonoBehaviour
    {
        [SerializeField] private string senderName = "rhizomode";

#if KLAK_NDI
        // 明示割り当て用 (任意)。null の場合は実行時に自動解決する。
        // Klak.NDI は NdiSender の `_resources` を MonoScript の defaultReferences で配線するため、
        // 実行時 AddComponent 経由では null のまま残り FormatConverter.Encode が NRE する。
        [SerializeField] private Klak.Ndi.NdiResources? ndiResources;
#endif

        private bool _isActive;

#if KLAK_NDI
        private Klak.Ndi.NdiSender? _sender;
#endif

        /// <summary>
        /// 送信元テクスチャを設定して送信を開始する。
        /// </summary>
        public void StartSending(RenderTexture source)
        {
            // 二重起動防止: 既にアクティブなら先に停止する
            if (_isActive) StopSending();

#if KLAK_NDI
            try
            {
                var resources = ResolveResources();
                if (resources == null)
                {
                    Debug.LogWarning("[NdiSender] NdiResources asset not found. NDI output disabled.");
                    return;
                }

                _sender = gameObject.AddComponent<Klak.Ndi.NdiSender>();
                // CaptureCoroutine 初回ティック前に必ず _resources を埋める。
                _sender.SetResources(resources);
                _sender.captureMethod = Klak.Ndi.CaptureMethod.Texture;
                _sender.sourceTexture = source;
                _sender.ndiName = senderName;
                _isActive = true;
                Debug.Log($"[NdiSender] Sending as '{senderName}'");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NdiSender] Failed to start: {ex.Message}");
            }
#else
            Debug.LogWarning("[NdiSender] KlakNDI package not installed. NDI output disabled.");
#endif
        }

#if KLAK_NDI
        private Klak.Ndi.NdiResources? ResolveResources()
        {
            if (ndiResources != null) return ndiResources;

            var loaded = Resources.FindObjectsOfTypeAll<Klak.Ndi.NdiResources>();
            if (loaded.Length > 0)
            {
                ndiResources = loaded[0];
                return ndiResources;
            }

#if UNITY_EDITOR
            // パッケージ同梱の NdiResources.asset を GUID 経由で復元する。
            const string ndiResourcesGuid = "69304b86950074db7ba8caba75214004";
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(ndiResourcesGuid);
            if (!string.IsNullOrEmpty(path))
            {
                ndiResources = UnityEditor.AssetDatabase.LoadAssetAtPath<Klak.Ndi.NdiResources>(path);
                return ndiResources;
            }
#endif
            return null;
        }
#endif

        /// <summary>
        /// 送信を停止する。
        /// </summary>
        public void StopSending()
        {
#if KLAK_NDI
            try
            {
                if (_sender != null)
                {
                    Destroy(_sender);
                    _sender = null;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NdiSender] Failed to stop: {ex.Message}");
            }
#endif
            _isActive = false;
        }

        private void OnDestroy()
        {
            if (_isActive)
                StopSending();
        }
    }
}
