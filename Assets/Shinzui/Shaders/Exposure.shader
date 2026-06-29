Shader "Custom/Exposure"
{
    Properties
    {
        [HideInInspector] _BlitTexture("Blit Texture", 2D) = "white" { }
    }
    
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off Cull Off
        
        Pass
        {
            Name "ExposurePass"
            
            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            // C#側から送られる制御変数
            uint _Mode;
            float _FixedExposureMultiplier;
            
            // ComputeShaderから出力された露出バッファ
            StructuredBuffer<float> _HDRPExposureBufferGlobal;
            
            half4 Frag(Varyings input) : SV_Target {
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                
                float exposure = 1.0f;
                
                if (_Mode == 0) {
                    exposure = _FixedExposureMultiplier;
                } else {
                    exposure = _HDRPExposureBufferGlobal[0];
                }
                
                // 露出をカラーに適用
                color.rgb *= exposure;
                
                return color;
            }
            
            ENDHLSL
        }
    }
}