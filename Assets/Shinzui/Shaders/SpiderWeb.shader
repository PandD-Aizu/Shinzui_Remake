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
        [HideInInspector] _AlphaClip ("Legacy Alpha Clip", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "ForwardOnly" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fragment PUNCTUAL_SHADOW_LOW PUNCTUAL_SHADOW_MEDIUM PUNCTUAL_SHADOW_HIGH
            #pragma multi_compile_fragment DIRECTIONAL_SHADOW_LOW DIRECTIONAL_SHADOW_MEDIUM DIRECTIONAL_SHADOW_HIGH
            #pragma multi_compile_fragment AREA_SHADOW_MEDIUM AREA_SHADOW_HIGH

            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "HdrpObjectCommon.hlsl"
            #include "HdrpObjectLighting.hlsl"

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

            /// <summary>座標に応じた乱数を求める</summary>
            /// <param name="samplePosition">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            float Hash21(float2 samplePosition)
            {
                samplePosition = frac(samplePosition * float2(123.34, 456.21));
                samplePosition += dot(samplePosition, samplePosition + 45.32);
                return frac(samplePosition.x * samplePosition.y);
            }

            /// <summary>繊維に沿った濃淡を求める</summary>
            /// <param name="uv">評価に用いる入力値</param>
            /// <param name="shade">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            half FiberVariation(float2 uv, half shade)
            {
                float coordinate = uv.y * _NoiseScale;
                float cell = floor(coordinate);
                float phase = frac(coordinate);
                phase = phase * phase * (3.0 - 2.0 * phase);
                half variation = (half)lerp(
                    Hash21(float2(cell, shade * 37.0)),
                    Hash21(float2(cell + 1.0, shade * 37.0)), phase);
                variation = lerp(variation, 0.5h, saturate(fwidth(coordinate)));
                return lerp(1.0h - _NoiseStrength, 1.0h, variation);
            }

            /// <summary>細い糸の被覆率を求める</summary>
            /// <param name="acrossThread">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            half ThreadCoverage(float acrossThread)
            {
                float radius = abs(acrossThread * 2.0 - 1.0);
                float filterWidth = clamp(fwidth(acrossThread) * 2.0, 0.12, 0.7);
                return (half)(1.0 - smoothstep(1.0 - filterWidth, 1.0, radius));
            }

            /// <summary>糸の異方性拡散と鏡面反射を求める</summary>
            /// <param name="light">評価に用いる入力値</param>
            /// <param name="tangentWS">評価に用いる入力値</param>
            /// <param name="normalWS">評価に用いる入力値</param>
            /// <param name="viewDirectionWS">評価に用いる入力値</param>
            /// <param name="albedo">評価に用いる入力値</param>
            /// <param name="meshRenderingLayers">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            half3 EvaluateSilkLight(ShinzuiLight light, half3 tangentWS,
                half3 normalWS, half3 viewDirectionWS, half3 albedo,
                uint meshRenderingLayers)
            {

                /* リボンの向きでなく糸の軸を基準に反射を求める */
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

                return (scattering + reflected) * light.color;
            }

            /// <summary>頂点を画面座標へ変換する</summary>
            /// <param name="input">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz, false);
                output.uv = input.uv;
                output.color = input.color;
                output.fogFactor = 0;
                return output;
            }

            /// <summary>画素の色と被覆率を評価する</summary>
            /// <param name="input">評価に用いる入力値</param>
            /// <param name="facing">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
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
                half alpha = saturate(_BaseColor.a * input.color.a * fiber * ThreadCoverage(input.uv.x));
                half3 albedo = _BaseColor.rgb * input.color.rgb;
                uint meshRenderingLayers = GetMeshRenderingLayerMask();

                half3 color = 0;
                /* HDRPの可視光源で繊維の異方性反射を評価する */
                for (uint i = 0; i < _DirectionalLightCount + _PunctualLightCount; i++)
                {
                    ShinzuiLight light = ShinzuiGetLight(i, input.positionWS, normalWS, input.positionCS.xy);
                    color += EvaluateSilkLight(light, tangentWS, normalWS, viewDirectionWS, albedo, meshRenderingLayers);
                }

                /* 空の被覆領域に霧を描かないよう合成後にアルファを掛ける */
                color = ShinzuiFog(color * GetCurrentExposureMultiplier(), input.positionCS, input.positionWS);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
