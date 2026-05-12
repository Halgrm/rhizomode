Shader "Rhizomode/CrtWaveform"
{
    Properties
    {
        _Color ("Color", Color) = (0,0,0,0)
        _LineColor ("Line Color", Color) = (0.3, 0.8, 1.0, 1.0)
        _Intensity ("Intensity", Range(0, 4)) = 1
        _Delta("Line Width", Range(0, 0.1)) = 0.01
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
            #include "RhizomodeAudioWaveform.hlsl"

            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            #pragma target 3.0

            fixed4 _Color;
            fixed4 _LineColor;
            fixed _Intensity;
            fixed _Delta;

            float4 frag(v2f_customrendertexture IN) : COLOR
            {
                const float2 uv = IN.localTexcoord.xy;
                uint ind = floor(uv.x * _RhizomodeAudioWaveformSize);

                float data = RhizomodeAudioWaveform(ind) * 0.5 + 0.5;
                float v = step(uv.y, data) * step(data, uv.y + _Delta) * _Intensity;
                return _Color + _LineColor * v;
            }
            ENDCG
        }
    }
}
