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
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Opaque" }

        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "Horror Detection Noise"

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"
            TEXTURE2D_X(_HorrorInput);
            float4 _HorrorInputScale;

            float _HorrorNoiseIntensity;
            float _HorrorDistortionIntensity;
            float _HorrorChromaticAberrationIntensity;
            float _HorrorScanlineIntensity;

            /// <summary>座標に応じた乱数を求める</summary>
            /// <param name="position">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            float Random(float2 position) {
                return frac(sin(dot(position, float2(12.9898, 78.233))) * 43758.5453);
            }

            /// <summary>画素の色を評価する</summary>
            /// <param name="input">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            half4 frag(Varyings input) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.positionCS.xy * _ScreenSize.zw;

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
                half red = SAMPLE_TEXTURE2D_X(_HorrorInput, s_linear_clamp_sampler, saturate(uv + float2(rgbOffset, 0.0)) * _HorrorInputScale.xy).r;
                half green = SAMPLE_TEXTURE2D_X(_HorrorInput, s_linear_clamp_sampler, saturate(uv) * _HorrorInputScale.xy).g;
                half blue = SAMPLE_TEXTURE2D_X(_HorrorInput, s_linear_clamp_sampler, saturate(uv - float2(rgbOffset, 0.0)) * _HorrorInputScale.xy).b;

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
