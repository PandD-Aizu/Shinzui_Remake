Shader "Hidden/Shinzui/HdrpMaskPack"
{
    SubShader
    {
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MetallicSource, _OcclusionSource, _GlossSource;
            float _MetallicValue, _SmoothnessValue, _HasMetallic;
            float4 frag(v2f_img i) : SV_Target
            {
                return float4(lerp(_MetallicValue, tex2D(_MetallicSource,i.uv).r, _HasMetallic),
                    tex2D(_OcclusionSource,i.uv).g, 1, tex2D(_GlossSource,i.uv).a * _SmoothnessValue);
            }
            ENDCG
        }
    }
}
