Shader "Rhizomode/CrtSpectrum"
{
    Properties
    {
        _Color ("Color", Color) = (0,0,0,0)
        _BarColor ("Bar Color", Color) = (0.2, 0.6, 1.0, 1.0)
        _Intensity ("Intensity", Range(0, 4)) = 1
        _Delta("Bar Width", Range(0, 0.1)) = 0.01
    }

    SubShader
    {
        Lighting Off
        Blend One Zero

        Pass
        {
            Name "Update"
            CGPROGRAM
            #include "UnityCustomRenderTexture.cginc"
            #include "RhizomodeAudioSpectrum.hlsl"

            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            #pragma target 3.0

            fixed4 _Color;
            fixed4 _BarColor;
            fixed _Intensity;
            fixed _Delta;

            float gaussian_weight(float x, float sigma)
            {
                return exp(-x * x / (2.0 * sigma * sigma));
            }

            float smoothSpectrum(int index, float sigma)
            {
                const int radius = 2;
                float weightSum = 0;
                float sum = 0;
                for (int i = -radius; i <= radius; i++)
                {
                    float weight = gaussian_weight(i, sigma);
                    sum += RhizomodeAudioSpectrum(
                        clamp(index + i, 0, _RhizomodeAudioSpectrumSize - 1)) * weight;
                    weightSum += weight;
                }
                return sum / weightSum;
            }

            float4 frag(v2f_customrendertexture IN) : COLOR
            {
                const float2 uv = IN.localTexcoord.xy;
                uint ind = floor(uv.x * _RhizomodeAudioSpectrumSize);

                float sigma = exp(uv.x * 8.0) - 1.0;
                float data = smoothSpectrum(ind, max(sigma, 0.5));
                float v = step(uv.y, data) * step(data, uv.y + _Delta) * _Intensity;
                return _Color + _BarColor * v;
            }
            ENDCG
        }
    }
}
