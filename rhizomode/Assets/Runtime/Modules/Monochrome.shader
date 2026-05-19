Shader "Hidden/PostEffect/Monochrome"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Monochrome"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment FragMonochrome

            CBUFFER_START(UnityPerMaterial)
                float _RedWeight;
                float _GreenWeight;
                float _BlueWeight;
                float _ToneShadow;
                float _ToneHighlight;
                float _MonoBlend;
            CBUFFER_END

            half4 FragMonochrome(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // Channel mixer: チャンネル別ウェイトで luminance を合成
                float luma = dot(col.rgb, float3(_RedWeight, _GreenWeight, _BlueWeight));

                // S 字トーンカーブ: shadow を黒に潰し highlight を白にクリップ
                float shadow = _ToneShadow;
                float highlight = max(_ToneHighlight, shadow + 1e-4);
                luma = smoothstep(shadow, highlight, luma);

                // カラー↔モノクロブレンド
                col.rgb = lerp(col.rgb, luma.xxx, saturate(_MonoBlend));
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
