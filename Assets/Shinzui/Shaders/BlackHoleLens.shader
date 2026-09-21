Shader "Shinzui/BlackHoleLens"
{
    Properties
    {
        _PullStrength ("Inward Pull", Range(0, 4)) = 2.6
        _TwistStrength ("Spiral Distortion", Range(-1.5, 1.5)) = 0.65
        _RadiusScale ("Distortion Radius", Range(0.5, 1.3)) = 1
        _FlowSpeed ("Suction Wave Speed", Range(0, 5)) = 1.6
        _PulseStrength ("Suction Wave Strength", Range(0, 0.5)) = 0.22
        _Opacity ("Distortion Blend", Range(0, 1)) = 1
        [HideInInspector] _LensSize ("Lens Plane Size", Vector) = (1.95, 1.6, 0, 0)
        [HideInInspector] _EyeA ("Eye A: XY, Radius, Phase", Vector) = (0.225, -0.015, 0.70, 0.2)
        [HideInInspector] _EyeB ("Eye B: XY, Radius, Phase", Vector) = (-0.225, 0.02, 0.74, 2.1)
    }

    SubShader
    {
        // One field for both eyes avoids overlapping quads erasing each other's rings.
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" "RenderType" = "Transparent" }
        Pass
        {
            Name "SurroundingGravitationalLens"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _LensSize, _EyeA, _EyeB;
                float _PullStrength, _TwistStrength, _RadiusScale;
                float _FlowSpeed, _PulseStrength, _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 planePosition : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.planePosition = input.positionOS.xy * _LensSize.xy;
                return output;
            }

            float2 PullField(float2 planePoint, float4 eye, float direction, out float coverage)
            {
                float2 delta = planePoint - eye.xy;
                float radius = max(eye.z * _RadiusScale, 0.001);
                float radial = length(delta) / radius;
                float falloff = saturate(1.0 - radial);
                // Increasing time sends each pressure crest from the outside toward the eye.
                float wave = radial * 14.0 + _Time.y * _FlowSpeed * 3.0 + eye.w;
                float suction = _PullStrength * falloff * falloff * (1.0 + _PulseStrength * sin(wave));
                float twist = direction * _TwistStrength * (0.8 + 0.2 * cos(wave + eye.w));
                coverage = 1.0 - smoothstep(0.55, 1.0, radial);
                // Sample farther OUT from the eye so scene features appear pulled IN.
                // Sampling toward the centre would instead make the eye bulge outwards.
                return suction * (delta + float2(-delta.y, delta.x) * twist);
            }

            float2 PlaneToScreenUV(float2 planePoint)
            {
                float4 clipPosition = TransformObjectToHClip(float3(planePoint / _LensSize.xy, 0));
                float4 screenPosition = ComputeScreenPos(clipPosition);
                return screenPosition.xy / max(screenPosition.w, 0.0001);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_CameraOpaqueTexture_TexelSize.z <= 16.0) return 0;
                float coverageA, coverageB;
                float2 offset = PullField(input.planePosition, _EyeA, 1.0, coverageA);
                offset += PullField(input.planePosition, _EyeB, -1.0, coverageB);
                // Project displacement from the actual eye plane. Its apparent size scales
                // with distance, camera FOV, perspective, and render resolution.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float2 warpedUV = screenUV + PlaneToScreenUV(input.planePosition + offset) - PlaneToScreenUV(input.planePosition);
                float2 margin = _CameraOpaqueTexture_TexelSize.xy * 0.5;
                half3 scene = SampleSceneColor(clamp(warpedUV, margin, 1.0 - margin));
                float2 edgeDistance = abs(input.planePosition / _LensSize.xy) * 2.0;
                float edge = 1.0 - smoothstep(0.88, 1.0, max(edgeDistance.x, edgeDistance.y));
                half alpha = max(coverageA, coverageB) * edge * _Opacity;
                // The scene copy already includes fog; applying fog again creates a halo.
                return half4(scene * alpha, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
