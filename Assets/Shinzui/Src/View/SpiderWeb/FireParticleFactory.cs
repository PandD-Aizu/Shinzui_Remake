using UnityEngine;

namespace Shinzui.View
{
    /// <summary>
    /// 炎・火の粉・煙の粒子で共用する実行時マテリアルとテクスチャを生成する
    /// 生成物の破棄は呼び出し側が担当する
    /// </summary>
    public static class FireParticleFactory
    {
        private const string ShaderName = "Shinzui/BurnParticle";

        /// <summary>
        /// 炎の舌のように縦長で揺らいだ粒子テクスチャを生成する
        /// </summary>
        /// <returns>生成したテクスチャ</returns>
        public static Texture2D CreateFlameTexture()
        {
            return CreateTexture("Fire Flame Particle", 64, (x, y) =>
            {
                // 下側が丸く膨らみ上へ細く伸びる涙滴形。外周は全方向で透明まで落とす
                float radius = 0.3f * Mathf.Sin(Mathf.PI * Mathf.Pow(y, 0.6f));
                float wobble = Mathf.Sin(y * 11.0f) * 0.04f * y;
                float across = Mathf.Abs(x - 0.5f + wobble) / Mathf.Max(0.001f, radius);
                float body = Mathf.Clamp01(1.0f - across * across);
                body *= Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(y / 0.18f)) *
                        (1.0f - Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01((y - 0.6f) / 0.4f)));
                body *= 0.75f + 0.25f * Mathf.PerlinNoise(x * 9.0f, y * 6.0f);
                return body * body;
            });
        }

        /// <summary>
        /// 火の粉や光の芯に使う中心の強い円形テクスチャを生成する
        /// </summary>
        /// <returns>生成したテクスチャ</returns>
        public static Texture2D CreateGlowTexture()
        {
            return CreateTexture("Fire Glow Particle", 32, (x, y) =>
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)) * 2.0f;
                float glow = Mathf.Clamp01(1.0f - distance);
                return glow * glow * glow;
            });
        }

        /// <summary>
        /// 輪郭が不規則にちぎれた煙のテクスチャを生成する
        /// </summary>
        /// <returns>生成したテクスチャ</returns>
        public static Texture2D CreateSmokeTexture()
        {
            return CreateTexture("Fire Smoke Particle", 64, (x, y) =>
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)) * 2.0f;
                float noise = Mathf.PerlinNoise(x * 5.0f + 3.1f, y * 5.0f + 7.7f) * 0.65f +
                              Mathf.PerlinNoise(x * 11.0f + 1.3f, y * 11.0f + 4.2f) * 0.35f;
                float puff = Mathf.Clamp01(1.0f - distance * (0.75f + noise * 0.5f));
                return puff * puff * (0.55f + noise * 0.45f);
            });
        }

        /// <summary>
        /// 頂点カラーを反映する燃焼粒子用マテリアルを生成する
        /// </summary>
        /// <param name="texture">粒子テクスチャ</param>
        /// <param name="nits">白い粒子の輝度</param>
        /// <param name="additive">加算合成にするか。falseは乗算済みの半透明</param>
        /// <returns>生成したマテリアル。シェーダーが無い場合はnull</returns>
        public static Material CreateMaterial(Texture2D texture, float nits, bool additive)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = "Fire Particle Runtime Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_Intensity", nits);

            // 炎と火の粉は光を足し、煙は背景を覆う
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", additive
                ? (float)UnityEngine.Rendering.BlendMode.One
                : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.renderQueue = additive ? 3010 : 3005;
            return material;
        }

        /// <summary>
        /// 0から1の座標を受け取る関数でアルファだけのテクスチャを生成する
        /// </summary>
        /// <param name="textureName">テクスチャ名</param>
        /// <param name="size">一辺の画素数</param>
        /// <param name="alpha">画素ごとの不透明度</param>
        /// <returns>生成したテクスチャ</returns>
        private static Texture2D CreateTexture(string textureName, int size, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float value = Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size));
                    pixels[y * size + x] = new Color(1.0f, 1.0f, 1.0f, value);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
