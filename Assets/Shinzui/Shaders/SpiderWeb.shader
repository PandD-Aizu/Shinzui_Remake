Shader "Shinzui/SpiderWeb"
{
    Properties
    {
        [HDR] _BaseColor ("Thread Color", Color) = (0.82, 0.88, 0.9, 0.78)
        [HDR] _HighlightColor ("Glancing Highlight", Color) = (1.15, 1.3, 1.45, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.88
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.4
        _FresnelStrength ("Fresnel Strength", Range(0, 4)) = 1.35
        _NoiseScale ("Fiber Noise Scale", Range(1, 80)) = 24
        _NoiseStrength ("Fiber Noise Strength", Range(0, 1)) = 0.28
        _AlphaClip ("Alpha Clip", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest+20"
            "RenderType" = "TransparentCutout"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
                half fogFactor : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _HighlightColor;
                half _Smoothness;
                half _FresnelPower;
                half _FresnelStrength;
                half _NoiseScale;
                half _NoiseStrength;
                half _AlphaClip;
            CBUFFER_END

            float Hash21(float2 samplePosition)
            {
                samplePosition = frac(samplePosition * float2(123.34, 456.21));
                samplePosition += dot(
                    samplePosition,
                    samplePosition + 45.32);
                return frac(samplePosition.x * samplePosition.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = input.uv;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                if (!isFrontFace)
                {
                    normalWS = -normalWS;
                }

                half3 viewDirectionWS =
                    SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight =
                    GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half halfLambert = ndotl * 0.5h + 0.5h;

                half3 halfVector =
                    SafeNormalize(mainLight.direction + viewDirectionWS);
                half specularPower = exp2(4.0h + _Smoothness * 8.0h);
                half specular = pow(
                    saturate(dot(normalWS, halfVector)),
                    specularPower) * _Smoothness;

                half fresnel = pow(
                    1.0h - saturate(abs(dot(normalWS, viewDirectionWS))),
                    _FresnelPower) * _FresnelStrength;

                float fiberNoise = Hash21(
                    float2(floor(input.uv.y * _NoiseScale), input.color.r * 37.0));
                half fiber = lerp(
                    1.0h - _NoiseStrength,
                    1.0h,
                    (half)fiberNoise);

                half alpha = _BaseColor.a * input.color.a * fiber;
                clip(alpha - _AlphaClip);

                half attenuation =
                    mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half3 diffuse = _BaseColor.rgb * input.color.rgb *
                                (0.18h + halfLambert * mainLight.color * attenuation);
                half3 highlight = _HighlightColor.rgb *
                                  (fresnel + specular * attenuation);
                half3 color = diffuse + highlight;
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
