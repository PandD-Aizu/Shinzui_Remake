using UnityEditor;
using Shinzui.Infrastructure.Lighting;
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
        /// 中心光と周辺光を持つHDRP用クッキーを生成して保存する
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
                    float value = TunnelLookLighting.FlashlightCookie(radius);
                    pixels[y * size + x] = new Color(value, value, value, 1f);
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
            Debug.Log($"Flashlight cookie rebuilt: {CookiePath}, 512x512 linear, hotspot + 25% broad spill, calibrated peak={TunnelLookLighting.FlashlightCandela(true):F0}cd for 3000 emitted lm");
        }
    }
}
