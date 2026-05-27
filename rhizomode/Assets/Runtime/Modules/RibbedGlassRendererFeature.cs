#nullable enable

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Rhizomode.Modules
{
    /// <summary>
    /// RibbedGlass PostEffect の URP RendererFeature。
    /// RibbedGlassFeatureSettings から毎フレーム値を pull して material に適用する。
    /// </summary>
    /// <remarks>
    /// VR HMD には適用せず Mirror / Desktop 出力のみ処理する (targetTexture==null 判定)。
    /// Binarization / Monochrome と同じ injection pattern。
    /// </remarks>
    public sealed class RibbedGlassRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader? shader;
        [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

        private Material? _material;
        private RibbedGlassPass? _pass;

        public override void Create()
        {
            if (shader == null)
                shader = Shader.Find("Hidden/PostEffect/RibbedGlass");
            if (shader == null)
            {
                Debug.LogWarning("[RibbedGlassRendererFeature] Shader 'Hidden/PostEffect/RibbedGlass' not found.");
                return;
            }
            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new RibbedGlassPass(_material) { renderPassEvent = injectionPoint };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!RibbedGlassFeatureSettings.Enabled) return;
            if (_pass == null) return;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _pass = null;
        }

        private sealed class RibbedGlassPass : ScriptableRenderPass
        {
            private static readonly int RibCountId = Shader.PropertyToID("_RibCount");
            private static readonly int DistortionId = Shader.PropertyToID("_Distortion");
            private static readonly int ChromaShiftId = Shader.PropertyToID("_ChromaShift");
            private static readonly int EdgeDarkenId = Shader.PropertyToID("_EdgeDarken");
            private static readonly int FrostIntensityId = Shader.PropertyToID("_FrostIntensity");
            private static readonly int FrostGrainId = Shader.PropertyToID("_FrostGrain");
            private static readonly int BlurSamplesId = Shader.PropertyToID("_BlurSamples");
            private static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
            private static readonly int EdgeDistortionId = Shader.PropertyToID("_EdgeDistortion");
            private static readonly int EdgeFalloffId = Shader.PropertyToID("_EdgeFalloff");
            private static readonly int BarrelDistortionId = Shader.PropertyToID("_BarrelDistortion");

            private readonly Material _material;

            public RibbedGlassPass(Material material)
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
                desc.name = "RibbedGlass_Temp";
                desc.clearBuffer = false;
                var temp = renderGraph.CreateTexture(desc);

                var applyParams = new RenderGraphUtils.BlitMaterialParameters(source, temp, _material, 0);
                renderGraph.AddBlitPass(applyParams, passName: "RibbedGlass_Apply");

                resourceData.cameraColor = temp;
            }

            private static void ApplySettingsToMaterial(Material mat)
            {
                mat.SetFloat(RibCountId, RibbedGlassFeatureSettings.RibCount);
                mat.SetFloat(DistortionId, RibbedGlassFeatureSettings.Distortion);
                mat.SetFloat(ChromaShiftId, RibbedGlassFeatureSettings.ChromaShift);
                mat.SetFloat(EdgeDarkenId, RibbedGlassFeatureSettings.EdgeDarken);
                mat.SetFloat(FrostIntensityId, RibbedGlassFeatureSettings.FrostIntensity);
                mat.SetFloat(FrostGrainId, RibbedGlassFeatureSettings.FrostGrain);
                mat.SetFloat(BlurSamplesId, RibbedGlassFeatureSettings.BlurSamples);
                mat.SetFloat(BlurRadiusId, RibbedGlassFeatureSettings.BlurRadius);
                mat.SetFloat(EdgeDistortionId, RibbedGlassFeatureSettings.EdgeDistortion);
                mat.SetFloat(EdgeFalloffId, RibbedGlassFeatureSettings.EdgeFalloff);
                mat.SetFloat(BarrelDistortionId, RibbedGlassFeatureSettings.BarrelDistortion);
            }
        }
    }
}
