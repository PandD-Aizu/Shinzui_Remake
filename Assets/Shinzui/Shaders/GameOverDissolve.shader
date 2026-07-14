Shader "Custom/GameOverDissolve"
{
    Properties
    {
        _Progress("Progress", Range(0.0, 1.0)) = 0.0
        _EdgeWidth("Edge Width", Range(0.01, 0.35)) = 0.11
        _NoiseStrength("Noise Strength", Range(0.0, 0.65)) = 0.24
        _CellIntensity("Cell Intensity", Range(0.0, 2.0)) = 1.0
        _CoverColor("Cover Color", Color) = (0.0, 0.0, 0.01, 1.0)
        _MembraneColor("Membrane Color", Color) = (0.12, 0.55, 0.72, 1.0)
        _HotEdgeColor("Hot Edge Color", Color) = (1.05, 1.22, 1.25, 1.0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "GameOverDissolvePass"

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_MainTex);
            float _Progress;
            float _EdgeWidth;
            float _NoiseStrength;
            float _CellIntensity;
            float4 _CoverColor;
            float4 _MembraneColor;
            float4 _HotEdgeColor;

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint id : SV_VertexID)
            {
                Varyings output;
                output.positionHCS = GetFullScreenTriangleVertexPosition(id);
                output.uv = GetFullScreenTriangleTexCoord(id);
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 Hash22(float2 p)
            {
                float n = Hash21(p);
                return float2(n, Hash21(p + n + 19.19));
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float2x2 rotation = float2x2(1.55, 1.18, -1.18, 1.55);

                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    value += ValueNoise(p) * amplitude;
                    p = mul(rotation, p) + 0.17;
                    amplitude *= 0.5;
                }

                return value;
            }

            float2 DomainWarp(float2 p, float time)
            {
                float2 q;
                q.x = Fbm(p * 1.15 + float2(0.0, time * 0.10));
                q.y = Fbm(p * 1.15 + float2(5.2, -time * 0.08));

                float2 r;
                r.x = Fbm(p * 1.9 + q * 2.3 + float2(1.7, 9.2));
                r.y = Fbm(p * 1.9 + q * 2.3 + float2(8.3, 2.8));

                return p + (r - 0.5) * 0.27;
            }

            float3 OrganicVoronoi(float2 p, float time)
            {
                float2 baseCell = floor(p);
                float2 localPosition = frac(p);

                float nearest = 100.0;
                float secondNearest = 100.0;
                float cellRandom = 0.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2((float)x, (float)y);
                        float2 cell = baseCell + offset;
                        float2 randomPoint = Hash22(cell);

                        float phaseX = Hash21(cell + 2.31) * 6.2831853;
                        float phaseY = Hash21(cell + 8.72) * 6.2831853;
                        float2 movement = float2(
                            sin(time * (0.55 + randomPoint.x * 0.45) + phaseX),
                            cos(time * (0.48 + randomPoint.y * 0.50) + phaseY));

                        randomPoint = 0.5 + (randomPoint - 0.5) * 0.48 + movement * 0.08;

                        float2 delta = offset + randomPoint - localPosition;
                        float stretch = lerp(0.82, 1.22, Hash21(cell + 14.7));
                        delta.x *= stretch;
                        delta.y /= stretch;

                        float distanceSquared = dot(delta, delta);
                        if (distanceSquared < nearest)
                        {
                            secondNearest = nearest;
                            nearest = distanceSquared;
                            cellRandom = Hash21(cell + 31.7);
                        }
                        else if (distanceSquared < secondNearest)
                        {
                            secondNearest = distanceSquared;
                        }
                    }
                }

                float f1 = sqrt(nearest);
                float f2 = sqrt(secondNearest);
                return float3(f1, f2 - f1, cellRandom);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half4 source = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv);

                float2 centered = (uv * 2.0 - 1.0) * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                float time = _Time.y;
                float radialDistance = saturate(length(centered) / length(float2(_ScreenParams.x / _ScreenParams.y, 1.0)));

                float2 warped = DomainWarp(centered * 2.15 + float2(time * 0.035, -time * 0.02), time * 0.75);
                float broadNoise = Fbm(warped * 2.2 + float2(time * 0.025, -time * 0.018));
                float mediumNoise = Fbm(warped * 5.5 - float2(time * 0.045, time * 0.027));
                float fineNoise = Fbm(warped * 12.0 + float2(-time * 0.035, time * 0.052));

                float directionalBias = dot(normalize(centered + 0.0001), normalize(float2(-0.45, 0.35))) * 0.035;
                float dissolveField =
                    radialDistance +
                    (broadNoise - 0.5) * _NoiseStrength +
                    (mediumNoise - 0.5) * _NoiseStrength * 0.5 +
                    (fineNoise - 0.5) * _NoiseStrength * 0.18 +
                    directionalBias;

                float threshold = lerp(1.18, -0.13, saturate(_Progress));
                float cover = smoothstep(threshold - _EdgeWidth, threshold + _EdgeWidth, dissolveField);
                float edgeDistance = abs(dissolveField - threshold);
                float broadEdge = (1.0 - smoothstep(0.0, _EdgeWidth * 1.55, edgeDistance)) * saturate(_Progress);
                float brightEdge = (1.0 - smoothstep(0.0, _EdgeWidth * 0.34, edgeDistance)) * saturate(_Progress);

                float2 cellWarpA = DomainWarp(centered * 2.6 + float2(time * 0.025, -time * 0.016), time * 0.9);
                float3 cellA = OrganicVoronoi(cellWarpA * 5.2, time * 0.8);
                float largeCellRadius = lerp(0.27, 0.42, cellA.z);
                float largeInterior = 1.0 - smoothstep(largeCellRadius - 0.05, largeCellRadius + 0.04, cellA.x);
                float largeMembrane = 1.0 - smoothstep(0.012, 0.075, cellA.y);

                float2 cellWarpB = DomainWarp(centered * 3.8 - float2(time * 0.018, time * 0.022), time * 1.13);
                float3 cellB = OrganicVoronoi(cellWarpB * 8.3, -time * 0.65);
                float smallCellRadius = lerp(0.22, 0.36, cellB.z);
                float smallInterior = 1.0 - smoothstep(smallCellRadius - 0.045, smallCellRadius + 0.035, cellB.x);
                float smallMembrane = 1.0 - smoothstep(0.010, 0.055, cellB.y);

                float breakup = smoothstep(0.30, 0.67, Fbm(centered * 20.0 + float2(time * 0.12, -time * 0.09)));
                float cellInterior = max(largeInterior, smallInterior * 0.72) * broadEdge * _CellIntensity;
                float cellMembrane = max(largeMembrane * lerp(0.35, 1.0, breakup), smallMembrane * lerp(0.25, 1.0, breakup) * 0.82) * broadEdge * _CellIntensity;

                float veins = 1.0 - smoothstep(0.035, 0.115, abs(Fbm(centered * 18.0 + float2(sin(time * 0.25), cos(time * 0.21))) - 0.52));
                veins *= cellInterior * 0.4;

                float3 cellularColor = _CoverColor.rgb;
                cellularColor -= float3(0.005, 0.025, 0.075) * cellInterior * 1.45;
                cellularColor += _MembraneColor.rgb * cellInterior * 0.48;
                cellularColor += _MembraneColor.rgb * veins * 0.8;
                cellularColor += _MembraneColor.rgb * cellMembrane * 1.55;
                cellularColor += _HotEdgeColor.rgb * brightEdge * 1.95;

                float3 color = lerp(source.rgb, cellularColor, cover);
                color += _MembraneColor.rgb * broadEdge * 0.38;
                color += _HotEdgeColor.rgb * brightEdge * 1.35;

                float residualDarken = smoothstep(0.82, 1.0, _Progress) * (1.0 - cover);
                color = lerp(color, _CoverColor.rgb, residualDarken * 0.65);

                color = max(color, 0.0);
                color = 1.0 - exp(-color * 1.18);
                color += pow(color, 4.0) * 0.12;

                return half4(color, source.a);
            }

            ENDHLSL
        }
    }
}
