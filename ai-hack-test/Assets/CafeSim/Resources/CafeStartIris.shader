Shader "Hidden/CafeSim/StartIris"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Aperture ("Screen Size, Radius, Feather", Vector) = (1, 1, 0, 1)
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

            float4 _Aperture;

            fixed4 frag(v2f_img input) : SV_Target
            {
                float distanceFromCenter = length((input.uv - .5) * _Aperture.xy);
                float opacity = smoothstep(_Aperture.z - _Aperture.w, _Aperture.z, distanceFromCenter);
                return fixed4(0, 0, 0, opacity);
            }
            ENDCG
        }
    }
}
