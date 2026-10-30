Shader "Custom/PortalShader"
{
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent-99" "RenderType"="Opaque" "RenderPipeline"="HDRenderPipeline" }
        LOD 100

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "ForwardOnly" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "HdrpObjectCommon.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 screenPos    : TEXCOORD0;
            };

            Texture2D _BaseMap;
            SamplerState sampler_BaseMap;

            /// <summary>頂点を画面座標へ変換する</summary>
            /// <param name="input">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            /// <summary>画素の色を評価する</summary>
            /// <param name="input">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / input.screenPos.w;
                half4 col = _BaseMap.Sample(sampler_BaseMap, uv);
                return col;
            }
            ENDHLSL
        }
    }
}
