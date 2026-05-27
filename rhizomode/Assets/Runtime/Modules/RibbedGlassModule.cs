#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Graph.Model;
using Rhizomode.NodeCatalog.Contracts;
using UnityEngine;

namespace Rhizomode.Modules
{
    /// <summary>
    /// RibbedGlass (リブガラス + フロスト + 色収差 + バレル) PostEffect を
    /// ノードグラフから driving するモジュール。
    /// </summary>
    /// <remarks>
    /// "Active" Bool ポートで効果の on/off を制御する (default false)。
    /// 残りは Float ポートで shader プロパティと 1:1 対応。
    ///
    /// Single-instance assumption: <see cref="RibbedGlassFeatureSettings"/> はプロセス静的
    /// 共有状態。同時に複数の RibbedGlassModule node を置くと最後の <see cref="SetParam"/> が
    /// 全てを上書きし、片方の <see cref="Deactivate"/> が他方の出力も停止させる。VJ live の
    /// 用途では同一 effect の重複 spawn は意図しないため許容している (cf. memory
    /// <c>feedback_health_monitor</c> 流 fail-open の "Video 継続" を最重視)。
    /// 将来 multi-instance が要件化したら instance ownership を追跡する設計に変更が要る。
    /// </remarks>
    [PerformanceModule(NodeCategory.Shader)]
    public sealed class RibbedGlassModule : MonoBehaviour, IPerformanceModule
    {
        private const string ParamActive = "Active";
        private const string ParamRibCount = "RibCount";
        private const string ParamDistortion = "Distortion";
        private const string ParamChromaShift = "ChromaShift";
        private const string ParamEdgeDarken = "EdgeDarken";
        private const string ParamFrostIntensity = "FrostIntensity";
        private const string ParamFrostGrain = "FrostGrain";
        private const string ParamBlurSamples = "BlurSamples";
        private const string ParamBlurRadius = "BlurRadius";
        private const string ParamEdgeDistortion = "EdgeDistortion";
        private const string ParamEdgeFalloff = "EdgeFalloff";
        private const string ParamBarrelDistortion = "BarrelDistortion";

        // GPU saturation guard: BlurSamples drives a fragment-shader for-loop iteration count.
        // 16 サンプルで 4K 出力相当でも安全側 (HMD と Mirror の同時 90fps を死守)。
        private const float BlurSamplesMin = 1f;
        private const float BlurSamplesMax = 16f;

        // 0 や負値で pow(yDist, _EdgeFalloff) が未定義になり全画面が破綻するため必ず正の上下限を持たせる。
        private const float EdgeFalloffMin = 1f;
        private const float EdgeFalloffMax = 5f;

        private static readonly List<ParamDefinition> EmptyParams = new();

        [SerializeField] private ModuleDefinition? definition;

        /// <inheritdoc />
        public string ModuleName => definition != null ? definition.moduleName : "RibbedGlass";

        /// <inheritdoc />
        public IReadOnlyList<ParamDefinition> Params =>
            definition != null ? definition.parameters : EmptyParams;

        /// <inheritdoc />
        public void Initialize(ModuleDefinition def)
        {
            definition = def;
        }

        /// <inheritdoc />
        public void SetParam(string paramName, object value)
        {
            try
            {
                if (TrySetBool(paramName, value)) return;
                TrySetFloat(paramName, value);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RibbedGlassModule] SetParam failed: {paramName} = {value} ({ex.Message})");
            }
        }

        // 型不一致 (int / null / 文字列) は throw 経路ではなく `is` パターンで弾いて
        // 静かに無視し、video pipeline を絶対に止めない。Codex review feedback (FAIL→) 反映。
        private static bool TrySetBool(string name, object value)
        {
            if (name != ParamActive) return false;
            if (value is bool b) RibbedGlassFeatureSettings.Enabled = b;
            return true;
        }

        private static void TrySetFloat(string name, object value)
        {
            if (value is not float f) return;
            switch (name)
            {
                case ParamRibCount: AssignFinite(ref RibbedGlassFeatureSettings.RibCount, f); break;
                case ParamDistortion: AssignFinite(ref RibbedGlassFeatureSettings.Distortion, f); break;
                case ParamChromaShift: AssignClamped01(ref RibbedGlassFeatureSettings.ChromaShift, f); break;
                case ParamEdgeDarken: AssignClamped01(ref RibbedGlassFeatureSettings.EdgeDarken, f); break;
                case ParamFrostIntensity: AssignFinite(ref RibbedGlassFeatureSettings.FrostIntensity, f); break;
                case ParamFrostGrain: AssignFinite(ref RibbedGlassFeatureSettings.FrostGrain, f); break;
                case ParamBlurSamples: AssignClamped(ref RibbedGlassFeatureSettings.BlurSamples, f, BlurSamplesMin, BlurSamplesMax); break;
                case ParamBlurRadius: AssignFinite(ref RibbedGlassFeatureSettings.BlurRadius, f); break;
                case ParamEdgeDistortion: AssignFinite(ref RibbedGlassFeatureSettings.EdgeDistortion, f); break;
                case ParamEdgeFalloff: AssignClamped(ref RibbedGlassFeatureSettings.EdgeFalloff, f, EdgeFalloffMin, EdgeFalloffMax); break;
                case ParamBarrelDistortion: AssignFinite(ref RibbedGlassFeatureSettings.BarrelDistortion, f); break;
            }
        }

        private static void AssignFinite(ref float dest, float value)
        {
            if (float.IsFinite(value)) dest = value;
        }

        private static void AssignClamped01(ref float dest, float value)
        {
            if (float.IsFinite(value)) dest = Mathf.Clamp01(value);
        }

        private static void AssignClamped(ref float dest, float value, float min, float max)
        {
            if (float.IsFinite(value)) dest = Mathf.Clamp(value, min, max);
        }

        /// <inheritdoc />
        public void Activate()
        {
            // Active 状態は "Active" Bool ポート (default false) が SetParam 経由で push する。
        }

        /// <inheritdoc />
        public void Deactivate()
        {
            RibbedGlassFeatureSettings.Enabled = false;
        }
    }
}
