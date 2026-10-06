Shader "Custom/PortalMask"
{
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "Queue"="Geometry-1" 
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "PortalMaskPass"
            
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" }
        Pass
        {
            Name "HdrpPortalMask"
            Tags { "LightMode"="ShinzuiPortal" }
            Cull Off ZWrite On ZTest LEqual ColorMask 0
            Stencil { Ref 64 ReadMask 64 WriteMask 64 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            float4 Vert(float3 positionOS : POSITION) : SV_POSITION { return TransformWorldToHClip(TransformObjectToWorld(positionOS)); }
            float4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
