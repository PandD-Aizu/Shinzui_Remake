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
        
        Pass
        {
            Name "PixelationPass"
            
            HLSLPROGRAM
            
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_BlitTexture);
            float4 _MainTex_TexelSize;
            float _Intensity;
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            Varyings vert(uint id : SV_VertexID) {
                Varyings output;
                output.positionHCS = GetFullScreenTriangleVertexPosition(id);
                output.uv = GetFullScreenTriangleTexCoord(id);
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target {
                float2 uv = input.uv;
                
                if (_Intensity > 0.0)
                {
                    float2 texSize = _ScreenParams.xy;
                    
                    // _Intensityに応じて、1つのモザイクのピクセルサイズを1から64まで補間する
                    float pixelSize = lerp(1.0, 64.0, _Intensity);
                    
                    float2 pixelUV = uv * texSize;
                    pixelUV = floor(pixelUV / pixelSize) * pixelSize + (pixelSize * 0.5);
                    uv = pixelUV / texSize;
                }
                
                half4 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv);
                return color;
            }
            
            ENDHLSL
        }
    }
}