// Worn rubber-stamp ink. Drawn over a freshly inked stamp in paper colour: blotchy gaps where
// the ink did not take, a grainy edge, and lighter coverage toward one corner (uneven pressure).
// The halo that makes the ink look soaked in is drawn underneath by CafeHud.Stamp.
Shader "CafeSim/StampWear"
{
    Properties
    {
        _MainTex ("Unused", 2D) = "white" {}
        _PaperColor ("Paper", Color) = (0.984, 0.965, 0.91, 1)
        _Size ("Size (px)", Vector) = (78, 60, 0, 0)
        _Amount ("Wear", Range(0, 1)) = 0.3
        _Seed ("Seed", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "CafeNoise.cginc"

            fixed4 _PaperColor;
            float4 _Size;
            float _Amount, _Seed;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 px = i.uv * _Size.xy + _Seed * 53.0;
                float blotch = CafeFbm(px * 0.11);
                float grit = CafeHash(floor(px * 1.6));
                float n = blotch * 0.72 + grit * 0.28;

                // Light pressure on one side: more paper shows through there.
                float pressure = saturate(i.uv.x * 0.65 + (1.0 - i.uv.y) * 0.35);
                float threshold = 1.0 - _Amount * (0.45 + 0.9 * pressure);
                float gap = smoothstep(threshold - 0.035, threshold + 0.035, n);
                return fixed4(_PaperColor.rgb, gap * 0.95);
            }
            ENDCG
        }
    }
}
