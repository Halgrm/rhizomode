// 黒色の磁性流体: 黒ベース + Fresnel rim (HDR emission for bloom) + vertex displacement
// URP Unlit Forward。VFX Graph の Output Particle Mesh / Lit メッシュ両方で使える想定。
Shader "Rhizomode/Ferrofluid_Rim"
{
    Properties
    {
        [HDR] _BaseColor("Base Color (Black)", Color) = (0,0,0,1)
        [HDR] _RimColor("Rim Color (Cyan glow)", Color) = (0.2, 0.6, 1.0, 1)
        _RimPower("Rim Power", Range(0.1, 16)) = 6.0
        _RimIntensity("Rim Intensity", Range(0, 20)) = 4.0
        _Displacement("Vertex Displacement", Range(0, 1)) = 0.0
        _DisplacementScale("Noise Scale", Range(0.1, 20)) = 4.0
        _DisplacementSpeed("Noise Speed", Range(0, 5)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                float _Displacement;
                float _DisplacementScale;
                float _DisplacementSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
            };

            // 3D hash → simplex 風の安価な擬似ノイズ (球頂点の displacement 用)。
            // 高品質ノイズは VFX Graph 側に任せ、ここでは shape を波立たせる程度。
            float Hash31(float3 p)
            {
                p = frac(p * 0.3183099 + float3(0.1, 0.2, 0.3));
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float SmoothNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = Hash31(i + float3(0,0,0));
                float n100 = Hash31(i + float3(1,0,0));
                float n010 = Hash31(i + float3(0,1,0));
                float n110 = Hash31(i + float3(1,1,0));
                float n001 = Hash31(i + float3(0,0,1));
                float n101 = Hash31(i + float3(1,0,1));
                float n011 = Hash31(i + float3(0,1,1));
                float n111 = Hash31(i + float3(1,1,1));
                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z) * 2.0 - 1.0;
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                // 球面に沿った noise displacement (Wave trigger 時に _Displacement を上げる)
                float t = _Time.y * _DisplacementSpeed;
                float3 samplePos = IN.positionOS.xyz * _DisplacementScale + t;
                float noise = SmoothNoise(samplePos);
                float3 displacedOS = IN.positionOS.xyz + IN.normalOS * noise * _Displacement * 0.3;

                float3 positionWS = TransformObjectToWorld(displacedOS);
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float NdotV = saturate(dot(N, V));

                // Fresnel: 縁ほど 1 に近づく。RimPower で立ち上がりの鋭さを制御。
                float fresnel = pow(1.0 - NdotV, _RimPower);

                // HDR 出力で bloom を効かせる。base は黒、rim は HDR cyan。
                float3 col = _BaseColor.rgb + _RimColor.rgb * fresnel * _RimIntensity;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
