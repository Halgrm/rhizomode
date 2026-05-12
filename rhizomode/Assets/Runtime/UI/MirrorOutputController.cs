#nullable enable

using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Rhizomode.UI
{
    /// <summary>
    /// デスクトップ/Spout/NDI 出力用ミラーカメラの管理。
    /// RenderTexture (1920x1080) に出力し、Spout/NDI 送信に利用する。
    /// 位置・回転制御は Cinemachine Brain に委譲する（同 GameObject に Brain を追加すること）。
    /// </summary>
    public class MirrorOutputController : MonoBehaviour
    {
        private const int OutputWidth = 1920;
        private const int OutputHeight = 1080;
        private const int OutputDepth = 0;
        private const RenderTextureFormat OutputFormat = RenderTextureFormat.ARGB32;

        [Header("Camera")]
        [SerializeField] private Camera? mirrorCamera;

        private RenderTexture? _outputTexture;

        /// <summary>ミラー出力用のRenderTexture。Activate後に有効。</summary>
        public RenderTexture? OutputTexture => _outputTexture;

        /// <summary>ミラー出力が有効かどうか。</summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// 初期化（後方互換のため引数は温存）。
        /// 位置・回転は Cinemachine Brain が制御するためここでは触らない。
        /// </summary>
        /// <param name="headTransform">VR HMDのTransform（未使用）。</param>
        public void Initialize(Transform headTransform)
        {
            _ = headTransform;
        }

        /// <summary>
        /// ミラー出力を有効化する。RenderTextureを生成しカメラに割り当てる。
        /// </summary>
        public void Activate()
        {
            if (IsActive) return;
            if (mirrorCamera == null)
            {
                Debug.LogError("[MirrorOutput] mirrorCamera が未設定です");
                return;
            }

            _outputTexture = new RenderTexture(OutputWidth, OutputHeight, OutputDepth, OutputFormat)
            {
                name = "MirrorOutput_RT"
            };
            _outputTexture.Create();

            mirrorCamera.targetTexture = _outputTexture;
            mirrorCamera.enabled = true;
            ConfigureUrpCamera(mirrorCamera);

            IsActive = true;
        }

        /// <summary>
        /// ミラー出力を無効化する。カメラを停止しRenderTextureを解放する。
        /// </summary>
        public void Deactivate()
        {
            if (!IsActive) return;

            if (mirrorCamera != null)
            {
                mirrorCamera.enabled = false;
                mirrorCamera.targetTexture = null;
            }

            ReleaseOutputTexture();
            IsActive = false;
        }

        private void OnDestroy()
        {
            Deactivate();
        }

        /// <summary>
        /// URP用カメラ設定。Overlay不要のスタンドアロンBaseカメラとして構成。
        /// </summary>
        private static void ConfigureUrpCamera(Camera camera)
        {
            var urpData = camera.GetUniversalAdditionalCameraData();
            if (urpData == null) return;

            // VRメインカメラとは独立したBaseカメラとしてレンダリング
            urpData.renderType = CameraRenderType.Base;
        }

        private void ReleaseOutputTexture()
        {
            if (_outputTexture == null) return;

            _outputTexture.Release();
            Destroy(_outputTexture);
            _outputTexture = null;
        }
    }
}
