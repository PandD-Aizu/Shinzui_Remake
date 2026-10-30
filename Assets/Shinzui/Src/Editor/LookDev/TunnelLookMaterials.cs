using System;
using System.IO;
using System.Linq;
using Shinzui.Editor.HdrpMigration;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace Shinzui.Editor.LookDev
{
    /// <summary>トンネル用PBR材質と湿気のデカールをGUIDを保って構築する</summary>
    public static class TunnelLookMaterials
    {
        internal const string ResourceFolder = "Assets/Shinzui/Resources/TunnelLook";
        private const string TextureFolder = "Assets/Shinzui/Textures/Tunnel";
        private const string Fallback = "Assets/Shinzui/Sprites/TunnelTempTex/concrete_floor_damaged_01_";

        /// <summary>面ごとの材質を実寸のトライプラナー投影で構築する</summary>
        [MenuItem("Shinzui/LookDev/1 Build Tunnel Materials")]
        public static void BuildMaterials()
        {
            HdrpVerificationAssets.RequireSavedScenes();
            EnsureFolder(ResourceFolder);
            EnsureFolder(TextureFolder + "/Generated");
            string detailPath = TextureFolder + "/Generated/TunnelDetail.png";
            WriteTexture(detailPath, 256, (u, v) =>
            {
                float noise = Mathf.PerlinNoise(u * 32f, v * 32f);
                return new Color(0.5f + (noise - 0.5f) * 0.12f, 0.5f, 0.5f + (noise - 0.5f) * 0.08f, 0.5f);
            });
            Texture2D detail = ImportTexture(detailPath, false);
            /* HDRPの詳細法線はAにX成分とGにY成分を格納する */
            string roadDetailPath = TextureFolder + "/Generated/TunnelRoadDetail.png";
            WriteTexture(roadDetailPath, 256, (u, v) =>
            {
                const float step = 1f / 256f;
                float grain = Mathf.PerlinNoise(u * 32f, v * 32f);
                float dx = Mathf.PerlinNoise((u + step) * 32f, v * 32f) - Mathf.PerlinNoise((u - step) * 32f, v * 32f);
                float dy = Mathf.PerlinNoise(u * 32f, (v + step) * 32f) - Mathf.PerlinNoise(u * 32f, (v - step) * 32f);
                Vector3 normal = new Vector3(-dx * 2f, -dy * 2f, 1f).normalized;
                return new Color(0.5f + (grain - 0.5f) * 0.25f, normal.y * 0.5f + 0.5f,
                    0.5f + (grain - 0.5f) * 0.3f, normal.x * 0.5f + 0.5f);
            });
            Texture2D roadDetail = ImportTexture(roadDetailPath, false);
            string[] sets = { "concrete_slab_wall_02", "asphalt_03", "damaged_concrete_floor", "concrete_wall_008" };
            string[] names = { "TunnelWall", "TunnelRoad", "TunnelWalkway", "TunnelCeiling" };
            int fallbackCount = 0;
            for (int i = 0; i < sets.Length; i++)
            {
                string diffusePath = FindSource(sets[i], "diff");
                string normalPath = FindSource(sets[i], "nor_gl");
                string armPath = FindSource(sets[i], "arm");
                string roughPath = FindSource(sets[i], "rough");

                bool complete = diffusePath != null && normalPath != null && (armPath != null || roughPath != null);
                if (!complete)
                {
                    fallbackCount++;
                    Debug.LogWarning($"Tunnel material fallback: {names[i]}, expected {TextureFolder}/{sets[i]}/{sets[i]}_{{diff,nor_gl,arm}}_2k.jpg (PNG/EXR and separate roughness also supported); using {Fallback}*_1k.png");
                    diffusePath = Fallback + "diff_1k.png";
                    normalPath = Fallback + "nor_gl_1k.png";
                    armPath = Fallback + "arm_1k.png";
                }
                Texture2D diffuse = ImportTexture(diffusePath, true);
                Texture2D normal = ImportTexture(normalPath, false, true);
                string packedSource = armPath ?? roughPath;
                string maskPath = Path.GetDirectoryName(packedSource).Replace('\\', '/') + "/" + Path.GetFileNameWithoutExtension(packedSource) + "_HDRPMask.png";
                Texture2D mask = BuildMask(packedSource, maskPath, armPath != null);
                Material material = GetMaterial(ResourceFolder + "/" + names[i] + ".mat", "HDRP/Lit");
                material.SetTexture("_BaseColorMap", diffuse);
                /* 元画像の茶色を抑え路面だけを湿った中立色へ寄せる */
                material.SetColor("_BaseColor", i == 1 ? new Color(0.55f, 0.64f, 0.78f, 1f) : Color.white);
                material.SetTexture("_NormalMap", normal);
                material.SetFloat("_NormalScale", i == 1 ? 1.25f : 0.75f);
                material.SetTexture("_MaskMap", mask);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_MetallicRemapMin", 0f);
                material.SetFloat("_MetallicRemapMax", 1f);
                material.SetFloat("_AORemapMin", 0.35f);
                material.SetFloat("_AORemapMax", 1f);
                material.SetFloat("_Smoothness", 0.3f);
                material.SetFloat("_SmoothnessRemapMin", i == 1 ? 0.18f : 0.05f);
                material.SetFloat("_SmoothnessRemapMax", i == 1 ? 0.68f : 0.65f);
                /* 2.5mで一周するワールド座標投影により変形後のUV伸びを回避する */
                material.SetFloat("_UVBase", 5f);
                material.SetFloat("_ObjectSpaceUVMapping", 0f);
                material.SetFloat("_TexWorldScale", 0.4f);
                material.SetTextureScale("_BaseColorMap", Vector2.one);
                material.SetTextureOffset("_BaseColorMap", Vector2.zero);
                material.SetFloat("_NormalMapSpace", 0f);
                material.SetTexture("_DetailMap", i == 1 ? roadDetail : detail);
                material.SetTextureScale("_DetailMap", Vector2.one * 8f);
                material.SetFloat("_LinkDetailsWithBase", 1f);
                material.SetFloat("_DetailAlbedoScale", 0.3f);
                material.SetFloat("_DetailNormalScale", i == 1 ? 0.65f : 0.15f);
                material.SetFloat("_DetailSmoothnessScale", 0.2f);
                material.SetFloat("_SupportDecals", 1f);
                material.SetFloat("_ReceivesSSR", 1f);
                material.SetFloat("_EnableGeometricSpecularAA", 1f);
                material.SetFloat("_SpecularOcclusionMode", 1f);
                /* 未使用の高さマップとキーワードを外して変位のない描画に揃える */
                material.SetTexture("_HeightMap", null);
                material.SetFloat("_DisplacementMode", 0f);
                HDMaterial.ValidateMaterial(material);
                EditorUtility.SetDirty(material);
                Debug.Log($"Tunnel material built: {AssetDatabase.GetAssetPath(material)}, diffuse={diffusePath}, normal={normalPath}, world tiling=2.5m, mask={maskPath}, normal scale={material.GetFloat("_NormalScale")}, displacement=off, height keyword={material.IsKeywordEnabled("_HEIGHTMAP")}");
            }
            /* 旧材質を参照する静的シーンでもUV修正を適用する */
            string fallbackPath = "Assets/Shinzui/Materials/TunnelTempMaterial.mat";
            Material fallback = GetMaterial(fallbackPath, "HDRP/Lit");
            fallback.CopyPropertiesFromMaterial(AssetDatabase.LoadAssetAtPath<Material>(ResourceFolder + "/TunnelWall.mat"));
            HDMaterial.ValidateMaterial(fallback);
            EditorUtility.SetDirty(fallback);
            int assigned = 0;
            /* 静的シーンとプレハブの既知のトンネル部品も同じ材質へ揃える */
            HdrpVerificationAssets.VisitAssets((root, path, persistent) =>
            {
                bool changed = false;
                foreach (HDAdditionalCameraData camera in root.GetComponentsInChildren<HDAdditionalCameraData>(true))
                {
                    if (!camera.customRenderingSettings) continue;
                    camera.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.Decals, true);
                    camera.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.Decals] = true;
                    EditorUtility.SetDirty(camera);
                    if (PrefabUtility.IsPartOfPrefabInstance(camera)) PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
                    changed = true;
                    Debug.Log($"Tunnel camera decals enabled: {path} / {HdrpVerificationAssets.Hierarchy(camera.transform)}");
                }
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string name = renderer.name.ToLowerInvariant();
                    if (!name.StartsWith("tunnel_") || name.Contains("portal")) continue;
                    string materialName = name.Contains("side_floor") ? "TunnelWalkway" : name.Contains("floor") ? "TunnelRoad"
                        : name.Contains("ceiling") || name.Contains("header") ? "TunnelCeiling" : "TunnelWall";
                    Material target = AssetDatabase.LoadAssetAtPath<Material>(ResourceFolder + "/" + materialName + ".mat");
                    Material[] slots = renderer.sharedMaterials;
                    bool different = false;
                    for (int slot = 0; slot < slots.Length; slot++)
                    {
                        if (slots[slot] == target) continue;
                        slots[slot] = target;
                        different = true;
                    }
                    if (!different) continue;
                    renderer.sharedMaterials = slots;
                    EditorUtility.SetDirty(renderer);
                    if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    assigned++;
                    changed = true;
                    Debug.Log($"Tunnel surface assignment: {path} / {HdrpVerificationAssets.Hierarchy(renderer.transform)} -> {materialName}");
                }
                return changed;
            });
            AssetDatabase.SaveAssets();
            Debug.Log($"Tunnel material summary: surfaces=4, fallback material=1 ({fallbackPath}), fallback sets={fallbackCount}, procedural detail maps=2, static assignments={assigned}; displacement disabled");
        }

        /// <summary>水溜まりと漏水と汚れの投影材質を生成する</summary>
        [MenuItem("Shinzui/LookDev/2 Build Decal Materials")]
        public static void BuildDecals()
        {
            RequireEditMode();
            EnsureFolder(ResourceFolder);
            string folder = TextureFolder + "/Generated";
            EnsureFolder(folder);
            string[] names = { "Puddle", "Leak", "Grime", "Seepage" };
            for (int kind = 0; kind < names.Length; kind++)
            {
                int type = kind;
                string path = folder + "/" + names[kind] + ".png";
                Func<float, float, Color> shape = (u, v) =>
                {
                    float noise = Mathf.PerlinNoise(u * 9f + 11f, v * 9f + 7f);
                    float x = u * 2f - 1f, y = v * 2f - 1f;
                    float edge = Mathf.Clamp01((1f - x * x - y * y + (noise - 0.5f) * 0.7f) * 5f);
                    float alpha = edge * (0.85f + noise * 0.15f);
                    if (type == 1)
                    {
                        float streak = Mathf.PerlinNoise(u * 35f + 4f, v * 2f);
                        alpha *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.7f, streak)) * (0.3f + v * 0.7f);
                    }
                    if (type == 2) alpha *= noise * noise;
                    if (type == 3) alpha *= Mathf.SmoothStep(0f, 1f, noise);
                    return new Color(1f, 1f, 1f, alpha);
                };
                WriteTexture(path, 512, shape);
                string maskPath = folder + "/" + names[kind] + "Mask.png";
                float smoothness = kind == 0 ? 0.92f : kind == 1 ? 0.7f : kind == 2 ? 0.18f : 0.55f;
                WriteTexture(maskPath, 512, (u, v) => new Color(0f, 1f, shape(u, v).a, smoothness));
                Material material = GetMaterial(ResourceFolder + "/" + names[kind] + ".mat", "HDRP/Decal");
                material.SetTexture("_BaseColorMap", ImportTexture(path, true, false, true));
                material.SetColor("_BaseColor", kind == 0 ? new Color(0.018f, 0.022f, 0.025f, 0.65f) : new Color(0.025f, 0.03f, 0.028f, 0.85f));
                material.SetTexture("_MaskMap", ImportTexture(maskPath, false, false, true));
                material.SetFloat("_DecalBlend", kind == 0 ? 0.95f : 0.85f);
                material.SetFloat("_DecalMaskMapBlueScale", 1f);
                material.SetFloat("_SmoothnessRemapMin", 0f);
                material.SetFloat("_SmoothnessRemapMax", 1f);
                material.SetInt("_DrawOrder", kind == 0 ? 20 : 10);
                material.enableInstancing = true;
                material.SetFloat("_MaskBlendSrc", 1f);
                material.SetFloat("_NormalBlendSrc", 1f);
                material.SetFloat("_AffectAlbedo", 1f);
                material.SetFloat("_AffectNormal", kind == 0 ? 1f : 0f);
                material.SetFloat("_AffectMetal", 0f);
                material.SetFloat("_AffectAO", 0f);
                material.SetFloat("_AffectSmoothness", 1f);
                /* 水面の追加法線は平坦にして不要な凹凸を加えない */
                material.SetTexture("_NormalMap", null);
                if (kind == 0)
                {
                    string normalPath = folder + "/WaterNormal.png";
                    WriteTexture(normalPath, 64, (u, v) => new Color(0.5f, 0.5f, 1f, 1f));
                    material.SetTexture("_NormalMap", ImportTexture(normalPath, false, true, true));
                }
                HDMaterial.ValidateMaterial(material);
                EditorUtility.SetDirty(material);
                Debug.Log($"Tunnel decal built: {AssetDatabase.GetAssetPath(material)}, albedo/opacity={path}, mask={maskPath}, smoothness={smoothness}, blend={material.GetFloat("_DecalBlend")}, mask opacity=blue, base opacity={material.GetColor("_BaseColor").a}");
            }
            TunnelLookEnvironment.EnsureDecalRendering();
            AssetDatabase.SaveAssets();
            Debug.Log("Tunnel decal summary: materials=4, generated textures=9, skipped=0; runtime density/count controlled by TunnelGenerator");
        }

        /// <summary>セット名を保った画像を指定フォルダー以下から解決する</summary>
        /// <param name="set">セット名</param>
        /// <param name="channel">画像の用途</param>
        /// <returns>対応画像のパスまたはnull</returns>
        private static string FindSource(string set, string channel)
        {
            string expected = $"{TextureFolder}/{set}/{set}_{channel}_2k.jpg";
            if (File.Exists(expected)) return expected;
            return Directory.GetFiles(TextureFolder, $"{set}_{channel}_2k.*", SearchOption.AllDirectories)
                .Where(p => new[] { ".jpg", ".png", ".exr" }.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .OrderBy(p => p, StringComparer.Ordinal).Select(p => p.Replace('\\', '/')).FirstOrDefault();
        }

        /// <summary>ARMまたは粗さ画像をHDRP用の線形マスクへ変換する</summary>
        /// <param name="sourcePath">入力画像</param>
        /// <param name="outputPath">出力PNG</param>
        /// <param name="packed">ARM画像か</param>
        /// <returns>線形空間のマスクテクスチャ</returns>
        private static Texture2D BuildMask(string sourcePath, string outputPath, bool packed)
        {
            Texture2D source = ImportTexture(sourcePath, false);
            Color32[] pixels = ReadLinearPixels(source);
            byte minSmoothness = 255, maxSmoothness = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 input = pixels[i];
                pixels[i] = packed ? new Color32(input.b, input.r, 255, (byte)(255 - input.g))
                    : new Color32(0, 255, 255, (byte)(255 - input.r));
                minSmoothness = (byte)Mathf.Min(minSmoothness, pixels[i].a);
                maxSmoothness = (byte)Mathf.Max(maxSmoothness, pixels[i].a);
            }
            Debug.Log($"Tunnel mask verified: {outputPath}, linear R=metal G=AO B=detail A=1-roughness, smoothness={minSmoothness / 255f:F3}..{maxSmoothness / 255f:F3}");
            var output = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            output.SetPixels32(pixels);
            output.Apply();
            SavePng(outputPath, output.EncodeToPNG());
            Object.DestroyImmediate(output);
            if (!packed) Debug.Log($"Tunnel mask from roughness: {sourcePath} -> {outputPath}; no baked AO supplied, AO=1, metallic=0");
            return ImportTexture(outputPath, false);
        }

        /// <summary>EXRを含む画像をReadable設定を変えず線形の画素として読む</summary>
        /// <param name="source">線形インポート済み画像</param>
        /// <returns>PNG保存用の画素配列</returns>
        private static Color32[] ReadLinearPixels(Texture2D source)
        {
            RenderTexture previous = RenderTexture.active;
            bool previousSrgbWrite = GL.sRGBWrite;
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            try
            {
                GL.sRGBWrite = false;
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                readback.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSrgbWrite;
                RenderTexture.ReleaseTemporary(temporary);
                Object.DestroyImmediate(readback);
            }
        }

        /// <summary>既存の材質を再利用しシェーダーを設定する</summary>
        /// <param name="path">保存先</param>
        /// <param name="shaderName">シェーダー名</param>
        /// <returns>永続材質</returns>
        internal static Material GetMaterial(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Shader missing: {shaderName}");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            return material;
        }

        /// <summary>画像用途に合わせたインポート設定を適用する</summary>
        /// <param name="path">画像パス</param>
        /// <param name="srgb">色として扱うか</param>
        /// <param name="normal">法線マップか</param>
        /// <param name="clamp">外周を繰り返さないか</param>
        /// <returns>インポートした画像</returns>
        private static Texture2D ImportTexture(string path, bool srgb, bool normal = false, bool clamp = false)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Tunnel texture missing", path);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.convertToNormalmap = false;
            importer.flipGreenChannel = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.anisoLevel = 16;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            if (normal) Debug.Log($"Tunnel normal import: {path}, type={importer.textureType}, sRGB={importer.sRGBTexture}, convertFromHeight={importer.convertToNormalmap}, flipGreen={importer.flipGreenChannel}");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>決定的な手続き画像を生成してPNGとして保存する</summary>
        /// <param name="path">保存先</param>
        /// <param name="size">一辺の画素数</param>
        /// <param name="sample">UVから画素値を作る関数</param>
        private static void WriteTexture(string path, int size, Func<float, float, Color> sample)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) pixels[y * size + x] = sample((x + 0.5f) / size, (y + 0.5f) / size);
            texture.SetPixels(pixels);
            texture.Apply();
            SavePng(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        /// <summary>内容が変わった画像だけを書き込みmetaを維持する</summary>
        /// <param name="path">画像パス</param>
        /// <param name="bytes">PNGデータ</param>
        private static void SavePng(string path, byte[] bytes)
        {
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes)) File.WriteAllBytes(path, bytes);
        }

        /// <summary>フォルダーをUnity経由で再帰的に用意する</summary>
        /// <param name="path">フォルダーパス</param>
        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>実行時の一時状態を保存しないよう再生モードを拒否する</summary>
        internal static void RequireEditMode()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before LookDev asset changes");
        }
    }
}
