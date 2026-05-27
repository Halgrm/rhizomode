#nullable enable

namespace Rhizomode.Modules
{
    /// <summary>
    /// RibbedGlass (リブガラス + フロスト + 色収差) PostEffect の共有設定。
    /// RibbedGlassModule が書き込み、RibbedGlassRendererFeature が毎フレーム pull する。
    /// </summary>
    public static class RibbedGlassFeatureSettings
    {
        /// <summary>RendererFeature が描画パスを生成するかどうか。</summary>
        public static bool Enabled = false;

        // HARDCODE-OK: tuning defaults — live で ModuleDefinition から override される値。
        // 初期値の役割は「ノード未接続 / Active=false 時に shader を破綻させない」ガード。
        public static float RibCount = 50f;
        public static float Distortion = 0.03f;
        public static float ChromaShift = 0.2f;
        public static float EdgeDarken = 0.3f;
        public static float FrostIntensity = 0.005f;
        public static float FrostGrain = 200f;
        public static float BlurSamples = 8f;
        public static float BlurRadius = 0.003f;
        public static float EdgeDistortion = 2.0f;
        public static float EdgeFalloff = 2.0f;
        public static float BarrelDistortion = 0.5f;

        /// <summary>テスト等から既定値に戻すためのリセット。</summary>
        public static void ResetToDefaults()
        {
            // HARDCODE-OK: 上の初期値と同期して維持する。新しいパラメータを足したら両方更新する。
            Enabled = false;
            RibCount = 50f;
            Distortion = 0.03f;
            ChromaShift = 0.2f;
            EdgeDarken = 0.3f;
            FrostIntensity = 0.005f;
            FrostGrain = 200f;
            BlurSamples = 8f;
            BlurRadius = 0.003f;
            EdgeDistortion = 2.0f;
            EdgeFalloff = 2.0f;
            BarrelDistortion = 0.5f;
        }
    }
}
