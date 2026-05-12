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
                _sender = gameObject.AddComponent<Klak.Ndi.NdiSender>();
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
