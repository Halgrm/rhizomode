Shader "Rhizomode/UnlitCircle"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EdgeSoftness ("Edge Softness", Range(0.0, 0.2)) = 0.02
        _RingWidth ("Ring Width (0=filled)", Range(0.0, 0.5)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "UnlitCircle"
            Blend SrcAlpha OneMinusSrcAlpha
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
                half4 _EmissionColor;
                half _EdgeSoftness;
                half _RingWidth;
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

                // 中心(0.5,0.5)からの距離 → 半径0.5の円
                float2 centered = input.uv - 0.5;
                float dist = length(centered) * 2.0; // 0=中心, 1=端

                half softness = max(_EdgeSoftness, 0.0001);
                // 円盤の透明度 (1=内側, 0=外側)
                half alpha = 1.0 - smoothstep(1.0 - softness, 1.0, dist);

                // リング表示時は内側もくり抜く
                if (_RingWidth > 0.0001)
                {
                    half innerEdge = 1.0 - _RingWidth;
                    half innerAlpha = smoothstep(innerEdge - softness, innerEdge, dist);
                    alpha *= innerAlpha;
                }

                half3 color = _BaseColor.rgb + _EmissionColor.rgb;
                return half4(color, alpha * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
