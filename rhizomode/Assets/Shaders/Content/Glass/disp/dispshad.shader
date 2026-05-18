Shader "Custom/dispshad"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _IorR("IOR Red",    Range(1.0, 2.5)) = 1.15
        _IorY("IOR Yellow", Range(1.0, 2.5)) = 1.16
        _IorG("IOR Green",  Range(1.0, 2.5)) = 1.18
        _IorC("IOR Cyan",   Range(1.0, 2.5)) = 1.20
        _IorB("IOR Blue",   Range(1.0, 2.5)) = 1.22
        _IorV("IOR Violet", Range(1.0, 2.5)) = 1.24
        _RefractPower("Refract Power",Range(0.0, 1.0)) = 0.2
        _ChromaticAberration("Chromatic Aberration", Range(0.0, 2.0)) = 1.0
        _Saturation("Saturation",Range(0.0, 3.0)) = 1.3

        _LightDir       ("Light Direction (World)", Vector) = (-1, 1, -1, 0)
        _Shininess      ("Shininess",                Range(1, 200))   = 32
        _Diffuseness    ("Diffuseness",              Range(0, 1))     = 0.3
        _LightIntensity ("Light Intensity",          Range(0, 2))     = 0.5
        _FresnelPower("Fresnel Power", Range(1.0, 10.0)) = 4.0
        _FresnelColor("Fresnel Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Transparent"}

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            #define LOOP_COUNT 12       
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;

            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 eyeVectorWS : TEXCOORD1;
            };


            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _IorR;
                float _IorY;
                float _IorG;
                float _IorC;
                float _IorB;
                float _IorV;
                float _RefractPower;
                float _ChromaticAberration;
                float _Saturation;
                float4 _LightDir;
                float _Shininess;
                float _Diffuseness;
                float _LightIntensity;
                float _FresnelPower;
                half4 _FresnelColor;
            CBUFFER_END
            // 彩度調整 (組み込み saturate と衝突しないよう Custom 接尾辞)
            half3 SaturateCustom(half3 rgb, float intensity)
            {
                // ITU-R BT.709 輝度係数
                const half3 luminanceCoeff = half3(0.2125, 0.7154, 0.0721);
                half luminance = dot(rgb, luminanceCoeff);
                half3 grayscale = half3(luminance, luminance, luminance);

                // intensity < 1 → グレー寄り
                // intensity = 1 → 変化なし
                // intensity > 1 → 鮮やか (外挿)
                return lerp(grayscale, rgb, intensity);
            }
            void RGBtoRYGCBV(half R, half G, half B,
                out half r, out half y, out half g,
                out half c, out half b, out half v)
            {
                r = R * 0.5;
                g = G * 0.5;
                b = B * 0.5;
                y = (2.0 * R + 2.0 * G - B) / 6.0;
                c = (2.0 * G + 2.0 * B - R) / 6.0;
                v = (2.0 * B + 2.0 * R - G) / 6.0;
            }

            // RYGCBV → RGB 再合成
            half3 RYGCBVtoRGB(half r, half y, half g, half c, half b, half v)
            {
                half R = r + (2.0 * v + 2.0 * y - c) / 3.0;
                half G = g + (2.0 * y + 2.0 * c - v) / 3.0;
                half B = b + (2.0 * c + 2.0 * v - y) / 3.0;
                return half3(R, G, B);
            }
            // Blinn-Phong 拡散 + 鏡面反射
