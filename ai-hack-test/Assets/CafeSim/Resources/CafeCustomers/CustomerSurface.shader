Shader "Cafe/Customer Surface"
{
    Properties
    {
        _BaseMap ("Original color", 2D) = "white" {}
        _BaseColor ("Base color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0, 2)) = 1
        [NoScaleOffset] _MetallicMap ("Metallic", 2D) = "black" {}
        [NoScaleOffset] _RoughnessMap ("Roughness", 2D) = "white" {}
        [NoScaleOffset] _ClothRegions ("Shirt / trousers", 2D) = "black" {}
        _ShirtColor ("Shirt color", Color) = (1, 1, 1, 1)
        _TrouserColor ("Trouser color", Color) = (1, 1, 1, 1)
        _SkinColor ("Skin color", Color) = (1, 1, 1, 1)
        _SkinReference ("Original skin brightness", Range(0, 1)) = .5
        _Recolor ("Use alternate colors", Range(0, 1)) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _Cutoff ("Cutoff", Float) = .5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #define _NORMALMAP 1
            #define InitializeStandardLitSurfaceData InitializeCustomerBaseSurface
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #undef InitializeStandardLitSurfaceData
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_MetallicMap); SAMPLER(sampler_MetallicMap);
            TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_ClothRegions); SAMPLER(sampler_ClothRegions);
            half4 _ShirtColor, _TrouserColor, _SkinColor;
            half _SkinReference, _Recolor;

            void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
            {
                InitializeCustomerBaseSurface(uv, surface);
                surface.metallic = SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, uv).r;
                surface.smoothness = 1 - SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, uv).r;
                if (_Recolor < .5) return;

                // The source atlas already paints the skin. Keep eyes, hair and accessories in their original colors.
                half3 source = surface.albedo;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                source = LinearToSRGB(source);
                #endif
                half skin = smoothstep(.035, .09, source.r - source.g)
                          * smoothstep(.02, .065, source.g - source.b)
                          * smoothstep(.20, .34, source.r);
                half brightness = Luminance(source);
                half2 cloth = SAMPLE_TEXTURE2D(_ClothRegions, sampler_ClothRegions, uv).rg;
                half3 shirt = _ShirtColor.rgb * lerp(.62, 1.16, brightness);
                half3 trousers = _TrouserColor.rgb * lerp(.65, 1.12, brightness);
                surface.albedo = lerp(surface.albedo, shirt, cloth.r * (1 - skin) * .93);
                surface.albedo = lerp(surface.albedo, trousers, cloth.g * (1 - skin) * .92);
                half skinDetail = clamp(brightness / _SkinReference, .50, 1.22);
                surface.albedo = lerp(surface.albedo, _SkinColor.rgb * skinDetail, skin);
                surface.metallic *= 1 - skin;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
