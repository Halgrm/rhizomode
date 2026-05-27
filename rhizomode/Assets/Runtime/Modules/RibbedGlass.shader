Shader "Hidden/PostEffect/RibbedGlass"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "RibbedGlass"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment FragRibbedGlass

            CBUFFER_START(UnityPerMaterial)
                float _RibCount;
                float _Distortion;
                float _ChromaShift;
                float _EdgeDarken;
                float _FrostIntensity;
                float _FrostGrain;
                float _BlurSamples;
                float _BlurRadius;
                float _EdgeDistortion;
                float _EdgeFalloff;
                float _BarrelDistortion;
            CBUFFER_END

            #define RG_PI 3.14159265
            #define RG_GOLDEN 2.39996323

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(443.897, 441.423, 437.195));
                p3 += dot(p3, p3.yzx + 19.19);
                return frac((p3.xx + p3.yz) * p3.zy) * 2.0 - 1.0;
            }

            // 単色サンプル (Blit Source の RGB のみ参照)。
            float3 SampleSource(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
            }

            half4 FragRibbedGlass(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // 1. 上下バレル歪み
                float2 centered = uv - 0.5;
                float barrelStrength = centered.y * centered.y * _BarrelDistortion;
                float2 barrelUV = float2(
                    uv.x + centered.x * barrelStrength,
                    uv.y + centered.y * barrelStrength);

                // 2. リブ歪み + 上下端で増幅
                float yDist = abs(barrelUV.y - 0.5) * 2.0;
                float edgeMask = pow(yDist, _EdgeFalloff);

                float phase = frac(barrelUV.x * _RibCount);
                float angle = (phase - 0.5) * RG_PI;
                float displacement = sin(angle) * _Distortion * (1.0 + edgeMask * _EdgeDistortion);

                // 3. すりガラスノイズ
                float2 noiseUV = barrelUV * _FrostGrain;
                float2 frostOffset = Hash22(noiseUV) * _FrostIntensity;

                // 4. 複数サンプルぼかし (RGB ごとに chroma shift)
                float3 colR = 0;
                float3 colG = 0;
                float3 colB = 0;

                int samples = max(1, (int)_BlurSamples);
                float invSamples = 1.0 / (float)samples;

                for (int i = 0; i < samples; i++)
                {
                    float r = sqrt((float)i * invSamples) * _BlurRadius;
                    float theta = i * RG_GOLDEN;
                    float2 offset = float2(cos(theta), sin(theta)) * r;
                    offset += Hash22(noiseUV + i * 7.13) * _FrostIntensity * 0.5;

                    float2 baseUV = barrelUV + frostOffset + offset;

                    float2 uvR = float2(baseUV.x + displacement * (1.0 + _ChromaShift), baseUV.y);
                    float2 uvG = float2(baseUV.x + displacement,                       baseUV.y);
                    float2 uvB = float2(baseUV.x + displacement * (1.0 - _ChromaShift), baseUV.y);

                    colR += SampleSource(uvR);
                    colG += SampleSource(uvG);
                    colB += SampleSource(uvB);
                }

                float3 col = float3(colR.r, colG.g, colB.b) * invSamples;

                // 5. リブ境界の暗さ
                float edge = 1.0 - pow(abs(sin(angle)), 0.3) * _EdgeDarken;
                col *= edge;

                // 6. 表面の粒状感
                float grain = Hash22(uv * 800.0).x * 0.03;
                col += grain;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
