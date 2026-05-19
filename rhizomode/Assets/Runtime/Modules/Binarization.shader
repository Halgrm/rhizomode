Shader "Hidden/PostEffect/Binarization"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Binarization"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment FragBinarization

            CBUFFER_START(UnityPerMaterial)
                float _RedWeight;
                float _GreenWeight;
                float _BlueWeight;
                float _ToneShadow;
                float _ToneHighlight;
                float _MonoBlend;
            CBUFFER_END

            half4 FragBinarization(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // Channel mixer
                float luma = dot(col.rgb, float3(_RedWeight, _GreenWeight, _BlueWeight));

                // S 字トーンカーブ (shadow→highlight 幅が狭いほど near-binary)
                float shadow = _ToneShadow;
                float highlight = max(_ToneHighlight, shadow + 1e-4);
                luma = smoothstep(shadow, highlight, luma);

                col.rgb = lerp(col.rgb, luma.xxx, saturate(_MonoBlend));
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
