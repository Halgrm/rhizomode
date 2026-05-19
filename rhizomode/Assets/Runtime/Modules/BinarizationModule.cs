#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Graph.Model;
using Rhizomode.NodeCatalog.Contracts;
using UnityEngine;

namespace Rhizomode.Modules
{
    /// <summary>
    /// Binarization (S 字トーンで二値化的) PostEffect をノードグラフから driving するモジュール。
    /// </summary>
    /// <remarks>
    /// Shadow / Highlight 端点を狭めると near-binary 化、広げるとソフトコントラストになる。
    /// 純粋なグレースケール化は <see cref="MonochromeModule"/> 側で扱う。
    /// "Active" Bool ポートで効果の on/off を制御する (default false)。
    /// </remarks>
    [PerformanceModule(NodeCategory.Shader)]
    public sealed class BinarizationModule : MonoBehaviour, IPerformanceModule
    {
        private const string ParamActive = "Active";
        private const string ParamRedWeight = "RedWeight";
        private const string ParamGreenWeight = "GreenWeight";
        private const string ParamBlueWeight = "BlueWeight";
        private const string ParamToneShadow = "ToneShadow";
        private const string ParamToneHighlight = "ToneHighlight";
        private const string ParamMonoBlend = "MonoBlend";

        private static readonly List<ParamDefinition> EmptyParams = new();

        [SerializeField] private ModuleDefinition? definition;

        /// <inheritdoc />
        public string ModuleName => definition != null ? definition.moduleName : "Binarization";

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
                switch (paramName)
                {
                    case ParamActive:
                        BinarizationFeatureSettings.Enabled = (bool)value;
                        break;
                    case ParamRedWeight:
                        BinarizationFeatureSettings.RedWeight = ToFinite((float)value, BinarizationFeatureSettings.RedWeight);
                        break;
                    case ParamGreenWeight:
                        BinarizationFeatureSettings.GreenWeight = ToFinite((float)value, BinarizationFeatureSettings.GreenWeight);
                        break;
                    case ParamBlueWeight:
                        BinarizationFeatureSettings.BlueWeight = ToFinite((float)value, BinarizationFeatureSettings.BlueWeight);
                        break;
                    case ParamToneShadow:
                        BinarizationFeatureSettings.ToneShadow = Mathf.Clamp01(ToFinite((float)value, BinarizationFeatureSettings.ToneShadow));
                        break;
                    case ParamToneHighlight:
                        BinarizationFeatureSettings.ToneHighlight = Mathf.Clamp01(ToFinite((float)value, BinarizationFeatureSettings.ToneHighlight));
                        break;
                    case ParamMonoBlend:
                        BinarizationFeatureSettings.MonoBlend = Mathf.Clamp01(ToFinite((float)value, BinarizationFeatureSettings.MonoBlend));
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BinarizationModule] SetParam failed: {paramName} = {value} ({ex.Message})");
            }
        }

        /// <inheritdoc />
        public void Activate()
        {
            // Active 状態は "Active" Bool ポート (default false) が SetParam 経由で push する。
        }

        /// <inheritdoc />
        public void Deactivate()
        {
            BinarizationFeatureSettings.Enabled = false;
        }

        private static float ToFinite(float value, float fallback)
        {
            return float.IsFinite(value) ? value : fallback;
        }
    }
}
