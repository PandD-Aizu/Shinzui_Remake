Shader "Shinzui/SpiderWeb"
{
    Properties
    {
        [HDR] _BaseColor ("Thread Color", Color) = (0.82, 0.86, 0.88, 0.78)
        [HDR] _HighlightColor ("Silk Reflection", Color) = (0.92, 0.96, 1.0, 1)
        _Smoothness ("Fiber Smoothness", Range(0, 1)) = 0.72
        _FresnelPower ("Glancing Reflection Power", Range(0.5, 8)) = 3.0
        _FresnelStrength ("Glancing Reflection Strength", Range(0, 4)) = 0.75
        _NoiseScale ("Fiber Noise Scale", Range(1, 80)) = 24
        _NoiseStrength ("Fiber Noise Strength", Range(0, 1)) = 0.18
        // Retained for existing materials. Coverage is continuous, never alpha-clipped.
        [HideInInspector] _AlphaClip ("Legacy Alpha Clip", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            // Transparent strands must sample the actual shadow map, not the
            // screen-space shadow of whichever opaque surface lies behind them.
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half3 tangentWS : TEXCOORD4;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
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
                samplePosition += dot(samplePosition, samplePosition + 45.32);
                return frac(samplePosition.x * samplePosition.y);
            }

            half FiberVariation(float2 uv, half shade)
            {
                float coordinate = uv.y * _NoiseScale;
                float cell = floor(coordinate);
                float phase = frac(coordinate);
                phase = phase * phase * (3.0 - 2.0 * phase);
                half variation = (half)lerp(
                    Hash21(float2(cell, shade * 37.0)),
                    Hash21(float2(cell + 1.0, shade * 37.0)), phase);
                // Unresolved variation approaches its mean instead of sparkling
                // as the camera moves across subpixel fibers.
                variation = lerp(variation, 0.5h, saturate(fwidth(coordinate)));
                return lerp(1.0h - _NoiseStrength, 1.0h, variation);
            }

            half ThreadCoverage(float acrossThread)
            {
                float radius = abs(acrossThread * 2.0 - 1.0);
                float filterWidth = clamp(fwidth(acrossThread) * 2.0, 0.12, 0.7);
                return (half)(1.0 - smoothstep(1.0 - filterWidth, 1.0, radius));
            }

            half3 EvaluateSilkLight(Light light, half3 tangentWS,
                half3 normalWS, half3 viewDirectionWS, half3 albedo,
                uint meshRenderingLayers)
            {
                #if defined(_LIGHT_LAYERS)
                    if (!IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                        return 0.0h;
                #endif

                // A thin cylinder receives light around its whole circumference.
                // Its response follows the strand axis, rather than the changing
                // normal of the camera-facing ribbon used to draw it.
                half tangentLight = dot(tangentWS, light.direction);
                half cylinderDiffuse = sqrt(saturate(1.0h - tangentLight * tangentLight));
                half3 halfVector = SafeNormalize(light.direction + viewDirectionWS);
                half tangentHalf = dot(tangentWS, halfVector);
                half sinHalfAngle = sqrt(saturate(1.0h - tangentHalf * tangentHalf));
                half fiberSpecular = pow(sinHalfAngle, exp2(4.0h + _Smoothness * 5.0h));

                half grazing = pow(1.0h - saturate(abs(dot(normalWS, viewDirectionWS))),
                    _FresnelPower);
                half reflection = (0.22h + grazing * _FresnelStrength) * _Smoothness;
                half3 scattering = albedo * cylinderDiffuse * 0.34h;
                half3 reflected = _HighlightColor.rgb * fiberSpecular * reflection;

                // Both lobes are illuminated and shadowed. There is no constant
                // Fresnel emission or ambient brightness floor in an unlit tunnel.
                return (scattering + reflected) * light.color *
                    (light.distanceAttenuation * light.shadowAttenuation);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz, false);
                output.uv = input.uv;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 tangentWS = SafeNormalize(input.tangentWS);
                half3 ribbonNormalWS = SafeNormalize(input.normalWS) * IS_FRONT_VFACE(facing, 1.0h, -1.0h);
                half3 acrossWS = SafeNormalize(cross(tangentWS, ribbonNormalWS));
                half crossSection = (half)(input.uv.x * 2.0 - 1.0);
                half3 normalWS = SafeNormalize(ribbonNormalWS *
                    sqrt(saturate(1.0h - crossSection * crossSection)) + acrossWS * crossSection);
                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                half fiber = FiberVariation(input.uv, input.color.r);
                // Vertex alpha conserves coverage when the mesh widens a very
                // fine strand to a stable subpixel footprint. Base alpha remains
                // the single opacity control used by the existing burn fade.
                half alpha = saturate(_BaseColor.a * input.color.a * fiber * ThreadCoverage(input.uv.x));
                half3 albedo = _BaseColor.rgb * input.color.rgb;
                uint meshRenderingLayers = GetMeshRenderingLayer();

                // The URP 17.5 cluster-loop macro requires this exact local name
                // and these fields to address its screen tile and depth slice.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half4 shadowMask = unity_ProbesOcclusion;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS),
                    input.positionWS, shadowMask);
                half3 color = EvaluateSilkLight(mainLight, tangentWS, normalWS,
                    viewDirectionWS, albedo, meshRenderingLayers);

                // Evaluate punctual lights per fragment even in the vertex-light
                // quality mode; an interpolated vertex lobe misses a narrow torch
                // beam on a long thread. The pipeline still controls light limits.
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX) || USE_CLUSTER_LIGHT_LOOP
                    #if USE_CLUSTER_LIGHT_LOOP
                        [loop] for (uint lightIndex = 0;
                            lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS);
                            ++lightIndex)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                            color += EvaluateSilkLight(light, tangentWS, normalWS,
                                viewDirectionWS, albedo, meshRenderingLayers);
                        }
                    #endif

                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                        color += EvaluateSilkLight(light, tangentWS, normalWS,
                            viewDirectionWS, albedo, meshRenderingLayers);
                    LIGHT_LOOP_END
                #endif

                // Fog is mixed before premultiplication so empty fiber coverage
                // never deposits a rectangular patch of fog over the background.
                color = MixFog(color, input.fogFactor);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
