Shader "Custom/PortalProjection"
{
    Properties
    {
        _MainTex ("Portal Texture", 2D) = "white" {}
        _PortalExposureMultiplier ("Portal Exposure Multiplier", Float) = 1.0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Transparent-99"
            "RenderPipeline"="HDRenderPipeline"
        }

        Pass
        {
            Name "PortalProjectionPass"
            Tags { "LightMode" = "ForwardOnly" }

            Cull Off
            ZWrite Off
            ZTest LEqual

            Stencil
            {
                Ref 64
                ReadMask 64
                WriteMask 64
                Comp Equal
                Pass Keep
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
                float4 screenPos    : TEXCOORD0;
            };

            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            float _PortalExposureMultiplier;

            /// <summary>頂点を画面座標へ変換する</summary>
            /// <param name="input">評価に用いる入力値</param>
            /// <returns>変換または評価した値</returns>
            Varyings vert(Attributes input)
            {
                Varyings output;
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
                half4 color = _MainTex.Sample(sampler_MainTex, uv);
                /* RTは倍率1のシーンリニアであり再帰カメラでも倍率1を維持する */
                color.rgb *= GetCurrentExposureMultiplier();
                return color;
            }
            ENDHLSL
        }
    }
}
