#nullable enable

using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// ミラー出力のRenderTextureをSpoutプロトコルで外部アプリ（Resolume、OBS等）に送信する��
    /// KlakSpoutパッケージが未インストールの場合はコンパイルから除外される。
    /// </summary>
    public class SpoutSenderController : MonoBehaviour
    {
        [SerializeField] private string senderName = "rhizomode";

#if KLAK_SPOUT
        // 明示割り当て用 (任意)。null の場合は実行時に自動解決する。
        // Klak.Spout は SpoutSender の `_resources` を MonoScript の defaultReferences で配線するため、
        // 実行時 AddComponent 経由では null のまま残り Blitter.GetMaterial が NRE する。
        [SerializeField] private Klak.Spout.SpoutResources? spoutResources;
#endif

        private bool _isActive;

#if KLAK_SPOUT
        private Klak.Spout.SpoutSender? _sender;
#endif

        /// <summary>
        /// 送信元テクスチャを設定して送信を開始する。
        /// </summary>
        public void StartSending(RenderTexture source)
        {
            // 二重起動防止: 既にアクティブなら先に停止する
            if (_isActive) StopSending();

#if KLAK_SPOUT
            try
            {
                var resources = ResolveResources();
                if (resources == null)
                {
                    Debug.LogWarning("[SpoutSender] SpoutResources asset not found. Spout output disabled.");
                    return;
                }

                _sender = gameObject.AddComponent<Klak.Spout.SpoutSender>();
                // CaptureCoroutine 初回ティック前に必ず _resources を埋める。
                _sender.SetResources(resources);
                _sender.captureMethod = Klak.Spout.CaptureMethod.Texture;
                _sender.sourceTexture = source;
                _sender.spoutName = senderName;
                _isActive = true;
                Debug.Log($"[SpoutSender] Sending as '{senderName}'");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SpoutSender] Failed to start: {ex.Message}");
            }
#else
            Debug.LogWarning("[SpoutSender] KlakSpout package not installed. Spout output disabled.");
#endif
        }

#if KLAK_SPOUT
        private Klak.Spout.SpoutResources? ResolveResources()
        {
            if (spoutResources != null) return spoutResources;

            var loaded = Resources.FindObjectsOfTypeAll<Klak.Spout.SpoutResources>();
            if (loaded.Length > 0)
            {
                spoutResources = loaded[0];
                return spoutResources;
            }

#if UNITY_EDITOR
            // パッケージ同梱の SpoutResources.asset を GUID 経由で復元する。
            const string spoutResourcesGuid = "f449ebbe2051c2e4d993eaa773a410de";
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(spoutResourcesGuid);
            if (!string.IsNullOrEmpty(path))
            {
                spoutResources = UnityEditor.AssetDatabase.LoadAssetAtPath<Klak.Spout.SpoutResources>(path);
                return spoutResources;
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
#if KLAK_SPOUT
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
                Debug.LogWarning($"[SpoutSender] Failed to stop: {ex.Message}");
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
