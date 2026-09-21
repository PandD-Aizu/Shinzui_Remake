Shader "Shinzui/BlackHoleEye"
{
    Properties
    {
        [HDR] _RingColor ("Photon Ring", Color) = (0.72, 0.94, 1, 1)
        [HDR] _DiskColor ("Accretion Disk", Color) = (0.18, 0.48, 0.65, 1)
        _Intensity ("Emission", Range(0, 6)) = 1.6
        _HorizonRadius ("Event Horizon Radius", Range(0.12, 0.48)) = 0.30
        _RingWidth ("Photon Ring Width", Range(0.008, 0.12)) = 0.032
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
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RingColor;
                half4 _DiskColor;
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
                float fog : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.uv * 2.0 - 1.0;
                float r = max(length(p), 0.0001);
                float angle = atan2(p.y, p.x);
                float t = _Time.y;
                float horizon = _HorizonRadius * (1.0 + 0.018 * sin(t * 1.3 + _Phase));
                float aa = max(fwidth(r), 0.001);
                float outside = smoothstep(horizon - aa, horizon + aa, r);
                float edge = 1.0 - smoothstep(0.82, 0.98, r);

                // Log-polar bands advance toward smaller radii while orbiting the core.
                // Integer angular frequencies keep the atan2 seam continuous.
                float logRadius = log(max(r / horizon, 0.001));
                float orbit = angle - t * _SwirlSpeed + _Phase;
                float flow = logRadius * _SpiralTightness + t * _InfallSpeed * 4.0;
                float filament = pow(saturate(0.5 + 0.5 * sin(orbit * 3.0 + flow)), 7.0);
                float fine = pow(saturate(0.5 + 0.5 * sin(orbit * 7.0 - flow * 1.7)), 12.0);
                float disk = exp(-max(r - horizon, 0.0) * 4.5) * outside * edge;
                float ringRadius = horizon + 0.055;
                float ring = 1.0 - smoothstep(_RingWidth, _RingWidth + aa * 1.5, abs(r - ringRadius));
                float halo = exp(-abs(r - ringRadius) * 19.0) * outside;
                float beaming = 0.78 + 0.22 * cos(orbit);
                half3 emission = _RingColor.rgb * (ring * 1.25 + halo * 0.22) * beaming;
                emission += _DiskColor.rgb * disk * (filament * 0.85 + fine * 0.24 + 0.045);
                emission *= _Intensity * outside;

                // Premultiplication preserves an opaque, genuinely black event horizon.
                float alpha = saturate((1.0 - outside) + ring + halo * 0.27 + disk * 0.8) * edge;
                half3 color = emission;
                // Fog the premultiplied result without exposing the quad's corners.
                color = MixFogColor(color, unity_FogColor.rgb * alpha, input.fog);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
