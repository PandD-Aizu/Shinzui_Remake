Shader "Custom/HorrorDetectionNoise"
{
    Properties
    {
        _NoiseIntensity("Noise Intensity", Range(0.0, 1.0)) = 0.0
        _DistortionIntensity("Distortion Intensity", Range(0.0, 0.1)) = 0.0
        _ChromaticAberrationIntensity("Chromatic Aberration Intensity", Range(0.0, 0.05)) = 0.0
        _ScanlineIntensity("Scanline Intensity", Range(0.0, 1.0)) = 0.15
    }
    
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        
        ZWrite Off
        Cull Off
        ZTest Always
        
        Pass
        {
            Name "Horror Detection Noise"
            
            HLSLPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            float _HorrorNoiseIntensity;
            float _HorrorDistortionIntensity;
            float _HorrorChromaticAberrationIntensity;
            float _HorrorScanlineIntensity;
            
            float Random(float2 position) {
                return frac(sin(dot(position, float2(12.9898, 78.233))) * 43758.5453);
            }
            
            Varyings vert(uint id : SV_VertexID) 
            {
                Varyings output;
                ZERO_INITIALIZE(Varyings, output);
                output.positionCS = GetFullScreenTriangleVertexPosition(id);
                output.texcoord = GetFullScreenTriangleTexCoord(id);
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target {
                float2 uv = input.texcoord;
                
                float noiseIntensity = saturate(_HorrorNoiseIntensity);
                
                // 横一列ごとにランダムなずれを作る
                float lineID = floor(uv.x * 120.0);
                float lineNoise = Random(
                    float2 (lineID, floor(uv.y * 30.0))  
                );
                
                // たまに強い横ずれ
                float glitchMask = step(0.82, lineNoise);
                float horizontalOffset = 
                    (lineNoise - 0.5) * _HorrorDistortionIntensity * glitchMask * noiseIntensity;
                
                uv.x += horizontalOffset;
                
                // RGBのサンプリング位置をずらす
                float rgbOffset = _HorrorChromaticAberrationIntensity * noiseIntensity;
                half red = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(rgbOffset, 0.0)).r;
                half green = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).g;
                half blue = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(rgbOffset, 0.0)).b;
                
                half3 color = half3(red, green, blue);
                
                // 砂嵐ノイズ
                float staticNoise = Random(
                    uv * float2(1920.0, 1080.0) + floor(_Time.y * 60.0)  
                );
                
                staticNoise = staticNoise * 2.0 - 1.0;
                
                color += staticNoise * noiseIntensity * 0.25;
                
                // 横方向の走査線
                float scanline = sin(uv.y * 900.0 + _Time.y * 20.0);
                
                color -= abs(scanline) * _HorrorScanlineIntensity * noiseIntensity * 0.08;
                
                return half4(color, 1.0);
            }
            
            ENDHLSL
        }
    }
}