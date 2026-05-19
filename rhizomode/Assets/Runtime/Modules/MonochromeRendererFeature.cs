#nullable enable

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Rhizomode.Modules
{
    /// <summary>
    /// Monochrome PostEffect の URP RendererFeature。
    /// MonochromeFeatureSettings から毎フレーム値を pull して material に適用し、
    /// camera color を一度 temp に blit (material 適用) → camera color に copy back する。
    /// </summary>
    /// <remarks>
    /// MonochromeFeatureSettings.Enabled が false のときは pass を enqueue しないので
    /// disable 状態のオーバーヘッドはほぼゼロ。
    /// </remarks>
    public sealed class MonochromeRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader? shader;
        [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

        private Material? _material;
        private MonochromePass? _pass;

        public override void Create()
        {
            if (shader == null)
                shader = Shader.Find("Hidden/PostEffect/Monochrome");
            if (shader == null)
            {
                Debug.LogWarning("[MonochromeRendererFeature] Shader 'Hidden/PostEffect/Monochrome' not found.");
                return;
            }
            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new MonochromePass(_material) { renderPassEvent = injectionPoint };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!MonochromeFeatureSettings.Enabled) return;
            if (_pass == null) return;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _pass = null;
        }

        private sealed class MonochromePass : ScriptableRenderPass
        {
            private static readonly int RedWeightId = Shader.PropertyToID("_RedWeight");
            private static readonly int GreenWeightId = Shader.PropertyToID("_GreenWeight");
            private static readonly int BlueWeightId = Shader.PropertyToID("_BlueWeight");
            private static readonly int ToneShadowId = Shader.PropertyToID("_ToneShadow");
            private static readonly int ToneHighlightId = Shader.PropertyToID("_ToneHighlight");
            private static readonly int MonoBlendId = Shader.PropertyToID("_MonoBlend");

            private readonly Material _material;

            public MonochromePass(Material material)
            {
                _material = material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer) return;

                ApplySettingsToMaterial(_material);

                var source = resourceData.activeColorTexture;
                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "Monochrome_Temp";
                desc.clearBuffer = false;
                var temp = renderGraph.CreateTexture(desc);

                var applyParams = new RenderGraphUtils.BlitMaterialParameters(source, temp, _material, 0);
                renderGraph.AddBlitPass(applyParams, passName: "Monochrome_Apply");

                var copyParams = new RenderGraphUtils.BlitMaterialParameters(temp, source, _material, 0)
                {
                    material = null
                };
                renderGraph.AddBlitPass(copyParams, passName: "Monochrome_CopyBack");
            }

            private static void ApplySettingsToMaterial(Material mat)
            {
                mat.SetFloat(RedWeightId, MonochromeFeatureSettings.RedWeight);
                mat.SetFloat(GreenWeightId, MonochromeFeatureSettings.GreenWeight);
                mat.SetFloat(BlueWeightId, MonochromeFeatureSettings.BlueWeight);
                mat.SetFloat(ToneShadowId, MonochromeFeatureSettings.ToneShadow);
                mat.SetFloat(ToneHighlightId, MonochromeFeatureSettings.ToneHighlight);
                mat.SetFloat(MonoBlendId, MonochromeFeatureSettings.MonoBlend);
            }
        }
    }
}
