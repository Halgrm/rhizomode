#nullable enable

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

using Rhizomode.Presentation.Layering;
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
        private RenderTexture? _cameraRt;
        private Material? _glitchMaterial;
        private bool _isEndCameraRenderingSubscribed;
        private bool _warnedMissingGlitchShader;
        private float _glitchAmount;

        // 起動時 (Activate) に最初に観測した cullingMask。
        // SetUIVisible(true) で復元するための baseline。Inspector で UI layer を含めて
        // 設定してあっても、起動直後に自動で UI を除外できるよう Activate 時に
        // MirrorHidden bit を落とす。
        private int _baseCullingMask;

        /// <summary>ミラー出力用のRenderTexture。Activate後に有効。</summary>
        public RenderTexture? OutputTexture => _outputTexture;

        /// <summary>現在のグリッチポストエフェクト強度。</summary>
        public float GlitchAmount => _glitchAmount;

        /// <summary>ミラー出力が有効かどうか。</summary>
        public bool IsActive { get; private set; }

        /// <summary>Mirror 出力に UI (MirrorHidden layer) を含めるか。default は false (= 配信に UI を出さない)。</summary>
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

            CreateCameraRenderTexture();
            CreateGlitchMaterialIfNeeded();
            SubscribeEndCameraRendering();

            mirrorCamera.targetTexture = _cameraRt;
            mirrorCamera.enabled = true;
            ConfigureUrpCamera(mirrorCamera);

            _baseCullingMask = mirrorCamera.cullingMask;
            IsActive = true;
            // Activate 前に SetUIVisible で事前注入された値 (default = false) を cullingMask へ反映。
            // RhizomodeSettings.MirrorShowUiDefault で起動時挙動を制御する場合は wiring 側で
            // Activate 直前に SetUIVisible(...) を呼ぶ。
            ApplyUIVisibility(IsUIVisible);
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

            UnsubscribeEndCameraRendering();

            if (mirrorCamera != null)
            {
                mirrorCamera.enabled = false;
                mirrorCamera.targetTexture = null;
            }

            ReleaseCameraRenderTexture();
            ReleaseOutputTexture();
            IsActive = false;
        }

        private void OnDestroy()
        {
            Deactivate();
            ReleaseGlitchMaterial();
        }

        /// <summary>
        /// グリッチポストエフェクト強度を設定する。NaN/Infinity は 0 として扱う。
        /// </summary>
        public void SetGlitchAmount(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount))
            {
                _glitchAmount = 0f;
                return;
            }

            _glitchAmount = Mathf.Clamp01(amount);
        }

        /// <summary>
        /// Mirror カメラに MirrorHidden Layer を含めるかを切り替える。
        /// true で VR HMD と同じく UI を含めて配信、false で VFX/Shader 結果のみの clean output。
        /// Activate 前に呼んだ場合は IsUIVisible のみ更新し、Activate 内で baseCullingMask 確定後に反映。
        /// </summary>
        public void SetUIVisible(bool visible)
        {
            if (!IsActive)
            {
                // baseCullingMask 未確定なので cullingMask は触らず state だけ保持。
                IsUIVisible = visible;
                return;
            }
            if (IsUIVisible == visible) return;
            ApplyUIVisibility(visible);
        }

        private void ApplyUIVisibility(bool visible)
        {
            IsUIVisible = visible;
            if (mirrorCamera == null) return;

            int uiBit = MirrorHiddenLayer.LayerMaskBit;
            if (uiBit == 0)
            {
                // MirrorHidden layer が TagManager に未登録 (Layer.NameToLayer < 0)。
                // 切替は no-op だが警告は 1 度だけ。
                if (!_warnedMissingLayer)
                {
                    Debug.LogWarning(
                        $"[MirrorOutput] Layer '{MirrorHiddenLayer.LayerName}' が TagManager に未登録です。"
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
            if (urpData != null)
                urpData.renderType = CameraRenderType.Base;

            // Bloom / Vignette / Tonemapping + BloomModule / MonochromeModule の有効化
            Rhizomode.Cameras.CameraPostFxConfigurator.EnablePostProcessing(camera);
        }

        private void CreateCameraRenderTexture()
        {
            if (_outputTexture == null) return;

            var descriptor = _outputTexture.descriptor;
            _cameraRt = new RenderTexture(descriptor)
            {
                name = "MirrorCamera_RT"
            };
            _cameraRt.Create();
        }

        private void CreateGlitchMaterialIfNeeded()
        {
            if (_glitchMaterial != null) return;

            var shader = Shader.Find("Rhizomode/GlitchFullscreen");
            if (shader == null)
            {
                WarnMissingGlitchShader();
                return;
            }

            _glitchMaterial = new Material(shader)
            {
                name = "MirrorOutput_GlitchMaterial"
            };
        }

        private void WarnMissingGlitchShader()
        {
            if (_warnedMissingGlitchShader) return;

            Debug.LogWarning(
                "[MirrorOutput] Shader 'Rhizomode/GlitchFullscreen' was not found. Glitch uses passthrough.");
            _warnedMissingGlitchShader = true;
        }

        private void SubscribeEndCameraRendering()
        {
            if (_isEndCameraRenderingSubscribed) return;

            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
            _isEndCameraRenderingSubscribed = true;
        }

        private void UnsubscribeEndCameraRendering()
        {
            if (!_isEndCameraRenderingSubscribed) return;

            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            _isEndCameraRenderingSubscribed = false;
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            _ = context;
            if (camera != mirrorCamera) return;
            if (_cameraRt == null || _outputTexture == null) return;

            if (_glitchAmount <= 0f || _glitchMaterial == null)
            {
                Graphics.Blit(_cameraRt, _outputTexture);
                return;
            }

            _glitchMaterial.SetFloat("_GlitchAmount", _glitchAmount);
            _glitchMaterial.SetFloat("_TimeSeed", Time.unscaledTime);
            Graphics.Blit(_cameraRt, _outputTexture, _glitchMaterial);
        }

        private void ReleaseCameraRenderTexture()
        {
            if (_cameraRt == null) return;

            _cameraRt.Release();
            Destroy(_cameraRt);
            _cameraRt = null;
        }

        private void ReleaseGlitchMaterial()
        {
            if (_glitchMaterial == null) return;

            Destroy(_glitchMaterial);
            _glitchMaterial = null;
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
