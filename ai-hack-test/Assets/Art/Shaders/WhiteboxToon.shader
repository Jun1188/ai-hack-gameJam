Shader "GrandpaRobot/WhiteboxToon"
{
    Properties { _BaseColor("Color",Color)=(1,1,1,1) _BaseMap("Base color texture",2D)="white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _BaseMap_ST;
            CBUFFER_END
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o; }
            half4 Frag(Varyings i):SV_Target
            {
                Light light=GetMainLight();
                half n=dot(normalize(i.normalWS),light.direction);
                half band=n>.55?1:(n>.05?.78:.52);
                half4 color=_BaseColor*SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                return half4(color.rgb*band,color.a);
            }
            ENDHLSL
        }
    }
}
