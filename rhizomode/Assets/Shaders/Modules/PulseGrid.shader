Shader "Rhizomode/PulseGrid"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 10)) = 1.0
        _BaseColor ("Base Color", Color) = (0, 0.8, 1, 1)
        _Speed ("Speed", Range(0, 10)) = 1.0
        _Active ("Active", Float) = 1.0
        _GridScale ("Grid Scale", Range(1, 50)) = 10.0
        _LineWidth ("Line Width", Range(0.01, 0.2)) = 0.05
        _PulseRadius ("Pulse Radius", Range(0.1, 5)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "PulseGrid"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
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
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float4 _BaseColor;
                float _Speed;
                float _Active;
                float _GridScale;
                float _LineWidth;
                float _PulseRadius;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Active toggle
                if (_Active < 0.5) return half4(0, 0, 0, 0);

                float2 uv = input.uv;

                // Grid lines
                float2 gridUV = frac(uv * _GridScale);
                float2 gridDist = min(gridUV, 1.0 - gridUV);
                float gridLine = 1.0 - smoothstep(0.0, _LineWidth, min(gridDist.x, gridDist.y));

                // Radial pulse from center
                float dist = length(uv - 0.5) * 2.0;
                float pulse = sin(dist * 6.28318 / _PulseRadius - _Time.y * _Speed * 3.0);
                pulse = pulse * 0.5 + 0.5; // remap 0-1
                pulse = pow(pulse, 2.0);

                // Combine
                float alpha = gridLine * pulse * _Intensity;
                alpha = saturate(alpha);

                // Color with HDR intensity
                half3 color = _BaseColor.rgb * _Intensity;

                // Glow at intersections
                float2 intGridUV = frac(uv * _GridScale);
                float2 intDist = min(intGridUV, 1.0 - intGridUV);
                float intersection = 1.0 - smoothstep(0.0, _LineWidth * 1.5, length(intDist));
                color += _BaseColor.rgb * intersection * pulse * _Intensity * 2.0;

                half4 finalColor = half4(color, alpha);
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);
                return finalColor;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}