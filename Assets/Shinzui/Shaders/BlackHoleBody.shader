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
        /* 屈折前に影付きの身体を描き色ピラミッドへ含める */
        Tags { "RenderPipeline" = "HDRenderPipeline" "Queue" = "Transparent-250" "RenderType" = "TransparentCutout" }

        HLSLINCLUDE
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fragment PUNCTUAL_SHADOW_LOW PUNCTUAL_SHADOW_MEDIUM PUNCTUAL_SHADOW_HIGH
            #pragma multi_compile_fragment DIRECTIONAL_SHADOW_LOW DIRECTIONAL_SHADOW_MEDIUM DIRECTIONAL_SHADOW_HIGH
            #pragma multi_compile_fragment AREA_SHADOW_MEDIUM AREA_SHADOW_HIGH

            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "HdrpObjectCommon.hlsl"
            #include "HdrpObjectLighting.hlsl"

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
            /// <summary>頂点を画面座標へ変換する</summary>
            /// <param name="input">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                float3 absolutePositionWS = GetAbsolutePositionWS(output.positionWS);
                float wave = sin(absolutePositionWS.y * 9.0 + absolutePositionWS.x * 5.0 + _Time.y * 1.1);
                wave *= cos(absolutePositionWS.z * 7.0 - absolutePositionWS.y * 4.0 - _Time.y * 0.8);
                output.positionWS += output.normalWS * (_EdgeSway * wave);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fog = 0;
                return output;
            }
            /// <summary>画素の色と被覆率を評価する</summary>
            /// <param name="input">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normalWS = normalize(input.normalWS);
                half3 illumination = 0;
                half3 diffuse = 0;
                /* 法線によらない入射光で消失を判定する */
                for (uint i = 0; i < _DirectionalLightCount + _PunctualLightCount; i++)
                {
                    ShinzuiLight light = ShinzuiGetLight(i, input.positionWS, normalWS, input.positionCS.xy);
                    /* 100ルクスを基準に旧消失しきい値へ正規化する */
                    illumination += light.color * 0.01;
                    diffuse += light.color * saturate(dot(normalWS, light.direction));
                }

                float brightness = max(illumination.r, max(illumination.g, illumination.b));
                float darkness = 1.0 - smoothstep(_LightFadeStart,
                    max(_LightFadeEnd, _LightFadeStart + 0.001), brightness);
                float opacity = _BaseColor.a * darkness;

                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facing = saturate(dot(normalWS, viewDirectionWS));
                float3 absolutePositionWS = GetAbsolutePositionWS(input.positionWS);
                float edgeWave = 0.5 + 0.5 * sin(absolutePositionWS.y * 16.0 +
                    sin(absolutePositionWS.x * 11.0 - _Time.y) + _Time.y * 1.3);
                opacity *= smoothstep(0.02, _EdgeSoftness * lerp(0.7, 1.3, edgeWave), facing);

                /* 境界の被覆率をディザで表し深度への書込みを維持する */
                const float thresholds[16] = {
                    0.5, 8.5, 2.5, 10.5, 12.5, 4.5, 14.5, 6.5,
                    3.5, 11.5, 1.5, 9.5, 15.5, 7.5, 13.5, 5.5
                };
                uint2 pixel = (uint2)input.positionCS.xy & 3u;
                clip(opacity - thresholds[pixel.y * 4u + pixel.x] / 16.0);

                half3 color = ShinzuiFog(_BaseColor.rgb * diffuse * GetCurrentExposureMultiplier(), input.positionCS, input.positionWS);

                half rim = 1.0h - facing;
                half glowShape = 0.55h + 0.45h * rim * rim;
                color += _DarkGlowColor.rgb * (_DarkGlowIntensity * darkness * glowShape);

                return half4(color, 1);
            }
        ENDHLSL
        Pass
        {
            Name "LightReactiveBody"
            Tags { "LightMode" = "ForwardOnly" }
            Cull Back
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
    Fallback Off
}
