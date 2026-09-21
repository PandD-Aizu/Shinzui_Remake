Shader "Shinzui/BlackHoleBody"
{
    Properties
    {
        _BaseColor ("Body Color", Color) = (0.014, 0.021, 0.026, 1)
        [HDR] _DarkGlowColor ("Unlit Glow Color", Color) = (1, 1, 1, 1)
        _DarkGlowIntensity ("Unlit Glow Intensity", Range(0, 1)) = 0.08
        _EdgeSoftness ("Silhouette Softness", Range(0.05, 0.8)) = 0.3
        _EdgeSway ("Silhouette Sway", Range(0, 0.05)) = 0.018
        _LightFadeStart ("Light Fade Start", Range(0, 1)) = 0.02
        _LightFadeEnd ("Light Fully Invisible", Range(0.01, 5)) = 0.2
    }

    SubShader
    {
        // Keep visible body pixels in the opaque scene copy used by the eye lens
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }

        Pass
        {
            Name "LightReactiveBody"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite On

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

            // Sample shadows at the body instead of the opaque background depth
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _DarkGlowColor;
                float _DarkGlowIntensity;
                float _EdgeSoftness, _EdgeSway;
                float _LightFadeStart, _LightFadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fog : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            /// <summary>
            /// Transform body vertices and carry lighting coordinates
            /// </summary>
            /// <param name="input">Object space vertex</param>
            /// <returns>Clip position and world space lighting data</returns>
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                // Small continuous waves loosen the silhouette without moving the eyes
                float wave = sin(output.positionWS.y * 9.0 + output.positionWS.x * 5.0 + _Time.y * 1.1);
                wave *= cos(output.positionWS.z * 7.0 - output.positionWS.y * 4.0 - _Time.y * 0.8);
                output.positionWS += output.normalWS * (_EdgeSway * wave);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            /// <summary>
            /// Evaluate illumination within the light range, cone and shadow
            /// </summary>
            /// <param name="light">URP light at the fragment</param>
            /// <param name="meshLayers">Body rendering layers</param>
            /// <returns>Incident light color</returns>
            half3 IncidentLight(Light light, uint meshLayers)
            {
                #if defined(_LIGHT_LAYERS)
                    if (!IsMatchingLightLayer(light.layerMask, meshLayers)) return 0;
                #endif

                return light.color * (light.distanceAttenuation * light.shadowAttenuation);
            }

            /// <summary>
            /// Remove illuminated body coverage while retaining the unlit silhouette
            /// </summary>
            /// <param name="input">Interpolated body fragment</param>
            /// <returns>Visible body color after lighting and fog</returns>
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                uint meshLayers = GetMeshRenderingLayer();
                half4 shadowMask = unity_ProbesOcclusion;

                // Ambient light shades the body; local and directional lights dissolve it
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS),
                    input.positionWS, shadowMask);
                half3 incident = IncidentLight(mainLight, meshLayers);
                half3 illumination = incident;
                half3 diffuse = incident * saturate(dot(normalWS, mainLight.direction));

                // Per-pixel evaluation keeps narrow flashlight cones accurate at every quality
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX) || USE_CLUSTER_LIGHT_LOOP
                    #if USE_CLUSTER_LIGHT_LOOP
                        [loop] for (uint lightIndex = 0;
                            lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS);
                            ++lightIndex)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                            incident = IncidentLight(light, meshLayers);
                            illumination += incident;
                            diffuse += incident * saturate(dot(normalWS, light.direction));
                        }
                    #endif

                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                        incident = IncidentLight(light, meshLayers);
                        illumination += incident;
                        diffuse += incident * saturate(dot(normalWS, light.direction));
                    LIGHT_LOOP_END
                #endif

                // Ignore surface orientation for dissolving so inner folds cannot fill the hole
                float brightness = max(illumination.r, max(illumination.g, illumination.b));
                float darkness = 1.0 - smoothstep(_LightFadeStart,
                    max(_LightFadeEnd, _LightFadeStart + 0.001), brightness);
                float opacity = _BaseColor.a * darkness;

                // Feather grazing edges with slow drifting variation rather than a hard outline
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facing = saturate(dot(normalWS, viewDirectionWS));
                float edgeWave = 0.5 + 0.5 * sin(input.positionWS.y * 16.0 +
                    sin(input.positionWS.x * 11.0 - _Time.y) + _Time.y * 1.3);
                opacity *= smoothstep(0.02, _EdgeSoftness * lerp(0.7, 1.3, edgeWave), facing);

                // Dither only the transition to preserve depth and the existing scene refraction
                const float thresholds[16] = {
                    0.5, 8.5, 2.5, 10.5, 12.5, 4.5, 14.5, 6.5,
                    3.5, 11.5, 1.5, 9.5, 15.5, 7.5, 13.5, 5.5
                };
                uint2 pixel = (uint2)input.positionCS.xy & 3u;
                clip(opacity - thresholds[pixel.y * 4u + pixel.x] / 16.0);

                half3 color = MixFog(_BaseColor.rgb * (SampleSH(normalWS) + diffuse), input.fog);

                // A faint white glow reveals the unlit body and fades with its coverage
                // Keep the supernatural glow visible through the stage's dense black fog
                half rim = 1.0h - facing;
                half glowShape = 0.55h + 0.45h * rim * rim;
                color += _DarkGlowColor.rgb * (_DarkGlowIntensity * darkness * glowShape);

                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
