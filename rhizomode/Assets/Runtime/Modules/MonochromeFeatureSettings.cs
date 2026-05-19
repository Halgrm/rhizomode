#nullable enable

namespace Rhizomode.Modules
{
    /// <summary>
    /// Monochrome (グレースケール化) PostEffect の共有設定。
    /// MonochromeModule が書き込み、MonochromeRendererFeature が毎フレーム pull する。
    /// </summary>
    /// <remarks>
    /// 純粋なチャンネルミキサー luminance 変換のみ。トーンカーブによる二値化的処理は
    /// <see cref="BinarizationFeatureSettings"/> 側で扱う。
    /// PostEffect は scene 単一の global resource なので IPerformanceModule の
    /// 通常パターン (1 prefab = 1 instance) と整合させるため static container を使う。
    /// Module が複数 spawn された場合は last-write-wins。
    /// </remarks>
    public static class MonochromeFeatureSettings
    {
        /// <summary>RendererFeature が描画パスを生成するかどうか。</summary>
        public static bool Enabled = false;

        /// <summary>Red チャンネルのウェイト。</summary>
        public static float RedWeight = 0.299f;

        /// <summary>Green チャンネルのウェイト (人間の眼の感度が最高)。</summary>
        public static float GreenWeight = 0.587f;

        /// <summary>Blue チャンネルのウェイト。</summary>
        public static float BlueWeight = 0.114f;

        /// <summary>カラー↔グレースケールのブレンド (0=フルカラー、1=完全モノクロ)。</summary>
        public static float MonoBlend = 1.0f;

        /// <summary>テスト等から既定値に戻すためのリセット。</summary>
        public static void ResetToDefaults()
        {
            Enabled = false;
            RedWeight = 0.299f;
            GreenWeight = 0.587f;
            BlueWeight = 0.114f;
            MonoBlend = 1.0f;
        }
    }
}
