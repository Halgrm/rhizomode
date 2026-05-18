#nullable enable

using UnityEngine;
using UnityEngine.Rendering.Universal;

using Rhizomode.Cameras;
using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

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
        // Unity 6 URP render graph requires camera target RT to have non-None depth stencil.
        // 24-bit depth + 8-bit stencil で SRP の depth-stencil 要求を満たす。
        private const int OutputDepth = 24;
        private const RenderTextureFormat OutputFormat = RenderTextureFormat.ARGB32;

        [Header("Camera")]
        [SerializeField] private Camera? mirrorCamera;

        private RenderTexture? _outputTexture;

        // 起動時 (Activate) に最初に観測した cullingMask。
        // SetUIVisible(true) で復元するための baseline。Inspector で UI layer を含めて
        // 設定してあっても、起動直後に自動で UI を除外できるよう Activate 時に
        // PerformerUI bit を落とす。
        private int _baseCullingMask;

        /// <summary>ミラー出力用のRenderTexture。Activate後に有効。</summary>
        public RenderTexture? OutputTexture => _outputTexture;

        /// <summary>ミラー出力が有効かどうか。</summary>
        public bool IsActive { get; private set; }

        /// <summary>Mirror 出力に UI (PerformerUI layer) を含めるか。default は false (= 配信に UI を出さない)。</summary>
        public bool IsUIVisible { get; private set; }

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

            _baseCullingMask = mirrorCamera.cullingMask;
            // 立ち上げ時は UI 非表示モード (配信に UI を見せない) でスタート。
            ApplyUIVisibility(false);

            IsActive = true;
        }

        /// <summary>
        /// 出力 RT への描画を一時停止する。Camera を disable するだけなので RT には直前の
        /// 最終フレームが残り、Spout/NDI/DesktopBlitter の下流もそのまま静止画を流せる。
        /// Cue 切替時の graph 再構築 1〜2 frame の glitch を観客に見せないために使う。
        /// </summary>
        public void Freeze()
        {
            if (!IsActive || mirrorCamera == null) return;
            mirrorCamera.enabled = false;
        }

        /// <summary>
        /// <see cref="Freeze"/> を解除し RT への描画を再開する。Active でないときは no-op。
        /// </summary>
        public void Unfreeze()
        {
            if (!IsActive || mirrorCamera == null) return;
            mirrorCamera.enabled = true;
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
        /// Mirror カメラに PerformerUI Layer を含めるかを切り替える。
        /// true で VR HMD と同じく UI を含めて配信、false で VFX/Shader 結果のみの clean output。
        /// Activate 前に呼んだ場合は IsUIVisible のみ更新し、Activate 時に反映される。
        /// </summary>
        public void SetUIVisible(bool visible)
        {
            if (IsUIVisible == visible && IsActive) return;
            ApplyUIVisibility(visible);
        }

        private void ApplyUIVisibility(bool visible)
        {
            IsUIVisible = visible;
            if (mirrorCamera == null) return;

            int uiBit = PerformerUILayer.LayerMaskBit;
            if (uiBit == 0)
            {
                // PerformerUI layer が TagManager に未登録 (Layer.NameToLayer < 0)。
                // 切替は no-op だが警告は 1 度だけ。
                if (!_warnedMissingLayer)
                {
                    Debug.LogWarning(
                        $"[MirrorOutput] Layer '{PerformerUILayer.LayerName}' が TagManager に未登録です。"
                      + "Mirror カメラの UI 表示切替は無効化されています。");
                    _warnedMissingLayer = true;
                }
                return;
            }

            mirrorCamera.cullingMask = visible
                ? _baseCullingMask | uiBit
                : _baseCullingMask & ~uiBit;
        }

        private bool _warnedMissingLayer;

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
