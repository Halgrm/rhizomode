#nullable enable

namespace Rhizomode.Modules
{
    /// <summary>
    /// Binarization (S 字トーンによる二値化的) PostEffect の共有設定。
    /// BinarizationModule が書き込み、BinarizationRendererFeature が毎フレーム pull する。
    /// </summary>
    /// <remarks>
    /// チャンネルミキサーで luminance を求めた後、smoothstep(shadow, highlight, luma) で
    /// S 字カーブを通し、shadow→highlight 幅を狭めると near-binary 化する。
    /// 純粋なグレースケール化は <see cref="MonochromeFeatureSettings"/> 側で扱う。
    /// </remarks>
    public static class BinarizationFeatureSettings
    {
        /// <summary>RendererFeature が描画パスを生成するかどうか。</summary>
        public static bool Enabled = false;

        /// <summary>Red チャンネルのウェイト。</summary>
        public static float RedWeight = 0.1f;

        /// <summary>Green チャンネルのウェイト。</summary>
        public static float GreenWeight = 0.75f;

        /// <summary>Blue チャンネルのウェイト。</summary>
        public static float BlueWeight = 0.15f;

        /// <summary>S 字トーンカーブの shadow 端点 (0 に近いほど黒を持ち上げる)。</summary>
        public static float ToneShadow = 0.45f;

        /// <summary>S 字トーンカーブの highlight 端点 (1 に近いほどハイライトを抑える)。</summary>
        public static float ToneHighlight = 0.55f;

        /// <summary>カラー↔二値化のブレンド (0=フルカラー、1=完全 binarization)。</summary>
        public static float MonoBlend = 1.0f;

        /// <summary>テスト等から既定値に戻すためのリセット。</summary>
        public static void ResetToDefaults()
        {
            Enabled = false;
            RedWeight = 0.1f;
            GreenWeight = 0.75f;
            BlueWeight = 0.15f;
            ToneShadow = 0.45f;
            ToneHighlight = 0.55f;
            MonoBlend = 1.0f;
        }
    }
}
