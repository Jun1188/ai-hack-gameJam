Shader "Cottage/Sunset Sky"
{
    Properties
    {
        _SunDirection ("Sun direction", Vector) = (0, 0.1, -1, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _SunDirection;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(float4 vertex : POSITION)
            {
                Varyings output;
                output.position = TransformObjectToHClip(vertex.xyz);
                output.direction = vertex.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float elevation = saturate(direction.y);
                float alignment = saturate(dot(direction, normalize(_SunDirection.xyz)));
                float3 horizon = float3(0.76, 0.28, 0.14);
                float3 zenith = float3(0.09, 0.15, 0.30);
                float3 sky = lerp(horizon, zenith, smoothstep(0.0, 0.57, elevation));
                sky *= lerp(0.54, 1.0, smoothstep(-0.35, 0.04, direction.y));
                sky += float3(0.64, 0.29, 0.065) * pow(alignment, 16.0);
                sky += float3(0.50, 0.26, 0.065) * pow(alignment, 300.0);
                sky += float3(2.5, 1.4, 0.48) * smoothstep(0.99955, 0.99968, alignment);
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
}
