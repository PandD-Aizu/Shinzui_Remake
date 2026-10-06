Shader "Custom/Pixelation"
{
    Properties
    {
        _Intensity("Intensity", Range(0.0, 1.0)) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off
        
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;
            float4 _PixelationOutputSize;
            float4 _PixelationSourceSize;
            float4 _PixelationGridSize;
            float _PixelationPixelSize;

            /// <summary>
            /// 既存の中心サンプル方式でピクセル化
            /// </summary>
            /// <param name="input">フルスクリーン頂点の補間結果</param>
            /// <returns>ハイライトを保護したカラー</returns>
            half4 FragPixelation(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                
                if (_Intensity > 0.0)
                {
                    float2 texSize = _ScreenParams.xy;
                    
                    // _Intensityに応じて、1つのモザイクのピクセルサイズを1から64まで補間する
                    float pixelSize = lerp(1.0, 64.0, _Intensity);
                    
                    float2 pixelUV = uv * texSize;
                    pixelUV = floor(pixelUV / pixelSize) * pixelSize + (pixelSize * 0.5);
                    uv = pixelUV / texSize;
                }
                
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                // 細い発光管などの高輝度部分をブロック中心のサンプル欠落から保護する
                half4 detail = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                half luminance = dot(detail.rgb, half3(0.2126, 0.7152, 0.0722));
                half highlight = smoothstep(0.75h, 1.0h, luminance);
                color.rgb = lerp(color.rgb, max(color.rgb, detail.rgb), highlight);
                return color;
            }

            /// <summary>
            /// ブロックが覆う高解像度入力を面積加重平均
            /// </summary>
            /// <param name="input">ブロック格子の補間結果</param>
            /// <returns>ブロック全体で共通の平均カラー</returns>
            float4 FragAverageBlocks(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 画面端の短いブロックも実際の被覆範囲で積分
                float2 block = floor(input.texcoord * _PixelationGridSize.xy);
                float2 outputMin = block * _PixelationPixelSize;
                float2 outputMax = min(outputMin + _PixelationPixelSize, _PixelationOutputSize.xy);
                float2 sourceMin = outputMin * _PixelationOutputSize.zw * _PixelationSourceSize.xy;
                float2 sourceMax = outputMax * _PixelationOutputSize.zw * _PixelationSourceSize.xy;
                int2 firstTexel = int2(floor(sourceMin));
                int2 endTexel = int2(ceil(sourceMax));
                float4 accumulated = 0.0;

                // 入力テクセルを省略せず、細い発光や反射の寄与をブロック全域へ保存
                [loop]
                for (int y = firstTexel.y; y < endTexel.y; ++y)
                {
                    float weightY = max(0.0, min(y + 1.0, sourceMax.y) - max((float)y, sourceMin.y));
                    [loop]
                    for (int x = firstTexel.x; x < endTexel.x; ++x)
                    {
                        float weightX = max(0.0, min(x + 1.0, sourceMax.x) - max((float)x, sourceMin.x));
                        float2 sampleUV = (float2(x, y) + 0.5) * _PixelationSourceSize.zw;
                        accumulated += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, sampleUV, 0) * (weightX * weightY);
                    }
                }

                float2 area = sourceMax - sourceMin;
                return accumulated / max(area.x * area.y, 0.000001);
            }

            /// <summary>
            /// 平均済みブロックを最終解像度へ点サンプリングで展開
            /// </summary>
            /// <param name="input">最終出力の補間結果</param>
            /// <returns>ブロック内で一定のカラー</returns>
            half4 FragExpandBlocks(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // レンダースケールが変わっても出力上のピクセル幅を維持
                float2 outputPixel = floor(input.texcoord * _PixelationOutputSize.xy);
                float2 block = floor(outputPixel / _PixelationPixelSize);
                float2 sampleUV = (block + 0.5) * _PixelationGridSize.zw;
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, sampleUV, 0);
            }
        ENDHLSL

        Pass
        {
            Name "PixelationPass"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragPixelation
            ENDHLSL
        }

        Pass
        {
            Name "AveragePixelBlocks"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragAverageBlocks
            ENDHLSL
        }

        Pass
        {
            Name "ExpandPixelBlocks"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragExpandBlocks
            ENDHLSL
        }
    }
}
