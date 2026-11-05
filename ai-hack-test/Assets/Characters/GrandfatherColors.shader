Shader "Cottage/Grandfather Vertex Colors"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                half3 normal : TEXCOORD1;
                half fog : TEXCOORD2;
                half3 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.position.xyz);
                output.position = position.positionCS;
                output.worldPosition = position.positionWS;
                output.normal = TransformObjectToWorldNormal(input.normal);
                output.fog = ComputeFogFactor(position.positionCS.z);
                output.color = input.color.rgb;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normal);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadow = ComputeScreenPos(TransformWorldToHClip(input.worldPosition));
                #else
                    float4 shadow = TransformWorldToShadowCoord(input.worldPosition);
                #endif
                Light sun = GetMainLight(shadow);
                half diffuse = saturate(dot(normal, sun.direction));
                half3 lighting = max(SampleSH(normal), 0.32h) + sun.color * diffuse * sun.shadowAttenuation;
                return half4(MixFog(input.color * lighting, input.fog), 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
