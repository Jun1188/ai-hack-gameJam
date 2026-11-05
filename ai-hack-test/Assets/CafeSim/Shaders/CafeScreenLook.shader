// Full-screen "afternoon cafe" look, run by a URP Full Screen Pass renderer feature on PC_Renderer.
// Warm grade with honey-coloured shadows, gentle contrast, a warm vignette and fine film grain.
// The soft glow around lamps and windows comes from URP Bloom on the scene's Cafe Look Volume.
// Driven only by global floats that CafeScreenLook sets; with _CafeLookStrength = 0 (every scene
// without that component) the pass copies the image through unchanged.
Shader "Hidden/CafeSim/ScreenLook"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "CafeScreenLook"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _CafeLookStrength, _CafeLookWarmth, _CafeLookSaturation, _CafeLookContrast;
            float _CafeLookVignette, _CafeLookGrain;

            float Hash(float2 p)
            {
                p = frac(p * float2(443.897, 441.423));
                p += dot(p, p.yx + 19.19);
                return frac((p.x + p.y) * p.x);
            }

            float3 Fetch(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb; }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float3 src = Fetch(uv);
                if (_CafeLookStrength <= 0.001)
                    return half4(src, 1);

                // The lamp glow is URP's Bloom (multi-resolution, so it stays smooth), driven by
                // CafeScreenLook.glow; this pass only grades the already-bloomed image.
                float3 c = src;

                // Grade in a gamma-like space so the controls behave evenly.
                float3 g = pow(max(c, 0), 1.0 / 2.2);
                g = (g - 0.5) * _CafeLookContrast + 0.5;
                float luma = dot(g, float3(0.299, 0.587, 0.114));
                g = lerp(luma.xxx, g, _CafeLookSaturation);
                g *= lerp(float3(1, 1, 1), float3(1.05, 1.0, 0.9), _CafeLookWarmth);
                float shadow = saturate(1.0 - luma);
                g += shadow * shadow * float3(0.035, 0.018, 0.0) * _CafeLookWarmth;   // honey shadows

                // Warm vignette, aspect-corrected.
                float2 d = uv - 0.5;
                d.x *= _BlitTexture_TexelSize.z / max(_BlitTexture_TexelSize.w, 1.0);   // width / height
                float v = smoothstep(0.95, 0.3, length(d));
                g = lerp(g * float3(0.62, 0.5, 0.42), g, lerp(1.0, v, _CafeLookVignette));

                // Fine film grain. It changes 12 times a second rather than every frame, and fades out in
                // bright areas so lamps and windows stay clean instead of shimmering.
                float tick = floor(_Time.y * 12.0);
                float n = Hash(uv * _BlitTexture_TexelSize.zw + frac(tick * 0.618) * 113.0) - 0.5;
                g += n * _CafeLookGrain * 0.045 * (1.0 - smoothstep(0.45, 0.9, luma));

                float3 graded = pow(saturate(g), 2.2);
                return half4(lerp(src, graded, _CafeLookStrength), 1);
            }
            ENDHLSL
        }
    }
}
