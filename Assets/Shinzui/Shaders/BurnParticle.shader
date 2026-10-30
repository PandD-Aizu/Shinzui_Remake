Shader "Shinzui/BurnParticle"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        [HDR] _TintColor ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Luminance (nits)", Float) = 1
        _SoftDistance ("Soft Particle Distance", Range(0.001, 0.5)) = 0.04
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment PUNCTUAL_SHADOW_LOW PUNCTUAL_SHADOW_MEDIUM PUNCTUAL_SHADOW_HIGH
            #pragma multi_compile_fragment DIRECTIONAL_SHADOW_LOW DIRECTIONAL_SHADOW_MEDIUM DIRECTIONAL_SHADOW_HIGH
            #pragma multi_compile_fragment AREA_SHADOW_MEDIUM AREA_SHADOW_HIGH

            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "HdrpObjectCommon.hlsl"
            #include "HdrpObjectLighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TintColor;
                float _Intensity;
                float _SoftDistance;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            /// <summary>粒子の頂点を画面座標へ変換する</summary>
            /// <param name="input">頂点入力</param>
            /// <returns>補間値</returns>
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            /// <summary>頂点カラーの寿命変化と物理輝度を反映した粒子色を求める</summary>
            /// <param name="input">補間値</param>
            /// <returns>合成する色</returns>
            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 color = texel * input.color * _TintColor;

                /* 壁や床との交差部分を深度差でぼかし、板ポリの切れ目を見せない */
                float sceneDepth = LinearEyeDepth(LoadCameraDepth(input.positionCS.xy), _ZBufferParams);
                float particleDepth = LinearEyeDepth(input.positionWS, UNITY_MATRIX_V);
                color.a *= (half)saturate((sceneDepth - particleDepth) / _SoftDistance);

                /* 光源と同じ物理輝度で露出を掛け、加算と半透明のどちらでも乗算済みで出力する */
                half3 rgb = color.rgb * (_Intensity * GetCurrentExposureMultiplier()) * color.a;

                /* 霧の奥では霧の透過率だけ減衰させる */
                float3 transmittance = ShinzuiFog(1, input.positionCS, input.positionWS) - ShinzuiFog(0, input.positionCS, input.positionWS);
                return half4(rgb * (half3)transmittance, color.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
