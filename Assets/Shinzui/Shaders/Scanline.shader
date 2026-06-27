Shader "Custom/Scanline"
{
    Properties
    {
        _Speed("Speed", Range(0.0, 10.0)) = 0.5
        _BarSize("Bar Size", Range(0.0, 1.0)) = 0.1
        _Strength("Strength", Range(0.0, 10.0)) = 1.0
        _Frequency("Frequency", Range(0.0, 100.0)) = 30.0
        _Curvature("Curvature", Range(0.0, 0.5)) = 0.03
        _FineLines("Fine Lines Intensity", Range(0.0, 1.0)) = 0.25
        _FineLinesScale("Fine Lines Scale", Range(50.0, 2000.0)) = 480.0
        _Vignette("Vignette Intensity", Range(0.0, 1.0)) = 0.3
        _Flicker("Flicker Intensity", Range(0.0, 0.5)) = 0.05
    }
    
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off
        
        Pass
        {
            Name "ScanlinePass"
            
            HLSLPROGRAM
            
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_BlitTexture);
            
            float _Speed;
            float _BarSize;
            float _Strength;
            float _Frequency;
            float _Curvature;
            float _FineLines;
            float _FineLinesScale;
            float _Vignette;
            float _Flicker;
            
            struct Varyings 
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;    
            };
            
            Varyings vert(uint id : SV_VertexID) 
            {
                Varyings output;
                output.positionHCS = GetFullScreenTriangleVertexPosition(id);
                output.uv = GetFullScreenTriangleTexCoord(id);
                return output;
            }
            
            // 擬似乱数ノイズ生成
            float rand(float2 co)
            {
                return frac(sin(dot(co, float2(12.9898, 78.233))) * 43758.5453);
            }
            
            // 画面の樽型歪み
            float2 radialDistortion(float2 uv, float distort)
            {
                float2 cc = uv - 0.5;
                float dist = dot(cc, cc);
                return uv + cc * dist * distort;
            }
            
            // 画面周辺を暗くするビネット効果
            float vignette(float2 uv, float strength)
            {
                float2 vignetteUV = uv * (1.0 - uv.yx);
                float vig = vignetteUV.x * vignetteUV.y * 15.0;
                return saturate(pow(vig, strength));
            }
            
            half4 frag(Varyings input) : SV_Target 
            {
                float2 baseUV = input.uv;
                float time = _Time.y;
                
                // ブラウン管球面湾曲シミュレーション
                float2 uv = baseUV;
                if (_Curvature > 0.0)
                {
                    uv = radialDistortion(uv, _Curvature);
                    // 湾曲によって画面外になった領域を黒にする
                    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    {
                        return half4(0.0, 0.0, 0.0, 1.0);
                    }
                }
                
                // スキャンラインバーの進行位置計算
                float scanY = fmod(time * _Speed, 1.0 + _BarSize * 2.0) - _BarSize;
                
                // Y座標との距離
                float dist = abs(uv.y - (1.0 - scanY));
                
                // 帯の中心ほど強く、端に向かって滑らかに減衰するベルカーブを計算
                float halfSize = _BarSize * 0.5;
                float rawIntensity = saturate(1.0 - (dist / halfSize));
                float intensity = smoothstep(0.0, 1.0, rawIntensity); // 境界の急激な変化を防ぐ
                
                // 歪みの計算: 範囲内のみ
                float distortion = intensity * _Strength;
                
                // ベースの歪みUV
                // 歪み全体のスケールをマイルドに調整
                float2 distortedUV = uv;
                float wave = sin(uv.y * _Frequency + time * 15.0);
                float noiseJitter = (rand(float2(uv.y * 300.0, time)) - 0.5) * 0.08;
                distortedUV.x -= distortion * (wave + noiseJitter) * 0.006;
                
                // RGB色収差
                // 色収差のずれ幅を大幅にマイルドに調整
                float distFromCenter = length(uv - 0.5);
                float aberration = distortion * 0.004 + (distFromCenter * 0.0015);
                
                half3 finalColor;
                finalColor.r = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, distortedUV + float2(aberration, 0.0)).r;
                finalColor.g = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, distortedUV).g;
                finalColor.b = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, distortedUV - float2(aberration, 0.0)).b;
                
                // 微細な水平走査線の重畳
                if (_FineLines > 0.0)
                {
                    // 走査線の細かさを _FineLinesScale で制御
                    float fineScanlineValue = sin(distortedUV.y * 3.14159 * 2.0 * _FineLinesScale);
                    float scanlineDarkness = lerp(1.0, 1.0 - _FineLines * 0.25, (fineScanlineValue + 1.0) * 0.5);
                    finalColor *= scanlineDarkness;
                }
                
                // スキャンバー自体のリン光グロー効果
                // レトロな青緑がかったグローをわずかに加算
                half3 glowColor = half3(0.6, 0.85, 1.0) * intensity * 0.15;
                finalColor += glowColor;
                
                // アナログ輝度のフリッカー
                if (_Flicker > 0.0)
                {
                    float flickerVal = 1.0 - (rand(float2(time * 0.5, time)) * _Flicker * 0.4);
                    finalColor *= flickerVal;
                }
                
                // ビネット効果の適用
                if (_Vignette > 0.0)
                {
                    finalColor *= vignette(baseUV, _Vignette * 0.5 + 0.1);
                }
                
                return half4(finalColor, 1.0);
            }
            
            ENDHLSL
        }
    }
}
