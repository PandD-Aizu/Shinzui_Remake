Shader "Shinzui/BlackHoleEye"
{
    Properties
    {
        [HDR] _RingColor ("Photon Ring", Color) = (0.68, 0.69, 0.62, 1)
        [HDR] _DiskColor ("Accretion Disk", Color) = (0.10, 0.095, 0.08, 1)
        _SocketColor ("Sunken Socket", Color) = (0.012, 0.010, 0.009, 1)
        _Intensity ("Emission", Range(0, 6)) = 0.9
        _HorizonRadius ("Event Horizon Radius", Range(0.12, 0.48)) = 0.30
        _RingWidth ("Photon Ring Width", Range(0.008, 0.12)) = 0.014
        _EyeShape ("Eye Shape XY / Tilt Z", Vector) = (1, 0.72, 0.12, 0)
        _PupilOffset ("Off-centre Iris XY", Vector) = (0.04, -0.02, 0, 0)
        _Deformation ("Eye Deformation", Range(0, 0.2)) = 0.085
        _DeformationSpeed ("Deformation Speed", Range(0, 2)) = 0.45
        _SwirlSpeed ("Orbit Speed", Range(-3, 3)) = 0.7
        _InfallSpeed ("Inward Flow Speed", Range(0, 3)) = 0.8
        _SpiralTightness ("Spiral Winding", Range(2, 20)) = 9
        _Phase ("Eye Phase", Float) = 0
        // Legacy material values. Surrounding refraction now renders before both eyes.
        [HideInInspector] _LensStrength ("Legacy Lens", Float) = 0
        [HideInInspector] _SceneRefraction ("Legacy Refraction", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Name "BlackHoleEye"
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

            CBUFFER_START(UnityPerMaterial)
                half4 _RingColor;
                half4 _DiskColor;
                half4 _SocketColor;
                float4 _EyeShape, _PupilOffset;
                float _Deformation, _DeformationSpeed;
                float _Intensity, _HorizonRadius, _RingWidth;
                float _SwirlSpeed, _InfallSpeed, _SpiralTightness, _Phase;
                float _LensStrength, _SceneRefraction;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            /// <summary>
            /// Project the eye surface and preserve its local coordinates
            /// </summary>
            /// <param name="input">Eye mesh vertex</param>
            /// <returns>Projected vertex and texture coordinates</returns>
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            /// <summary>
            /// Draw a slowly deforming, off-centre singularity inside a dark socket
            /// </summary>
            /// <param name="input">Interpolated eye surface</param>
            /// <returns>Premultiplied socket and unlit eye color</returns>
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.uv * 2.0 - 1.0;
                float t = _Time.y;
                float drift = t * _DeformationSpeed + _Phase;
                float edge = 1.0 - smoothstep(0.84, 0.98, length(p));

                // Warp the pupil and rings themselves, with a different phase for each eye
                p.x += p.y * _EyeShape.z;
                p /= max(_EyeShape.xy, float2(0.2, 0.2));
                p += _Deformation * float2(
                    sin(p.y * 5.0 + drift) + 0.35 * sin(p.x * 7.0 - drift * 0.7),
                    cos(p.x * 4.0 - drift * 0.8) + 0.3 * sin(p.y * 6.0 + drift * 0.6));
                float socketRadius = length(p);
                p -= _PupilOffset.xy;
                float angle = atan2(p.y, p.x);
                float r = max(length(p), 0.0001);
                r *= 1.0 + _Deformation * (0.55 * sin(angle * 2.0 + drift) +
                    0.3 * cos(angle * 3.0 - drift * 0.65));
                float horizon = _HorizonRadius * (1.0 + 0.018 * sin(t * 1.3 + _Phase));
                float aa = max(fwidth(r), 0.001);
                float outside = smoothstep(horizon - aa, horizon + aa, r);

                // Log-polar bands advance toward smaller radii while orbiting the core.
                // Integer angular frequencies keep the atan2 seam continuous.
                float logRadius = log(max(r / horizon, 0.001));
                float orbit = angle - t * _SwirlSpeed + _Phase;
                float flow = logRadius * _SpiralTightness + t * _InfallSpeed * 4.0;
                float filament = pow(saturate(0.5 + 0.5 * sin(orbit * 3.0 + flow)), 7.0);
                float fine = pow(saturate(0.5 + 0.5 * sin(orbit * 7.0 - flow * 1.7)), 12.0);
                float disk = exp(-max(r - horizon, 0.0) * 4.5) * outside * edge;
                float ringRadius = horizon + 0.035;
                float ring = 1.0 - smoothstep(_RingWidth, _RingWidth + aa * 1.5, abs(r - ringRadius));
                float halo = exp(-abs(r - ringRadius) * 32.0) * outside;
                float beaming = 0.38 + 0.62 * saturate(0.5 + 0.5 * cos(angle - _Phase + 0.15 * sin(drift)));
                float outerRing = 1.0 - smoothstep(_RingWidth * 0.5, _RingWidth * 0.5 + aa,
                    abs(r - horizon - 0.095));
                half3 emission = _RingColor.rgb * (ring * 0.85 + outerRing * 0.16 + halo * 0.035) * beaming;
                emission += _DiskColor.rgb * disk * (filament * 0.18 + fine * 0.06 + 0.025);
                emission *= _Intensity * outside;

                // Broad uneven sockets and a narrow pale iris replace the bright circular outline
                float socket = 1.0 - smoothstep(0.52, 0.88, socketRadius);
                float alpha = saturate(socket + (1.0 - outside) + ring + outerRing * 0.2) * edge;
                half3 color = (_SocketColor.rgb * socket * (0.45 + 0.55 * beaming) * outside + emission) * edge;
                // Preserve the unlit singularity alongside the body's glow in dense stage fog
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
