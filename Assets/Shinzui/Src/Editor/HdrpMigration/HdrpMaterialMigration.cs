using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.Editor.HdrpMigration
{
    /// <summary>保存済み材質とインポート材質をHDRPへ移行する</summary>
    public static class HdrpMaterialMigration
    {
        private static readonly Dictionary<string, string> KnownShaders = new Dictionary<string, string>
        {
            { "933532a4fcc9baf4fa0491de14d08ed7", "Universal Render Pipeline/Lit" },
            { "650dd9526735d5b46b79224bc6e94025", "Universal Render Pipeline/Unlit" },
            { "8d2bb70cbf9db8d4da26e15b26e74248", "Universal Render Pipeline/Simple Lit" }
        };

        /// <summary>URP材質を保存値から変換する</summary>
        [MenuItem("Shinzui/HDRP Migration/3 Convert Materials")]
        public static void ConvertMaterials()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before migration");
            int changed = 0, skipped = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                /* インポート内の材質は再インポートで生成する */
                if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;
                string source = SourceShader(material, path);
                if (!source.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal))
                {
                    if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                    {
                        Debug.LogWarning($"HDRP material UNKNOWN: {path}, source={source}");
                        skipped++;
                    }
                    continue;
                }
                ConvertMaterial(material, source, path);
                changed++;
            }
            IncludeRuntimeShaders();
            AssetDatabase.SaveAssets();
            Debug.Log($"HDRP materials: converted={changed}, unknown/skipped={skipped}");
        }

        /// <summary>実行時に名前で取得するポータルシェーダーをビルドへ含める</summary>
        private static void IncludeRuntimeShaders()
        {
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
            int added = 0;
            foreach (string name in new[] { "Custom/PortalShader", "Custom/PortalMask", "Custom/PortalProjection", "Shinzui/SpiderWeb", "HDRP/Unlit" })
            {
                Shader shader = Shader.Find(name);
                if (shader == null) { Debug.LogWarning($"HDRP runtime shader missing: {name}"); continue; }
                bool found = false;
                for (int i = 0; i < shaders.arraySize; i++) found |= shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader;
                if (found) continue;
                int index = shaders.arraySize++;
                shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                added++;
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"HDRP runtime shaders included: added={added}");
        }

        /// <summary>シェーダー参照のGUIDとfileIDから元の種類を取得する</summary>
        /// <param name="material">対象材質</param>
        /// <param name="path">材質パス</param>
        /// <returns>シェーダー名または未解決参照</returns>
        private static string SourceShader(Material material, string path)
        {
            var serialized = new SerializedObject(material);
            var shader = serialized.FindProperty("m_Shader").objectReferenceValue as Shader;
            if (shader != null && shader.name != "Hidden/InternalErrorShader") return shader.name;

            /* 消失参照はSerializedPropertyからGUIDを取得できないため保存テキストを読み取る */
            Match reference = Regex.Match(File.ReadAllText(path), @"m_Shader:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([a-f0-9]+)");
            if (!reference.Success) return "missing m_Shader reference";
            string guid = reference.Groups[2].Value;
            if (KnownShaders.TryGetValue(guid, out string known)) return known;
            string shaderPath = AssetDatabase.GUIDToAssetPath(guid);
            if (shaderPath.EndsWith(".shader") && File.Exists(shaderPath))
            {
                Match name = Regex.Match(File.ReadAllText(shaderPath), "Shader\\s+\"([^\"]+)\"");
                if (name.Success) return name.Groups[1].Value;
            }
            return $"guid={guid}, fileID={reference.Groups[1].Value}, path={shaderPath}";
        }

        /// <summary>保存プロパティをHDRPの表面設定へ変換する</summary>
        /// <param name="material">更新対象</param>
        /// <param name="source">元シェーダー名</param>
        /// <param name="path">診断用パス</param>
        private static void ConvertMaterial(Material material, string source, string path)
        {
            var saved = new SavedMaterial(material);
            bool unlit = source.Contains("Unlit");
            bool particles = source.Contains("Particles");
            float surface = saved.Float("_Surface", particles ? 1 : 0);
            float blend = saved.Float("_Blend", 0);
            int queue = new SerializedObject(material).FindProperty("m_CustomRenderQueue").intValue;
            material.shader = Shader.Find(unlit ? "HDRP/Unlit" : "HDRP/Lit");
            material.shaderKeywords = Array.Empty<string>();

            /* 色とUV変換を維持し旧キーワードをHDRPの検証で再構築する */
            if (!unlit) material.SetColor("_BaseColor", saved.Color("_BaseColor", saved.Color("_Color", Color.white)));
            saved.Texture(material, "_BaseMap", unlit ? "_UnlitColorMap" : "_BaseColorMap", "_MainTex");
            if (unlit) material.SetColor("_UnlitColor", saved.Color("_BaseColor", saved.Color("_Color", Color.white)));
            material.SetFloat("_SurfaceType", surface);
            material.SetFloat("_BlendMode", blend == 2 ? 1 : blend == 1 ? 4 : 0);
            material.SetFloat("_AlphaCutoffEnable", saved.Float("_AlphaClip", 0));
            material.SetFloat("_AlphaCutoff", saved.Float("_Cutoff", .5f));
            material.SetFloat("_DoubleSidedEnable", saved.Float("_Cull", 2) == 0 ? 1 : 0);
            material.SetFloat("_DoubleSidedNormalMode", 1);
            material.SetFloat("_ZWrite", surface == 0 ? 1 : 0);
            if (blend == 3) Debug.LogWarning($"HDRP material: Multiply blend approximated with Alpha: {path}");
            if (saved.Float("_Cull", 2) == 1) Debug.LogWarning($"HDRP material: front-face culling requires review: {path}");
            if (!unlit)
            {
                material.SetFloat("_Metallic", saved.Float("_Metallic", 0));
                material.SetFloat("_Smoothness", saved.Float("_Smoothness", .5f));
                material.SetFloat("_NormalScale", saved.Float("_BumpScale", 1));
                saved.Texture(material, "_BumpMap", "_NormalMap");
                /* パック規則とUVが一致する場合だけマスクを生成する */
                PackMask(material, saved, path);
            }
            Color emission = saved.Color("_EmissionColor", Color.black);
            material.SetColor("_EmissiveColor", emission);
            material.SetColor("_EmissiveColorLDR", emission.maxColorComponent > 0 ? emission / emission.maxColorComponent : Color.black);
            material.SetFloat("_UseEmissiveIntensity", 0);
            material.SetFloat("_EmissiveIntensityUnit", 0);
            material.SetFloat("_EmissiveIntensity", emission.maxColorComponent);
            saved.Texture(material, "_EmissionMap", "_EmissiveColorMap");
            HDMaterial.ValidateMaterial(material);
            /* 明示キューは維持しHDRP既定キューは検証結果を使う */
            if (queue >= 0) material.renderQueue = queue;
            EditorUtility.SetDirty(material);
            Debug.Log($"HDRP material converted: {path}, {source} -> {material.shader.name}, emission={emission.maxColorComponent} nits");
        }

        /// <summary>URPの金属Rと滑らかさAおよび遮蔽GをHDRPのRGBAへ詰め直す</summary>
        /// <param name="material">更新先</param>
        /// <param name="saved">旧設定</param>
        /// <param name="path">元材質パス</param>
        private static void PackMask(Material material, SavedMaterial saved, string path)
        {
            var metal = saved.Map("_MetallicGlossMap");
            var occlusion = saved.Map("_OcclusionMap");
            bool baseAlpha = saved.Float("_SmoothnessTextureChannel", 0) == 1;
            var alpha = baseAlpha ? saved.Map("_BaseMap") : metal;
            if (metal == null && occlusion == null && !baseAlpha) return;
            if (!saved.SameUV("_MetallicGlossMap") || !saved.SameUV("_OcclusionMap") || saved.Float("_WorkflowMode", 1) == 0)
            {
                Debug.LogWarning($"HDRP mask skipped: incompatible UV/specular workflow, scalar metallic/smoothness retained, AO needs review: {path}");
                return;
            }
            if (metal == null && occlusion == null && alpha == null) return;
            int width = new[] { metal, occlusion, alpha }.Where(t => t != null).Max(t => t.width);
            int height = new[] { metal, occlusion, alpha }.Where(t => t != null).Max(t => t.height);
            var m = ReadPixels(metal, width, height);
            var o = ReadPixels(occlusion, width, height);
            var a = ReadPixels(alpha, width, height);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(m == null ? saved.Float("_Metallic", 0) : m[i].r,
                    o == null ? 1 : Mathf.Lerp(1, o[i].g, saved.Float("_OcclusionStrength", 1)), 1,
                    (a == null ? 1 : a[i].a) * saved.Float("_Smoothness", .5f));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            texture.SetPixels(pixels);
            texture.Apply();
            string maskPath = Path.ChangeExtension(path, null) + "_HDRPMask.png";
            File.WriteAllBytes(maskPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(maskPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(maskPath);
            importer.sRGBTexture = false;
            importer.wrapMode = (metal != null ? metal : occlusion != null ? occlusion : alpha).wrapMode;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            material.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
            material.SetFloat("_MetallicRemapMin", 0);
            material.SetFloat("_MetallicRemapMax", 1);
            material.SetFloat("_SmoothnessRemapMin", 0);
            material.SetFloat("_SmoothnessRemapMax", 1);
            material.SetFloat("_AORemapMin", 0);
            material.SetFloat("_AORemapMax", 1);
        }

        /// <summary>インポート設定を変えずGPU経由でリニア画素を取得する</summary>
        /// <param name="source">元画像</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <returns>リニア画素または未指定</returns>
        private static Color[] ReadPixels(Texture source, int width, int height)
        {
            if (source == null) return null;
            RenderTexture previous = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var texture = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                return texture.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>glTFを再生成しFBXの内蔵材質と外部参照も検証する</summary>
        [MenuItem("Shinzui/HDRP Migration/4 Reimport glTF")]
        public static void ReimportModels()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before migration");
            HdrpGraphMigration.MigrateDust();
            int imported = 0, checkedMaterials = 0, unknown = 0;
            /* glTFastの複数パイプライン対応グラフを現行HDRPで再生成する */
            foreach (string graph in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Packages/com.unity.cloud.gltfast/") && p.EndsWith(".shadergraph")))
                AssetDatabase.ImportAsset(graph, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat")) continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Match source = Regex.Match(File.ReadAllText(path), @"m_Shader:.*guid:\s*([a-f0-9]+)");
                string shaderPath = AssetDatabase.GUIDToAssetPath(source.Groups[1].Value);
                if (!shaderPath.StartsWith("Packages/com.unity.cloud.gltfast/")) continue;
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (shader == null) { Debug.LogWarning($"HDRP glTF extracted material unresolved: {path} -> {shaderPath}"); unknown++; continue; }
                material.shader = shader;
                HDMaterial.ValidateMaterial(material);
                EditorUtility.SetDirty(material);
                Debug.Log($"HDRP glTF extracted shader refreshed: {path} -> {shaderPath}");
            }
            AssetDatabase.SaveAssets();
            foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") &&
                new[] { ".glb", ".gltf", ".fbx" }.Contains(Path.GetExtension(p).ToLowerInvariant())))
            {
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    imported++;
                }
                var materials = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToList();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null) materials.AddRange(model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials));
                if (AssetImporter.GetAtPath(path) is ModelImporter importer)
                    materials.AddRange(importer.GetExternalObjectMap().Values.OfType<Material>());
                foreach (Material material in materials.Where(m => m != null).Distinct())
                {
                    checkedMaterials++;
                    string pipeline = material.GetTag("RenderPipeline", false, "");
                    if (pipeline != "HDRenderPipeline")
                    {
                        unknown++;
                        Debug.LogWarning($"HDRP model material REVIEW: {path} / {material.name}, shader={material.shader?.name}, materialPath={AssetDatabase.GetAssetPath(material)}, pipeline={pipeline}");
                    }
                }
                Debug.Log($"HDRP model checked: {path}, materials={materials.Distinct().Count()}");
            }
            Debug.Log($"HDRP models: glTF reimported={imported}, materials checked={checkedMaterials}, non-HDRP={unknown}");
            HdrpAssetCleanup.AuditGraphs();
        }

        /// <summary>消失シェーダーでも読み取れる材質の保存値</summary>
        private sealed class SavedMaterial
        {
            private readonly SerializedObject serialized;
            /// <summary>変更前の材質を保持する</summary>
            /// <param name="material">元材質</param>
            public SavedMaterial(Material material) { serialized = new SerializedObject(material); }
            /// <summary>名前で保存要素を検索する</summary>
            /// <param name="array">保存配列</param>
            /// <param name="name">プロパティ名</param>
            /// <returns>保存要素または未指定</returns>
            private SerializedProperty Find(string array, string name)
            {
                var values = serialized.FindProperty("m_SavedProperties." + array);
                for (int i = 0; i < values.arraySize; i++)
                {
                    var item = values.GetArrayElementAtIndex(i);
                    if (item.FindPropertyRelative("first").stringValue == name) return item.FindPropertyRelative("second");
                }
                return null;
            }
            /// <summary>保存数値を取得する</summary>
            /// <param name="name">名前</param>
            /// <param name="fallback">未指定時の値</param>
            /// <returns>保存値</returns>
            public float Float(string name, float fallback) => Find("m_Floats", name)?.floatValue ?? fallback;
            /// <summary>保存色を取得する</summary>
            /// <param name="name">名前</param>
            /// <param name="fallback">未指定時の値</param>
            /// <returns>保存値</returns>
            public Color Color(string name, Color fallback) => Find("m_Colors", name)?.colorValue ?? fallback;
            /// <summary>保存画像を取得する</summary>
            /// <param name="name">名前</param>
            /// <returns>画像または未指定</returns>
            public Texture Map(string name) => Find("m_TexEnvs", name)?.FindPropertyRelative("m_Texture").objectReferenceValue as Texture;
            /// <summary>基準画像とUVが一致するか判定する</summary>
            /// <param name="name">名前</param>
            /// <returns>基準UVとの一致</returns>
            public bool SameUV(string name)
            {
                if (Map(name) == null) return true;
                var basis = Find("m_TexEnvs", "_BaseMap");
                var other = Find("m_TexEnvs", name);
                return basis == null || (basis.FindPropertyRelative("m_Scale").vector2Value == other.FindPropertyRelative("m_Scale").vector2Value &&
                    basis.FindPropertyRelative("m_Offset").vector2Value == other.FindPropertyRelative("m_Offset").vector2Value);
            }
            /// <summary>画像とタイリングを設定する</summary>
            /// <param name="target">更新材質</param>
            /// <param name="source">旧名</param>
            /// <param name="destination">新名</param>
            /// <param name="fallback">代替旧名</param>
            public void Texture(Material target, string source, string destination, string fallback = null)
            {
                var value = Find("m_TexEnvs", source) ?? (fallback == null ? null : Find("m_TexEnvs", fallback));
                if (value == null || !target.HasProperty(destination)) return;
                target.SetTexture(destination, value.FindPropertyRelative("m_Texture").objectReferenceValue as Texture);
                target.SetTextureScale(destination, value.FindPropertyRelative("m_Scale").vector2Value);
                target.SetTextureOffset(destination, value.FindPropertyRelative("m_Offset").vector2Value);
            }
        }
    }
}
