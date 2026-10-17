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
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" }
        Pass
        {
            Name "HdrpPortalProjection"
            Tags { "LightMode"="ShinzuiPortal" }
            Cull Off ZWrite Off ZTest LEqual
            Stencil { Ref 64 ReadMask 64 WriteMask 0 Comp Equal Pass Keep }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            #define SHADOW_LOW
            #define AREA_SHADOW_LOW
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/SkyUtils.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/AtmosphericScattering/AtmosphericScattering.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float _PortalExposureMultiplier;
            float4 _PortalUvTransform;
            float _PortalFogCorrection;
            float4 Vert(float3 positionOS : POSITION) : SV_POSITION { return TransformWorldToHClip(TransformObjectToWorld(positionOS)); }
            float4 Frag(float4 positionCS : SV_POSITION) : SV_Target
            {
                float2 uv = positionCS.xy * _ScreenSize.zw;
                uv = uv * _PortalUvTransform.xy + _PortalUvTransform.zw;
                float3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb * _PortalExposureMultiplier * GetCurrentExposureMultiplier();
                PositionInputs posInput = GetPositionInput(positionCS.xy, _ScreenSize.zw, positionCS.z, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
                float3 view = GetWorldSpaceNormalizeViewDir(posInput.positionWS);
                float3 fogColor = 0, fogOpacity = 0;
                // The clipped portal volume omits scattering between the receiving camera and this plane
                if (_PortalFogCorrection > 0.5 && _EnableVolumetricFog != 0)
                    EvaluateAtmosphericScattering(posInput, view, fogColor, fogOpacity);
                return float4(color * (1 - fogOpacity) + fogColor, 1);
            }
            ENDHLSL
        }
    }
}
