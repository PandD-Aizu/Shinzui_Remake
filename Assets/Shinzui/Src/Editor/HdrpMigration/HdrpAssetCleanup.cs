using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.Editor.HdrpMigration
{
    /// <summary>グラフの互換性と旧アセットの参照を検査する</summary>
    public static class HdrpAssetCleanup
    {
        /// <summary>現行パイプラインでVFXを再インポートし出力と依存グラフを報告する</summary>
        internal static void AuditGraphs()
        {
            int graphs = 0, outputs = 0, review = 0, shaders = 0;
            foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/Shinzui/") && p.EndsWith(".vfx")))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                graphs++;
                int graphOutputs = 0;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path).Where(a => a != null))
                {
                    string type = asset.GetType().FullName;
                    if (type.Contains("Output") && !type.Contains("SubOutput"))
                    {
                        outputs++;
                        graphOutputs++;
                        Debug.Log($"HDRP VFX output: {path} / {asset.name}, type={type}");
                        if (type.Contains("Universal") || type.Contains("URP"))
                        {
                            review++;
                            Debug.LogWarning($"HDRP VFX output requires replacement: {path} / {asset.name}");
                        }
                    }
                    if (asset is Shader shader)
                    {
                        shaders++;
                        if (ShaderUtil.ShaderHasError(shader))
                        {
                            review++;
                            Debug.LogError($"HDRP VFX generated shader errors: {path} / {shader.name}");
                        }
                    }
                }
                if (graphOutputs == 0)
                {
                    review++;
                    Debug.LogWarning($"HDRP VFX no output context found: {path}; check whether this graph is intentionally unfinished; to render particles connect Update Particle to Output Particle Quad (Unlit) and bind the intended flipbook texture");
                }
                foreach (string dependency in AssetDatabase.GetDependencies(path, true).Where(p => p.EndsWith(".shadergraph")))
                {
                    if (!File.Exists(dependency) || File.ReadAllText(dependency).Contains("HighDefinition.ShaderGraph.HDTarget")) continue;
                    review++;
                    Debug.LogWarning($"HDRP VFX graph target missing: {path} -> {dependency}; open Shader Graph, Graph Inspector > Graph Settings > Active Targets > HDRP, retain vertex/fragment connections, enable Support VFX");
                }
                Debug.Log($"HDRP VFX reimported: {path}; open graph and check Compile/Console and output material in HDRP scene");
            }
            const string dust = "Assets/Shinzui/ShaderGraph/DustEffect.shadergraph";
            if (File.Exists(dust) && !File.ReadAllText(dust).Contains("HighDefinition.ShaderGraph.HDTarget"))
            {
                review++;
                Debug.LogWarning($"HDRP Shader Graph MANUAL: {dust}; HDRP Lit target, Transparent, Additive, Alpha Clipping, Render Face Front, Support VFX; remove missing Universal target after checking all connections");
            }
            Debug.Log($"HDRP graphs: reimported={graphs}, output objects={outputs}, accessible generated shaders={shaders}, review={review}; generated VFX shader availability depends on importer, visual validation still required");
        }

        /// <summary>依存が残っていない旧パイプライン、RendererとTMP URPグラフを削除する</summary>
        [MenuItem("Shinzui/HDRP Migration/6 Remove URP Leftovers")]
        public static void RemoveLeftovers()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before cleanup");
            AssetDatabase.SaveAssets();
            string[] all = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p)).ToArray();
            var candidates = new HashSet<string>(all.Where(IsObsolete));
            var dependencies = new Dictionary<string, string[]>();
            var candidateGuids = candidates.ToDictionary(AssetDatabase.AssetPathToGUID, p => p);
            /* 全アセットに加えGraphicsとQualityの設定参照も検査する */
            foreach (string path in all.Concat(new[] { "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset" }))
            {
                var references = new HashSet<string>(AssetDatabase.GetDependencies(path, false).Where(p => p != path));
                /* 欠落スクリプトの参照が依存DBに現れない場合も保存GUIDで保護する */
                if (File.Exists(path) && new[] { ".asset", ".unity", ".prefab", ".mat", ".vfx", ".shadergraph", ".controller" }.Contains(Path.GetExtension(path)))
                    foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path), @"[a-f0-9]{32}"))
                        if (candidateGuids.TryGetValue(match.Value, out string referenced) && referenced != path) references.Add(referenced);
                dependencies[path] = references.ToArray();
            }
            var removable = new HashSet<string>(candidates);
            bool changed;
            do
            {
                changed = false;
                foreach (var pair in dependencies.Where(p => !removable.Contains(p.Key)))
                    foreach (string dependency in pair.Value)
                        if (removable.Remove(dependency)) changed = true;
            } while (changed);
            int deleted = 0;
            foreach (string path in candidates.OrderBy(p => p))
            {
                if (!removable.Contains(path))
                {
                    string[] owners = dependencies.Where(p => p.Value.Contains(path) && !removable.Contains(p.Key)).Select(p => p.Key).ToArray();
                    Debug.LogWarning($"HDRP cleanup RETAINED: {path}, referenced by: {string.Join(", ", owners)}");
                    continue;
                }
                if (AssetDatabase.DeleteAsset(path)) { deleted++; Debug.Log($"HDRP cleanup deleted: {path}"); }
                else Debug.LogWarning($"HDRP cleanup delete failed: {path}");
            }
            Debug.Log($"HDRP cleanup: assets checked={dependencies.Count}, candidates={candidates.Count}, deleted={deleted}, retained/failed={candidates.Count - deleted}; converted VolumeProfiles retained with original GUIDs");
        }

        /// <summary>既知の旧描画アセットだけを削除候補とする</summary>
        /// <param name="path">アセットパス</param>
        /// <returns>削除候補か</returns>
        private static bool IsObsolete(string path)
        {
            if (path.StartsWith("Assets/TextMesh Pro/Shaders/TMP_SDF-URP ") && path.EndsWith(".shadergraph")) return true;
            if (!path.EndsWith(".asset") || !File.Exists(path)) return false;
            if (!path.StartsWith("Assets/Shinzui/GraphicSettings/") && !path.StartsWith("Assets/Settings/")) return false;
            if (AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path) != null) return false;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null)
            {
                /* 移行済みの成分を持つプロファイルは旧名でも削除しない */
                if (profile.components.Any(c => c != null &&
                    (c.GetType().Namespace?.Contains("HighDefinition") == true || c.GetType().Namespace?.StartsWith("Shinzui") == true))) return false;
                return File.ReadAllText(path).Contains("Universal") || Path.GetFileName(path) == "DefaultVolumeProfile.asset";
            }
            string name = Path.GetFileNameWithoutExtension(path);
            return name == "QualityHigh" || name == "QualityMidium" || name == "QualityLow" ||
                name == "PC_RPAsset" || name == "Mobile_RPAsset" || name == "UniversalRenderPipelineGlobalSettings" ||
                new[] { "QualityHigh_Renderer", "QualityMidium_Renderer", "QualityLow_Renderer", "PC_Renderer", "Mobile_Renderer" }.Contains(name);
        }
    }
}
