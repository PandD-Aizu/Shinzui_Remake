Shader "Custom/Pixelation"
{
    Properties
    {
        _Intensity("Intensity", Range(0.0, 1.0)) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off
        
        Pass
        {
            Name "PixelationPass"
            
            HLSLPROGRAM
            
            #pragma target 4.5
            #pragma vertex Fullscreen
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"
            
            TEXTURE2D_X(_InputTexture);
            float _Intensity;
            
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
            
            half4 frag(Varyings input) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;
                
                if (_Intensity > 0.0)
                {
                    float2 texSize = _ScreenSize.xy;
                    
                    // _Intensityに応じて、1つのモザイクのピクセルサイズを1から64まで補間する
                    float pixelSize = lerp(1.0, 64.0, _Intensity);
                    
                    float2 pixelUV = uv * texSize;
                    pixelUV = floor(pixelUV / pixelSize) * pixelSize + (pixelSize * 0.5);
                    uv = pixelUV / texSize;
                }
                
                half4 color = SampleInput(uv);
                // 細い発光管などの高輝度部分をブロック中心のサンプル欠落から保護する
                half4 detail = SampleInput(input.uv);
                half luminance = dot(detail.rgb, half3(0.2126, 0.7152, 0.0722));
                half highlight = smoothstep(0.75h, 1.0h, luminance);
                color.rgb = lerp(color.rgb, max(color.rgb, detail.rgb), highlight);
                return color;
            }
            
            ENDHLSL
        }
    }
}
