using UnityEditor;
using UnityEngine;

namespace Shinzui.Editor
{
    /// <summary>
    /// 懐中電灯の反射板を模した配光テクスチャを作成する
    /// </summary>
    public static class FlashlightBeamAssets
    {
        private const string CookiePath = "Assets/Shinzui/Art/Flashlight/FlashlightBeam.asset";

        /// <summary>
        /// 中心光と周辺光を持つURP用クッキーを生成して保存する
        /// </summary>
        [MenuItem("Shinzui/Flashlight/Rebuild Beam Cookie")]
        public static void RebuildCookie()
        {
            // 線形空間の配光をテクスチャへ焼き込み、実行時の生成負荷を避ける
            const int size = 512;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CookiePath);
            bool isNew = texture == null;
            if (isNew)
            {
                texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
                texture.name = "FlashlightBeam";
            }

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float radius = Mathf.Sqrt(u * u + v * v);
                    float core = Mathf.Exp(-Mathf.Pow(radius / 0.28f, 2.4f));
                    float spill = 0.16f * Mathf.Exp(-radius * radius * 2.4f);
                    float reflector = 0.018f * Mathf.Exp(-Mathf.Pow((radius - 0.53f) / 0.13f, 2f));
                    float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, radius));
                    float value = Mathf.Clamp01((core * 0.84f + spill + reflector) * edge);
                    pixels[y * size + x] = new Color(value, value, value, value);
                }
            }

            // 黒い外周とミップマップで投影境界のちらつきを抑える
            texture.SetPixels(pixels);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.Apply(true, false);
            if (isNew)
            {
                AssetDatabase.CreateAsset(texture, CookiePath);
            }

            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssets();
        }
    }
}
