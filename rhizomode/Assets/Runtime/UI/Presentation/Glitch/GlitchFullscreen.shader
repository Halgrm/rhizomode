Shader "Rhizomode/GlitchFullscreen"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _GlitchAmount ("Glitch Amount", Range(0, 1)) = 0
        _TimeSeed ("Time Seed", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _GlitchAmount;
            float _TimeSeed;

            float Hash(float value)
            {
                return frac(sin(value * 12.9898) * 43758.5453);
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                float amount = saturate(_GlitchAmount);
                float2 uv = input.uv;

                if (amount <= 0.0)
                    return tex2D(_MainTex, uv);

                float band = floor(uv.y * 24.0);
                float seed = band + floor(_TimeSeed * 4.0);
                float hash = Hash(seed);
                if (hash > 0.85)
                    uv.x += amount * 0.10 * hash;

                float offset = amount * 0.02;
                fixed4 r = tex2D(_MainTex, uv + float2(offset, 0.0));
                fixed4 g = tex2D(_MainTex, uv);
                fixed4 b = tex2D(_MainTex, uv - float2(offset, 0.0));

                return fixed4(r.r, g.g, b.b, 1.0);
            }
            ENDCG
        }
    }
}
