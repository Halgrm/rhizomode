Shader "Custom/glassshad"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _IOR("Index of Refraction", Range(1.0, 2.5)) = 1.15
        _BlurRadius("Blur Radius", Range(0.0, 0.05)) = 0.008
        _FresnelPower("Fresnel Power", Range(1.0, 10.0)) = 4.0

        _FresnelColor("Fresnel Color", Color) = (1, 1, 1, 1)

    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }     

        Pass
        {
             Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #define BLUR_SAMPLES 12
            static const float GOLDEN_ANGLE = 2.39996323; 
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 eyeVectorWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _IOR;
                float _BlurRadius;
                float _FresnelPower;
                half4 _FresnelColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS =TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.eyeVectorWS = normalize(posWS - GetCameraPositionWS());
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.positionHCS.xy / _ScreenParams.xy;
                float3 N = normalize(IN.normalWS);
                float3 I = normalize(IN.eyeVectorWS);
                float eta = 1.0 / _IOR;
                float3 refractVec = refract(I, N, eta);
                float2 refractedUV = screenUV +refractVec.xy;
                half3 sceneColor = half3(0, 0, 0);
                for (int i = 0; i < BLUR_SAMPLES; i++)
                {
                    float r = sqrt(((float)i + 0.5) / BLUR_SAMPLES);
                    float angle = (float)i * GOLDEN_ANGLE;
                    float2 offset = _BlurRadius*r * float2(cos(angle), sin(angle));
                    sceneColor += SampleSceneColor(refractedUV + offset);
                }
                sceneColor /= BLUR_SAMPLES;
                float fresnelDot = abs(dot(I, N));
                float fresnel = pow(1.0 - fresnelDot, _FresnelPower);
                half3 finalRGB = sceneColor * _BaseColor.rgb;
                finalRGB += _FresnelColor.rgb * fresnel*_FresnelColor.a;
                half alpha = saturate(_BaseColor.a + fresnel);
                half4 color = half4(finalRGB, alpha);
                return color;
            }
            ENDHLSL
        }
    }
}
