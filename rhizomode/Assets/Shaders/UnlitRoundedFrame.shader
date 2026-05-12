Shader "Rhizomode/UnlitRoundedFrame"
{
    Properties
    {
        _BaseColor ("Fill Color", Color) = (0, 0, 0, 0.95)
        _BorderColor ("Border Color", Color) = (1, 1, 1, 0.6)
        _RectSize ("Rect Size (world m)", Vector) = (1, 1, 0, 0)
        _CornerRadius ("Corner Radius (world m)", Float) = 0.03
        _BorderWidth ("Border Width (world m, 0 = no border)", Float) = 0.003
        _EdgeSoftness ("Edge Softness (world m)", Float) = 0.0015
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
            Name "UnlitRoundedFrame"
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
                half4 _BorderColor;
                float4 _RectSize;
                float _CornerRadius;
                float _BorderWidth;
                float _EdgeSoftness;
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

            // Signed distance to rounded rect (centered, halfSize, radius)
            float sdRoundedRect(float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - halfSize + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 size = max(_RectSize.xy, float2(0.0001, 0.0001));
                float2 p = (input.uv - 0.5) * size;
                float2 halfSize = size * 0.5;
                float radius = clamp(_CornerRadius, 0.0, min(halfSize.x, halfSize.y));
                float softness = max(_EdgeSoftness, 0.00005);

                float sd = sdRoundedRect(p, halfSize, radius);

                // Outer alpha: 1 inside, 0 outside (anti-aliased)
                half outerAlpha = 1.0 - smoothstep(-softness, 0.0, sd);

                // Border mask: ring near the boundary
                half borderMask = 0.0;
                if (_BorderWidth > 0.00001)
                {
                    float innerEdge = -_BorderWidth;
                    borderMask = smoothstep(innerEdge - softness, innerEdge, sd) * outerAlpha;
                }

                half3 color = lerp(_BaseColor.rgb, _BorderColor.rgb, borderMask);
                half alpha = lerp(_BaseColor.a, _BorderColor.a, borderMask) * outerAlpha;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
