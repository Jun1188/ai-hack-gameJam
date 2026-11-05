// Thermal-receipt paper for the closing settlement: soft mottling, horizontal fibers,
// sparse ink specks and slightly aged side edges. Drawn by IMGUI (Graphics.DrawTexture),
// so it is a plain unlit shader. _Size is the drawn size in layout pixels so the grain
// keeps the same scale whatever height the receipt has.
Shader "CafeSim/ReceiptPaper"
{
    Properties
    {
        _MainTex ("Unused", 2D) = "white" {}
        _PaperColor ("Paper", Color) = (0.984, 0.965, 0.91, 1)
        _FiberColor ("Fiber", Color) = (0.86, 0.82, 0.72, 1)
        _InkColor ("Speck ink", Color) = (0.11, 0.23, 0.36, 1)
        _Size ("Size (px)", Vector) = (384, 660, 0, 0)
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

            fixed4 _PaperColor, _FiberColor, _InkColor;
            float4 _Size;
            float _Seed;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 px = i.uv * _Size.xy + _Seed * 37.0;

                float mottle = CafeFbm(px * 0.03);                                 // large soft clouds
                float fibers = CafeFbm(float2(px.x * 0.015, px.y * 0.42));         // long horizontal fibers
                float grain = CafeHash(floor(px * 1.4)) - 0.5;                     // fine tooth
                float speck = step(0.998, CafeHash(floor(px * 0.55) + 7.0));       // rare dark dots

                float3 c = _PaperColor.rgb;
                c *= 1.0 - 0.04 * (mottle - 0.5) * 2.0;
                c = lerp(c, _FiberColor.rgb, 0.12 * smoothstep(0.6, 0.88, fibers));
                c += grain * 0.01;
                c = lerp(c, _InkColor.rgb, speck * 0.16);

                // Side edges aged a little warmer and darker, as a receipt handled all day.
                float side = min(i.uv.x, 1.0 - i.uv.x) * _Size.x;
                float age = 1.0 - smoothstep(0.0, 22.0, side);
                c = lerp(c, c * float3(0.93, 0.88, 0.78), age * 0.55);

                return fixed4(saturate(c), 1);
            }
            ENDCG
        }
    }
}
