Shader "Rhizomode/EdgeGlow"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.63, 0.82, 0.94, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1.0
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "EdgeGlow"
            Blend One One // Additive
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _GlowIntensity;
                half _PulseSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // UV.x: エッジに沿った方向 (0-1)
                // UV.y: エッジの幅方向 (0-1, 0.5が中心)
                float edgePos = input.uv.x;
                float widthPos = input.uv.y;

                // 幅方向のソフトフォールオフ（中心が明るく、端が暗い）
                float centerDist = abs(widthPos - 0.5) * 2.0; // 0=中心, 1=端
                float widthFalloff = 1.0 - centerDist * centerDist; // 二次関数フォールオフ

                // パルスアニメーション: エッジに沿って走る光
                float pulse = sin(edgePos * 6.2832 - _Time.y * _PulseSpeed) * 0.5 + 0.5;
                float pulseContribution = lerp(0.6, 1.0, pulse); // 0.6-1.0の範囲で脈動

                // 最終カラー
                half3 color = _BaseColor.rgb * _GlowIntensity * widthFalloff * pulseContribution;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
