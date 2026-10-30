Shader "Custom/InverseScanline"
{
    Properties
    {
        _GlitchPeriod("Glitch Period", Range(0.0, 60.0)) = 15.0
        _GlitchSpeed("Glitch Speed", Range(0.05, 10.0)) = 0.5
        _GlitchProbability("Glitch Probability", Range(0.0, 1.0)) = 0.1
        _GlitchSize("Glitch Size", Range(0.001, 0.1)) = 0.001
        _BlockScale("Block Scale", Vector) = (50, 300, 0, 0)
    }
    
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off
        
        Pass
        {
            Name "InverseScanlinePass"
            
            HLSLPROGRAM
            
            #pragma target 4.5
            #pragma vertex Fullscreen
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"
            
            TEXTURE2D_X(_InputTexture);
            float _GlitchPeriod;
            float _GlitchSpeed;
            float _GlitchProbability;
            float _GlitchSize;
            float2 _BlockScale;
            
            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            float hashNoise(float2 p) 
            {
                return frac(sin(dot(p, float2(12.9898, 78.233)))   * 43758.5453);
            }
            
            Varyings Fullscreen(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            // 解像度変更時も有効な入力領域だけを線形補間する
            float4 SampleInput(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_InputTexture, s_linear_clamp_sampler,
                    ClampAndScaleUVForBilinearPostProcessTexture(uv));
            }
            
            half4 frag(Varyings input) : SV_Target 
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;
                float3 finalColor = LOAD_TEXTURE2D_X(_InputTexture, uint2(input.positionHCS.xy)).rgb;
                
                float glitchTime = fmod(_Time.y, max(_GlitchPeriod, 0.0001));
                float glitchY = glitchTime * _GlitchSpeed;
                if (glitchY < 1.0) {
                    
                    // グリッチの影響範囲
                    float distY = abs(uv.y - glitchY);
                    if (distY < _GlitchSize) {
                        float2 blockScale = _BlockScale.xy;
                        float2 blockId = floor(uv * blockScale);
                        
                        // 時間経過で高速に
                        float noiseVal = hashNoise(blockId + float2(_Time.y * 60.0, 0.0));
                        
                        // 出現確率を調整
                        float baseThreshold = 1.0 - _GlitchProbability;
                        float threshold = baseThreshold + (distY / _GlitchSize) * (1.0 - baseThreshold) * 0.8;
                        
                         if (noiseVal > threshold) {
                            float3 glitchColor;
                            float colorRand = frac(noiseVal * 123.45);
                            
                            if (colorRand < 0.3) {
                                glitchColor = float3(1.0, 0.05, 0.05);
                            } else if (colorRand < 0.6) {
                                glitchColor = float3(1.0, 0.5, 0.1);
                            } else {
                                glitchColor = float3(1.0, 0.9, 0.7);
                            }
                            
                            // 最終的な色を計算
                            float2 shiftUV = uv;
                            shiftUV.x += (noiseVal - 0.5) * 0.03;
                            
                            // テクスチャをサンプリング
                            float4 shiftColor = SampleInput(shiftUV);
                            
                            // 元のコードと同様のブレンド計算を適用
                            float3 tempColor = finalColor + glitchColor * 2.5;
                            finalColor = lerp(tempColor, shiftColor.rgb + glitchColor, 0.4);
                        }
                    }
                }
                
                return half4(finalColor, 1.0);
            }
            
            ENDHLSL
        }
    }
}