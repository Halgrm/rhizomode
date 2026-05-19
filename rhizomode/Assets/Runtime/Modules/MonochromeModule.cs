#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Graph.Model;
using Rhizomode.NodeCatalog.Contracts;
using UnityEngine;

namespace Rhizomode.Modules
{
    /// <summary>
    /// Monochrome PostEffect (URP RendererFeature) を rhizomode ノードグラフから driving するモジュール。
    /// </summary>
    /// <remarks>
    /// PostEffect は scene 単一の global resource なので <see cref="MonochromeFeatureSettings"/>
    /// (static) を介して値を共有する。Module が複数 spawn された場合は last-write-wins。
    /// </remarks>
    [PerformanceModule(NodeCategory.Shader)]
    public sealed class MonochromeModule : MonoBehaviour, IPerformanceModule
    {
        private const string ParamRedWeight = "RedWeight";
        private const string ParamGreenWeight = "GreenWeight";
        private const string ParamBlueWeight = "BlueWeight";
        private const string ParamToneShadow = "ToneShadow";
        private const string ParamToneHighlight = "ToneHighlight";
        private const string ParamMonoBlend = "MonoBlend";

        private static readonly List<ParamDefinition> EmptyParams = new();

        [SerializeField] private ModuleDefinition? definition;

        /// <inheritdoc />
        public string ModuleName => definition != null ? definition.moduleName : "Monochrome";

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
                    case ParamRedWeight:
                        MonochromeFeatureSettings.RedWeight = ToFinite((float)value, MonochromeFeatureSettings.RedWeight);
                        break;
                    case ParamGreenWeight:
                        MonochromeFeatureSettings.GreenWeight = ToFinite((float)value, MonochromeFeatureSettings.GreenWeight);
                        break;
                    case ParamBlueWeight:
                        MonochromeFeatureSettings.BlueWeight = ToFinite((float)value, MonochromeFeatureSettings.BlueWeight);
                        break;
                    case ParamToneShadow:
                        MonochromeFeatureSettings.ToneShadow = Mathf.Clamp01(ToFinite((float)value, MonochromeFeatureSettings.ToneShadow));
                        break;
                    case ParamToneHighlight:
                        MonochromeFeatureSettings.ToneHighlight = Mathf.Clamp01(ToFinite((float)value, MonochromeFeatureSettings.ToneHighlight));
                        break;
                    case ParamMonoBlend:
                        MonochromeFeatureSettings.MonoBlend = Mathf.Clamp01(ToFinite((float)value, MonochromeFeatureSettings.MonoBlend));
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MonochromeModule] SetParam failed: {paramName} = {value} ({ex.Message})");
            }
        }

        /// <inheritdoc />
        public void Activate()
        {
            MonochromeFeatureSettings.Enabled = true;
        }

        /// <inheritdoc />
        public void Deactivate()
        {
            MonochromeFeatureSettings.Enabled = false;
        }

        private static float ToFinite(float value, float fallback)
        {
            return float.IsFinite(value) ? value : fallback;
        }
    }
}
