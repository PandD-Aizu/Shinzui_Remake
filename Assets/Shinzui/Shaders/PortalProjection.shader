Shader "Custom/PortalProjection"
{
    Properties
    {
        _MainTex ("Portal Texture", 2D) = "white" {}
        _PortalExposureMultiplier ("Portal Exposure Multiplier", Float) = 1.0
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "Queue"="Geometry" 
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "PortalProjectionPass"
            
            Cull Off
            ZWrite Off
            ZTest LEqual

            Stencil
            {
                Ref 1
                Comp Equal
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 screenPos    : TEXCOORD0;
            };

            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            float _PortalExposureMultiplier;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / input.screenPos.w;
                half4 color = _MainTex.Sample(sampler_MainTex, uv);
                color.rgb *= _PortalExposureMultiplier;
                return color;
            }
            ENDHLSL
        }
    }
}
