using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Shinzui.Editor
{
    /// <summary>実物のマテリアルを使用したアイテムプレビューを生成する</summary>
    public static class ItemPreviewIcons
    {
        private const string OutputFolder = "Assets/Shinzui/Sprites/ItemPreviews";

        /// <summary>モデルが存在するアイテムのアイコンとAddressables登録を更新する</summary>
        [MenuItem("Shinzui/Items/Rebuild Preview Icons")]
        public static void Rebuild()
        {
            Directory.CreateDirectory(OutputFolder);

            // 取得用プレハブのメッシュとマテリアルから共通UI用画像を生成する
            Generate("penlight", "Penlight");
            Generate("stone", "StonePrefab");
            Generate("match_stick", "MatchPrefab");
            Generate("potion_botamochi", "Botamochi");
            Generate("FlashlightBattery", "FlashlightBattery");
            AssetDatabase.SaveAssets();
        }

        /// <summary>プレハブを透過背景で撮影してSpriteとして登録する</summary>
        /// <param name="itemId">アイテムID</param>
        /// <param name="prefabName">取得用プレハブ名</param>
        private static void Generate(string itemId, string prefabName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Shinzui/Prefabs/{prefabName}.prefab");
            var preview = new PreviewRenderUtility();
            Texture2D texture = null;
            var previousTarget = RenderTexture.active;
            try
            {
                // プレビュー専用シーンにモデルを配置して全体の描画範囲を求める
                var instance = preview.InstantiatePrefabInScene(prefab);
                var renderers = instance.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    renderer.gameObject.layer = 0;
                }

                foreach (var light in instance.GetComponentsInChildren<Light>())
                    light.enabled = false;

                // 斜め前から照明を当て、モデル全体を正方形に収める
                float radius = Mathf.Max(bounds.extents.magnitude, .01f);
                var camera = preview.camera;
                camera.orthographic = true;
                camera.orthographicSize = radius * 1.1f;
                camera.nearClipPlane = radius * .01f;
                camera.farClipPlane = radius * 10f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.transform.rotation = Quaternion.Euler(15f, -25f, 0f);
                camera.transform.position = bounds.center - camera.transform.forward * radius * 4f;
                preview.lights[0].intensity = 3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(30f, -40f, 0f);
                preview.lights[1].intensity = 2f;
                preview.lights[1].transform.rotation = Quaternion.Euler(340f, 140f, 0f);
                preview.ambientColor = new Color(.35f, .35f, .35f);

                // URPを有効にして元のマテリアルを描画し、透過PNGへ保存する
                preview.BeginPreview(new Rect(0, 0, 256, 256), GUIStyle.none);
                preview.Render(true);
                var rendered = (RenderTexture)preview.EndPreview();
                RenderTexture.active = rendered;
                texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                texture.Apply();
                File.WriteAllBytes($"{OutputFolder}/{itemId}.png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (texture != null) Object.DestroyImmediate(texture);
                preview.Cleanup();
            }

            // 既存のSpriteロード経路でビルド後も利用できるように登録する
            string path = $"{OutputFolder}/{itemId}.png";
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), settings.DefaultGroup);
            entry.SetAddress($"Sprite/ItemPreview/{itemId}");
            EditorUtility.SetDirty(settings);
        }
    }
}
