Shader "Custom/UI/DiagonalGradient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0,0,0,1) // デフォルトは黒

        // 煙・霧のアニメーションパラメータ
        _NoiseScale ("Smoke Scale", Float) = 3.5
        _NoiseSpeed ("Smoke Speed", Vector) = (0.08, 0.05, 0, 0)
        _SmokeIntensity ("Smoke Intensity", Range(0, 1)) = 0.65
        _AnimationSpeed ("Animation Speed Multiplier", Float) = 1.0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            // 煙のパラメータ
            float _NoiseScale;
            float2 _NoiseSpeed;
            float _SmokeIntensity;
            float _AnimationSpeed;

            // --- ノイズ関連のヘルパー関数 ---
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            // 2D Value Noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                
                // Hermite補間
                float2 u = f * f * (3.0 - 2.0 * f);
                
                return lerp(lerp(hash(i + float2(0.0, 0.0)), hash(i + float2(1.0, 0.0)), u.x),
                            lerp(hash(i + float2(0.0, 1.0)), hash(i + float2(1.0, 1.0)), u.x), u.y);
            }

            // Fractal Brownian Motion (FBM)
            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                float2 shift = float2(100.0, 100.0);
                float2x2 rot = float2x2(0.8, 0.6, -0.6, 0.8);
                for (int i = 0; i < 3; ++i)
                {
                    v += a * noise(p);
                    p = mul(rot, p) * 2.0 + shift;
                    a *= 0.5;
                }
                return v;
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);

                o.texcoord = v.texcoord;

                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // アスペクト比によるつぶれを防ぐオブジェクト絶対座標（ピクセル単位）ベースのUV
                float2 objUV = IN.worldPosition.xy * 0.01;

                // アニメーション用の時間（倍率適用）
                float t = _Time.y * _AnimationSpeed;

                // 2系統の別々のスクロールノイズを計算して湧き出しアニメーション（干渉）を作る
                float2 noiseUV1 = objUV * _NoiseScale + t * _NoiseSpeed;
                float2 noiseUV2 = objUV * (_NoiseScale * 1.3) - t * (_NoiseSpeed * 1.5) + float2(0.3, 0.7);
                
                float n1 = fbm(noiseUV1);
                float n2 = fbm(noiseUV2);
                
                // 2つのノイズの合成（干渉パターン）による煙のうねり歪み
                float distortion = (n1 + n2) * 0.5;
                
                // グラデーション自体の歪み
                float2 distortedUV = IN.texcoord + (distortion - 0.5) * _SmokeIntensity * 0.15;

                // 歪んだ正規化UVでメインテクスチャをサンプル
                half4 color = (tex2D(_MainTex, distortedUV) + _TextureSampleAdd) * IN.color;

                // 左上から右下への基本グラデーション
                float gradient = ((1.0 - distortedUV.x) + distortedUV.y) * 0.5;
                
                // 霧の密度計算（2系統のノイズの積をブレンドすることで、煙が湧き立つように動く）
                float2 denseUV1 = objUV * (_NoiseScale * 1.5) + t * _NoiseSpeed * 1.8;
                float2 denseUV2 = objUV * (_NoiseScale * 1.9) - t * _NoiseSpeed * 2.3 + float2(0.1, 0.8);
                
                float d1 = fbm(denseUV1);
                float d2 = fbm(denseUV2);
                
                // ノイズの積を乗算することで、湧き出すムラ（干渉波）を発生させる
                float smokeDensity = lerp(d1, d1 * d2 * 2.2, 0.7);
                float smokeFactor = lerp(1.0 - _SmokeIntensity * 0.8, 1.0 + _SmokeIntensity * 0.45, smokeDensity);
                
                // 最終アルファの適用
                color.a *= saturate(gradient) * saturate(smokeFactor);

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDHLSL
        }
    }
}
