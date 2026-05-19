#nullable enable

using UnityEngine;

namespace Rhizomode.Modules
{
    /// <summary>
    /// Monochrome PostEffect の共有設定。MonochromeModule が書き込み、
    /// MonochromeRendererFeature が毎フレーム読み取って material に push する。
    /// </summary>
    /// <remarks>
    /// PostEffect は scene 単一の global resource なので IPerformanceModule の
    /// 通常パターン (1 prefab = 1 instance) と整合させるため static container を使う。
    /// Module が複数 spawn された場合は last-write-wins (PostFX として意味的に問題なし)。
    /// </remarks>
    public static class MonochromeFeatureSettings
    {
        /// <summary>RendererFeature が描画パスを生成するかどうか。</summary>
        public static bool Enabled = false;

        /// <summary>Red チャンネルのモノクロ変換ウェイト。</summary>
        public static float RedWeight = 0.1f;

        /// <summary>Green チャンネルのモノクロ変換ウェイト (人間の眼の感度が最高)。</summary>
        public static float GreenWeight = 0.75f;

        /// <summary>Blue チャンネルのモノクロ変換ウェイト。</summary>
        public static float BlueWeight = 0.15f;

        /// <summary>S 字トーンカーブの shadow 端点 (0 に近いほど黒を持ち上げる)。</summary>
        public static float ToneShadow = 0.05f;

        /// <summary>S 字トーンカーブの highlight 端点 (1 に近いほどハイライトを抑える)。</summary>
        public static float ToneHighlight = 0.95f;

        /// <summary>カラー↔モノクロのブレンド (0=フルカラー、1=完全モノクロ)。</summary>
        public static float MonoBlend = 1.0f;

        /// <summary>テスト等から既定値に戻すためのリセット。</summary>
        public static void ResetToDefaults()
        {
            Enabled = false;
            RedWeight = 0.1f;
            GreenWeight = 0.75f;
            BlueWeight = 0.15f;
            ToneShadow = 0.05f;
            ToneHighlight = 0.95f;
            MonoBlend = 1.0f;
        }
    }
}
