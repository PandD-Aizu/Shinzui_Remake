Shader "Shinzui/BlackHoleEye"
{
    Properties
    {
        _Opacity ("Opacity", Range(0, 1)) = 1
        [HDR] _RingColor ("Photon Ring", Color) = (0.68, 0.69, 0.62, 1)
        [HDR] _DiskColor ("Accretion Disk", Color) = (0.10, 0.095, 0.08, 1)
        _Intensity ("Emission", Range(0, 6)) = 0.9
        _HorizonRadius ("Event Horizon Radius", Range(0.12, 0.48)) = 0.30
        _RingWidth ("Photon Ring Width", Range(0.008, 0.12)) = 0.014
        _EyeShape ("Outer Eye Radius XY / Tilt Z", Vector) = (0.65, 0.43, 0.04, 0)
        _PupilOffset ("Horizon Offset XY", Vector) = (0, 0, 0, 0)
        _Deformation ("Lens Drift", Range(0, 0.2)) = 0.012
        _DeformationSpeed ("Deformation Speed", Range(0, 2)) = 0.45
        _SwirlSpeed ("Orbit Speed", Range(-3, 3)) = 0.7
        _InfallSpeed ("Inward Flow Speed", Range(0, 3)) = 0.8
        _SpiralTightness ("Spiral Winding", Range(2, 20)) = 9
        _Phase ("Eye Phase", Float) = 0
        _SceneRefraction ("Environment Ring", Range(0, 1)) = 0.9
        [HideInInspector] _LensStrength ("Legacy Lens", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Name "BlackHoleEye"
            Tags { "LightMode" = "ForwardOnly" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "HdrpObjectCommon.hlsl"


            CBUFFER_START(UnityPerMaterial)
                float _Opacity;
                half4 _RingColor;
                half4 _DiskColor;
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            /// <summary>頂点を画面座標へ変換する</summary>
            /// <param name="input">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            /// <summary>眼球平面から画面UVへ投影する</summary>
            /// <param name="planePoint">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            float2 EyeToScreenUV(float2 planePoint)
            {
                float4 projected = ComputeScreenPos(TransformObjectToHClip(float3(planePoint * 0.5, 0)));
                return projected.xy / max(projected.w, 0.0001);
            }
            /// <summary>画素の色と被覆率を評価する</summary>
            /// <param name="input">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 original = input.uv * 2.0 - 1.0;
                float2 p = original;
                float t = _Time.y;
                float drift = t * _DeformationSpeed * 0.35 + _Phase;

                p -= _PupilOffset.xy;
                p += _Deformation * float2(sin(drift), cos(drift * 0.7));
                float angle = atan2(p.y, p.x);
                float r = max(length(p), 0.0001);
                float horizon = _HorizonRadius;
                float aa = max(fwidth(r), 0.001);
                float outside = smoothstep(horizon - aa, horizon + aa, r);
                float edge = 1.0 - smoothstep(0.80, 0.97, length(original));

                float2 lensPoint = float2(p.x + p.y * _EyeShape.z, p.y) / max(_EyeShape.xy, float2(0.2, 0.2));
                lensPoint.y *= 1.0 + 0.10 * lensPoint.x * lensPoint.x;
                float lensRadius = length(lensPoint);
                float lensAA = max(fwidth(lensRadius), 0.001);
                float rimRadius = 1.0 + 0.008 * sin(angle * 3.0 + _Phase);
                float rimWidth = 0.085 + 0.015 * sin(angle + _Phase);
                float outerArc = exp(-pow((lensRadius - rimRadius) / max(rimWidth, lensAA), 4.0));
                float annulus = (1.0 - smoothstep(0.92, 1.12, lensRadius)) * outside;

                float2 direction = p / r;
                float outerRadius = r / max(lensRadius, 0.001);
                float infall = saturate((outerRadius - r) / max(outerRadius - horizon, 0.05));
                float wave = 1.0 + 0.035 * sin(infall * 9.0 - t * _InfallSpeed + _Phase);
                float bend = horizon * horizon / max(r, horizon) * (3.2 + 2.0 * infall) * wave;
                float twist = sign(_SwirlSpeed) * (0.15 + 1.25 * infall * infall);
                float sine, cosine;
                sincos(twist, sine, cosine);
                float2 woundDirection = float2(direction.x * cosine - direction.y * sine,
                    direction.x * sine + direction.y * cosine);
                float2 samplePoint = original - p + woundDirection * (r + bend);
                float2 margin = _ScreenSize.zw * 0.5;
                half3 environment = 0;
                float hasScene = _ScreenSize.x > 16.0 ? 1.0 : 0.0;
                if (hasScene > 0.0)
                    environment = SampleCameraColor(clamp(EyeToScreenUV(samplePoint), margin, 1.0 - margin));

                float logRadius = log(max(r / horizon, 0.001));
                float orbit = angle - t * _SwirlSpeed * 0.12 + _Phase;
                float flow = logRadius * _SpiralTightness + t * _InfallSpeed;
                float filament = pow(saturate(0.5 + 0.5 * sin(orbit * 13.0 + flow * 2.0)), 8.0);
                float beaming = 0.08 + 0.92 * pow(saturate(0.5 + 0.5 * cos(angle + _Phase)), 3.0);
                float ringRadius = horizon + 0.018 + 0.003 * sin(orbit * 5.0);
                float ringWidth = max(_RingWidth, aa * 0.65);
                float ring = exp(-pow((r - ringRadius) / ringWidth, 2.0)) * outside;
                float halo = exp(-abs(r - ringRadius) * 42.0) * outside;
                half3 emission = _RingColor.rgb * (ring * (0.45 + 0.55 * beaming) + halo * 0.025);
                emission += _DiskColor.rgb * annulus * filament * 0.025;

                float arcDetail = 0.92 + 0.05 * sin(orbit * 19.0 + flow) + 0.03 * sin(orbit * 37.0 - flow);
                half3 arcLight = (environment * 0.85 + _RingColor.rgb * _Intensity * 0.38)
                    * outerArc * (0.38 + 0.62 * beaming) * arcDetail;

                float lensAlpha = annulus * _SceneRefraction * hasScene;
                float alpha = saturate(1.0 - outside + lensAlpha + ring * 0.65 + outerArc * 0.8) * edge;
                half3 color = environment * lensAlpha * (1.0 - 0.2 * infall) + emission * _Intensity + arcLight;
                return half4(color * edge, alpha) * _Opacity;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
