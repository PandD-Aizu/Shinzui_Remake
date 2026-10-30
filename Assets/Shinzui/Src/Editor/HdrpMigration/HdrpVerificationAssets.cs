using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Shinzui.Editor.HdrpMigration
{
    /// <summary>検証で見つかった欠落材質を元の参照を確認して修復する</summary>
    public static class HdrpVerificationAssets
    {
        private const string DefaultMaterialPath = "Assets/Shinzui/Materials/DefaultHDRPLit.mat";
        /* URP Runtime/Materials/Lit.mat.metaのGUIDとmainObjectFileID */
        private const string UrpDefaultGuid = "31321ba15b8f8eb4c954353edc038b1d";
        private static readonly Dictionary<string, string> TextCache = new();
        private static int fixedSlots, emptySlots, unknownSlots, skippedRenderers;

        /// <summary>URP既定材質への欠落参照だけをHDRP材質へ置き換える</summary>
        [MenuItem("Shinzui/HDRP Migration/7 Fix Null Materials")]
        public static void FixNullMaterials()
        {
            RequireSavedScenes();
            fixedSlots = emptySlots = unknownSlots = skippedRenderers = 0;
            TextCache.Clear();
            var material = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("HDRP/Lit");
                if (shader == null) throw new InvalidOperationException("HDRP/Lit is unavailable");
                material = new Material(shader) { name = "DefaultHDRPLit" };
                material.SetColor("_BaseColor", new Color(0.5f, 0.5f, 0.5f, 1f));
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0.35f);
                HDMaterial.ValidateMaterial(material);
                AssetDatabase.CreateAsset(material, DefaultMaterialPath);
            }

            /* 保存はUnity APIだけを使いYAMLは失われたGUIDの照合に限定する */
            VisitAssets((root, path, persistentRoot) => FixRoot(root, path, persistentRoot, material));
            AssetDatabase.SaveAssets();
            TextCache.Clear();
            Debug.Log($"HDRP null materials: fixed={fixedSlots}, intentionally empty={emptySlots}, unknown/skipped={unknownSlots}, skipped renderers={skippedRenderers}, default={DefaultMaterialPath}");
        }

        /// <summary>未保存の編集とバッチ保存が混在しない状態を確認する</summary>
        internal static void RequireSavedScenes()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before asset migration");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Close Prefab Mode before asset migration");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before asset migration");
        }

        /// <summary>プレハブを依存順に処理してからシーンを巡回する</summary>
        /// <param name="process">階層を処理して変更の有無を返す関数</param>
        internal static void VisitAssets(Func<GameObject, string, GameObject, bool> process)
        {
            RequireSavedScenes();
            int prefabs = 0, scenes = 0, changed = 0, skipped = 0;
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Shinzui" })
                    .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".prefab"))
                    .OrderBy(p => AssetDatabase.GetDependencies(p, true).Count(d => d.EndsWith(".prefab"))).ThenBy(p => p);
                foreach (string path in paths)
                {
                    if (!AssetDatabase.IsOpenForEdit(path)) { Debug.LogWarning($"HDRP skipped read-only: {path}"); skipped++; continue; }
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        if (process(root, path, AssetDatabase.LoadAssetAtPath<GameObject>(path)))
                        {
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                            changed++;
                        }
                        prefabs++;
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Shinzui/Scenes" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!AssetDatabase.IsOpenForEdit(path)) { Debug.LogWarning($"HDRP skipped read-only: {path}"); skipped++; continue; }
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    bool dirty = false;
                    foreach (GameObject root in scene.GetRootGameObjects()) dirty |= process(root, path, null);
                    if (dirty) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); changed++; }
                    scenes++;
                }
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previous);
                Debug.Log($"HDRP asset scan: scenes={scenes}, prefabs={prefabs}, changed={changed}, skipped={skipped}");
            }
        }

        /// <summary>同じ階層位置の永続オブジェクトからローカルIDを得て材質を修復する</summary>
        /// <param name="root">編集する階層</param>
        /// <param name="path">アセットパス</param>
        /// <param name="persistentRoot">プレハブの永続階層</param>
        /// <param name="material">代替材質</param>
        /// <returns>変更した場合はtrue</returns>
        private static bool FixRoot(GameObject root, string path, GameObject persistentRoot, Material material)
        {
            if (root == null)
            {
                LogSkippedRenderer(null, path, "missing root");
                return false;
            }
            bool changed = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    LogSkippedRenderer(renderer, path, "missing renderer");
                    continue;
                }
                var serialized = new SerializedObject(renderer);
                SerializedProperty slots = serialized.FindProperty("m_Materials");
                /* Rendererの種類によって存在しない材質配列を参照しない */
                if (slots == null || !slots.isArray || slots.arraySize == 0)
                {
                    LogSkippedRenderer(renderer, path, "m_Materials missing, not an array or empty");
                    continue;
                }

                Renderer identity = persistentRoot != null ? FindPersistentRenderer(root, persistentRoot, renderer) : renderer;
                if (identity == null)
                {
                    LogSkippedRenderer(renderer, path, "persistent prefab hierarchy/component mismatch");
                    continue;
                }
                for (int i = 0; i < slots.arraySize; i++)
                {
                    SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                    if (slot == null || slot.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        unknownSlots++;
                        Debug.LogWarning($"HDRP null material skipped (invalid serialized slot): {path} / {Hierarchy(renderer.transform)} type={renderer.GetType().Name} slot={i}");
                        continue;
                    }
                    if (slot.objectReferenceValue != null) continue;
                    string reference = FindReference(identity, path, i, new HashSet<Object>());
                    if (reference != null && Regex.IsMatch(reference, @"fileID:\s*0(?:[,}])")) { emptySlots++; continue; }
                    if (reference != null && reference.Contains(UrpDefaultGuid) && Regex.IsMatch(reference, @"fileID:\s*2100000(?:[,}])"))
                    {
                        slot.objectReferenceValue = material;
                        fixedSlots++;
                        changed = true;
                        Debug.Log($"HDRP null material fixed: {path} / {Hierarchy(renderer.transform)} slot={i}, source={reference} -> {DefaultMaterialPath}");
                    }
                    else
                    {
                        unknownSlots++;
                        Debug.LogWarning($"HDRP null material skipped (unproven URP reference): {path} / {Hierarchy(renderer.transform)} slot={i}, reference={reference ?? "unresolved"}");
                    }
                }
                if (serialized.ApplyModifiedPropertiesWithoutUndo() && PrefabUtility.IsPartOfPrefabInstance(renderer))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            return changed;
        }

        /// <summary>プレハブの永続階層に同じ位置のRendererがある場合だけ取得する</summary>
        /// <param name="root">編集する階層</param>
        /// <param name="persistentRoot">プレハブの永続階層</param>
        /// <param name="renderer">対応元のRenderer</param>
        /// <returns>対応するRendererまたは対応できない場合はnull</returns>
        private static Renderer FindPersistentRenderer(GameObject root, GameObject persistentRoot, Renderer renderer)
        {
            /* 名前の重複を避け兄弟インデックスと同一型コンポーネントの順序で対応させる */
            var indices = new Stack<int>();
            for (Transform t = renderer.transform; t != root.transform; t = t.parent)
            {
                if (t == null) return null;
                indices.Push(t.GetSiblingIndex());
            }
            Transform target = persistentRoot.transform;
            foreach (int index in indices)
            {
                if (target == null || index < 0 || index >= target.childCount) return null;
                target = target.GetChild(index);
            }
            if (target == null) return null;
            int componentIndex = Array.IndexOf(renderer.GetComponents(renderer.GetType()), renderer);
            Component[] components = target.GetComponents(renderer.GetType());
            return componentIndex >= 0 && componentIndex < components.Length ? components[componentIndex] as Renderer : null;
        }

        /// <summary>修復できないRendererを理由と階層付きで記録する</summary>
        /// <param name="renderer">処理対象または欠落時はnull</param>
        /// <param name="path">アセットパス</param>
        /// <param name="reason">処理を見送る理由</param>
        private static void LogSkippedRenderer(Renderer renderer, string path, string reason)
        {
            skippedRenderers++;
            string hierarchy = renderer != null ? Hierarchy(renderer.transform) : "<missing>";
            string type = renderer != null ? renderer.GetType().Name : "<missing>";
            Debug.LogWarning($"HDRP renderer skipped: {path} / {hierarchy}, type={type}, reason={reason}");
        }

        /// <summary>直接の材質配列とプレハブ上書きから元の参照を読む</summary>
        /// <param name="renderer">識別対象</param>
        /// <param name="path">参照を持つファイル</param>
        /// <param name="slot">材質スロット</param>
        /// <param name="visited">循環を防ぐ処理済み集合</param>
        /// <returns>元の参照または識別できない場合はnull</returns>
        private static string FindReference(Renderer renderer, string path, int slot, HashSet<Object> visited)
        {
            if (renderer == null || !visited.Add(renderer) || !File.Exists(path)) return null;
            if (!TextCache.TryGetValue(path, out string text)) TextCache[path] = text = File.ReadAllText(path);
            GlobalObjectId global = GlobalObjectId.GetGlobalObjectIdSlow(renderer);
            long localId = (long)global.targetObjectId;
            if (EditorUtility.IsPersistent(renderer) && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer, out string _, out long id)) localId = id;
            string block = ReadBlock(text, localId);
            Match materials = Regex.Match(block, @"(?m)^  m_Materials:\r?\n((?:  - [^\r\n]*\r?\n)*)");
            if (materials.Success)
            {
                MatchCollection refs = Regex.Matches(materials.Groups[1].Value, @"\{[^}]+\}");
                return slot < refs.Count ? refs[slot].Value : null;
            }

            Renderer source = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
            if (source == null || source == renderer) return null;
            string property = $"m_Materials.Array.data[{slot}]";
            PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(renderer);
            var serialized = new SerializedObject(renderer);
            SerializedProperty materialSlot = serialized.FindProperty(property);
            /* 上書き状態を確認できない場合は元プレハブの材質を推測しない */
            if (materialSlot == null || materialSlot.propertyType != SerializedPropertyType.ObjectReference) return null;
            bool overridden = materialSlot.prefabOverride
                || (modifications != null && modifications.Any(m => m != null && m.target == source && m.propertyPath == property));
            if (overridden)
            {
                /* 意図的な空参照の上書きを元プレハブの値で置換しない */
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string sourceGuid, out long sourceId);
                string instance = ReadBlock(text, (long)global.targetPrefabId);
                if (string.IsNullOrEmpty(instance)) return null;
                string pattern = @"target: \{fileID: " + sourceId + @", guid: " + sourceGuid
                    + @", type: \d+\}\r?\n\s+propertyPath: " + Regex.Escape(property)
                    + @"\r?\n\s+value:[^\r\n]*\r?\n\s+objectReference: (\{[^}]+\})";
                Match entry = Regex.Match(instance, pattern);
                return entry.Success ? entry.Groups[1].Value : null;
            }
            return FindReference(source, AssetDatabase.GetAssetPath(source), slot, visited);
        }

        /// <summary>指定ローカルIDのYAMLブロックを読み取り専用で抽出する</summary>
        /// <param name="text">元ファイル</param>
        /// <param name="id">ローカルID</param>
        /// <returns>該当ブロックまたは空文字</returns>
        private static string ReadBlock(string text, long id)
        {
            if (id == 0) return string.Empty;
            return Regex.Match(text, @"(?ms)^--- !u!\d+ &" + id + @"(?: stripped)?\r?\n.*?(?=^--- !u!|\z)").Value;
        }

        /// <summary>ログ用の階層名を組み立てる</summary>
        /// <param name="transform">対象Transform</param>
        /// <returns>親を含む階層名</returns>
        internal static string Hierarchy(Transform transform)
        {
            return transform.parent == null ? transform.name : Hierarchy(transform.parent) + "/" + transform.name;
        }
    }
}