// N: 表面法線、V: 表面→カメラ、lightDir: 光源の進行方向 (ワールド)
            half CalcBlinnPhong(half3 N, half3 V, half3 lightDir,
                                half shininess, half diffuseness)
            {
                // 光源「に向かう」方向 (lightDir の逆)
                half3 L = normalize(-lightDir);
                // ハーフベクトル: 光源方向と視線方向の中間
                half3 H = normalize(V + L);

                // Diffuse: 法線と光源方向の一致度
                half NdotL = max(0.0, dot(N, L));

                // Specular: 法線とハーフベクトルの一致度を二乗してから shininess 乗
                half NdotH  = max(0.0, dot(N, H));
                half NdotH2 = NdotH * NdotH;            // 二乗で先にシャープに
                half kSpecular = pow(NdotH2, shininess);

                return kSpecular + NdotL * diffuseness;
            }
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.eyeVectorWS = normalize(posWS - GetCameraPositionWS());
                return OUT;
            }

          half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.positionHCS.xy / _ScreenParams.xy;
                float3 N = normalize(IN.normalWS);
                float3 I = normalize(IN.eyeVectorWS);

                // 6 チャンネル分の屈折ベクトル
                float3 refR = refract(I, N, 1.0 / _IorR);
                float3 refY = refract(I, N, 1.0 / _IorY);
                float3 refG = refract(I, N, 1.0 / _IorG);
                float3 refC = refract(I, N, 1.0 / _IorC);
                float3 refB = refract(I, N, 1.0 / _IorB);
                float3 refV = refract(I, N, 1.0 / _IorV);

                half3 color = half3(0, 0, 0);

                for (int i = 0; i < LOOP_COUNT; i++)
                {
                    float slide = (float)i / (float)LOOP_COUNT * 0.1;

                    // 6 ヶ所からそれぞれサンプル (.rgb 全部使う)
                    half3 sampleR = SampleSceneColor(screenUV + refR.xy * (_RefractPower + slide * 1.0) * _ChromaticAberration);
                    half3 sampleY = SampleSceneColor(screenUV + refY.xy * (_RefractPower + slide * 2.0) * _ChromaticAberration);
                    half3 sampleG = SampleSceneColor(screenUV + refG.xy * (_RefractPower + slide * 3.0) * _ChromaticAberration);
                    half3 sampleC = SampleSceneColor(screenUV + refC.xy * (_RefractPower + slide * 1.0) * _ChromaticAberration);
                    half3 sampleB = SampleSceneColor(screenUV + refB.xy * (_RefractPower + slide * 2.0) * _ChromaticAberration);
                    half3 sampleV = SampleSceneColor(screenUV + refV.xy * (_RefractPower + slide * 3.0) * _ChromaticAberration);

                    // 各サンプルを RYGCBV に分解して、担当チャンネルだけ取る
                    half cr, cy, cg, cc, cb, cv;

                    RGBtoRYGCBV(sampleR.r, sampleR.g, sampleR.b, cr, cy, cg, cc, cb, cv);
                    half finalR = cr;

                    RGBtoRYGCBV(sampleY.r, sampleY.g, sampleY.b, cr, cy, cg, cc, cb, cv);
                    half finalY = cy;

                    RGBtoRYGCBV(sampleG.r, sampleG.g, sampleG.b, cr, cy, cg, cc, cb, cv);
                    half finalG = cg;

                    RGBtoRYGCBV(sampleC.r, sampleC.g, sampleC.b, cr, cy, cg, cc, cb, cv);
                    half finalC = cc;

                    RGBtoRYGCBV(sampleB.r, sampleB.g, sampleB.b, cr, cy, cg, cc, cb, cv);
                    half finalB = cb;

                    RGBtoRYGCBV(sampleV.r, sampleV.g, sampleV.b, cr, cy, cg, cc, cb, cv);
                    half finalV = cv;

                    // 集めた 6 チャンネルを RGB に再合成
                    color += RYGCBVtoRGB(finalR, finalY, finalG, finalC, finalB, finalV);
                }

                color /= (float)LOOP_COUNT;
                color = SaturateCustom(color, _Saturation);

                // --- Blinn-Phong ライティングを加算 ---
                // I は「カメラ→表面」なので、視線 (V) は -I = 「表面→カメラ」
                half3 V = -I;
                half lighting = CalcBlinnPhong(N, V, _LightDir.xyz,_Shininess, _Diffuseness);
                color += lighting * _LightIntensity;
                float fresnelDot = abs(dot(I, N));
                float fresnel = pow(1.0 - fresnelDot, _FresnelPower);
                half3 finalRGB = color * _BaseColor.rgb;
                finalRGB += _FresnelColor.rgb * fresnel*_FresnelColor.a;
                return half4(finalRGB, _BaseColor.a);
            }


            ENDHLSL
        }
    }
}
