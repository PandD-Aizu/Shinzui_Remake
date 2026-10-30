Shader "Custom/PortalMask"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Transparent-100"
            "RenderPipeline"="HDRenderPipeline"
        }

        Pass
        {
            Name "PortalMaskPass"
            Tags { "LightMode" = "ForwardOnly" }

            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            Stencil
            {
                Ref 64
                ReadMask 64
                WriteMask 64
                Comp Always
                Pass Replace
            }

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
            };

            /// <summary>頂点を画面座標へ変換する</summary>
            /// <param name="input">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            /// <summary>画素の色を評価する</summary>
            /// <param name="input">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            half4 frag(Varyings input) : SV_Target
            {
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
