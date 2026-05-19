#nullable enable

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Rhizomode.Modules
{
    /// <summary>
    /// Binarization PostEffect の URP RendererFeature (二値化的グレースケール)。
    /// BinarizationFeatureSettings から毎フレーム値を pull して material に適用。
    /// </summary>
    /// <remarks>
    /// VR HMD には適用せず Mirror / Desktop 出力のみ処理する (targetTexture==null 判定)。
    /// xrRendering は HMD active 時に全カメラで true になるため使えない (URP 1469-1480)。
    /// </remarks>
    public sealed class BinarizationRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader? shader;
        [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

        private Material? _material;
        private BinarizationPass? _pass;

        public override void Create()
        {
            if (shader == null)
                shader = Shader.Find("Hidden/PostEffect/Binarization");
            if (shader == null)
            {
                Debug.LogWarning("[BinarizationRendererFeature] Shader 'Hidden/PostEffect/Binarization' not found.");
                return;
            }
            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new BinarizationPass(_material) { renderPassEvent = injectionPoint };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!BinarizationFeatureSettings.Enabled) return;
            if (_pass == null) return;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _pass = null;
        }

        private sealed class BinarizationPass : ScriptableRenderPass
        {
            private static readonly int RedWeightId = Shader.PropertyToID("_RedWeight");
            private static readonly int GreenWeightId = Shader.PropertyToID("_GreenWeight");
            private static readonly int BlueWeightId = Shader.PropertyToID("_BlueWeight");
            private static readonly int ToneShadowId = Shader.PropertyToID("_ToneShadow");
            private static readonly int ToneHighlightId = Shader.PropertyToID("_ToneHighlight");
            private static readonly int MonoBlendId = Shader.PropertyToID("_MonoBlend");

            private readonly Material _material;

            public BinarizationPass(Material material)
            {
                _material = material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer) return;

                var cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.camera == null || cameraData.camera.targetTexture == null) return;

                ApplySettingsToMaterial(_material);

                var source = resourceData.activeColorTexture;
                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "Binarization_Temp";
                desc.clearBuffer = false;
                var temp = renderGraph.CreateTexture(desc);

                var applyParams = new RenderGraphUtils.BlitMaterialParameters(source, temp, _material, 0);
                renderGraph.AddBlitPass(applyParams, passName: "Binarization_Apply");

                resourceData.cameraColor = temp;
            }

            private static void ApplySettingsToMaterial(Material mat)
            {
                mat.SetFloat(RedWeightId, BinarizationFeatureSettings.RedWeight);
                mat.SetFloat(GreenWeightId, BinarizationFeatureSettings.GreenWeight);
                mat.SetFloat(BlueWeightId, BinarizationFeatureSettings.BlueWeight);
                mat.SetFloat(ToneShadowId, BinarizationFeatureSettings.ToneShadow);
                mat.SetFloat(ToneHighlightId, BinarizationFeatureSettings.ToneHighlight);
                mat.SetFloat(MonoBlendId, BinarizationFeatureSettings.MonoBlend);
            }
        }
    }
}
