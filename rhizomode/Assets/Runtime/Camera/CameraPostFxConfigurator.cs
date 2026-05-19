#nullable enable

using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// 非 VR カメラに対する URP PostProcessing 設定を集約する。
    /// </summary>
    /// <remarks>
    /// Bloom / Vignette / Tonemapping 等の URP Volume PostFX は
    /// <see cref="UniversalAdditionalCameraData.renderPostProcessing"/> が true でないと
    /// 描画されない。Mirror / Preview など audience に見せる出力カメラはこれを
    /// 集約 helper 経由で揃える (重複コード回避 + 将来追加カメラの差し込み 1 行で完結)。
    /// VR HMD は意図的に対象外 (パフォーマンスと Monochrome 等の HMD 適用回避方針)。
    /// </remarks>
    public static class CameraPostFxConfigurator
    {
        /// <summary>
        /// 対象カメラに対し URP PostProcessing を有効化する。
        /// </summary>
        /// <param name="camera">対象カメラ。null は no-op。</param>
        /// <returns>設定に成功したか。URP additional data 未取得時は false。</returns>
        public static bool EnablePostProcessing(Camera? camera)
        {
            if (camera == null) return false;

            var urpData = camera.GetUniversalAdditionalCameraData();
            if (urpData == null) return false;

            urpData.renderPostProcessing = true;
            return true;
        }
    }
}
