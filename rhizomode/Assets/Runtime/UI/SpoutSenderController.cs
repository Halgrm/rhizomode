#nullable enable

using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// ミラー出力のRenderTextureをSpoutプロトコルで外部アプリ（Resolume、OBS等）に送信する��
    /// KlakSpoutパッケージが未インストールの場合はコンパイルから除外される。
    /// </summary>
    public class SpoutSenderController : MonoBehaviour
    {
        [SerializeField] private string senderName = "rhizomode";

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
                _sender = gameObject.AddComponent<Klak.Spout.SpoutSender>();
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
