using System.Collections.Generic;
using System.IO;
using System.Text;
using FMODUnity;
using UnityEditor;
using UnityEngine;

namespace Shinzui.Editor
{
    public class FMODEventCodeGenerator : EditorWindow
    {
        private const string OUTPUT_SCRIPT_PATH = "Assets/Shinzui/Src/Domain/ValueObjects/FMOD/FMODEventPaths.cs";
        private const string OUTPUT_ASSET_PATH = "Assets/Shinzui/SO/FMODEventTable.asset";
        private const string NAMESPACE = "Shinzui.Domain.ValueObjects.FMOD";

        [MenuItem("Tools/FMOD/Generate AudioEventTable")]
        private static void Generate()
        {
            // FMODのイベントパスをすべて取得
            var eventPaths = GetAllFMODEventPaths();
            if (eventPaths.Count == 0)
            {
                Debug.LogError("[FMODEventCodeGenerator] No FMOD event paths found.");
                return;
            }

            GenerateScript(eventPaths);
            AssetDatabase.Refresh();

            EditorApplication.delayCall += GenerateAssetIfNotExists;
            Debug.Log($"[FMODEventCodeGenerator] Generated {eventPaths.Count} events.");
        }

        private static List<string> GetAllFMODEventPaths()
        {
            var paths = new List<string>();
            
            foreach (var e in EventManager.Events)
            {
                paths.Add(e.Path);
            }
            return paths;
        }

        private static void GenerateScript(List<string> eventPaths)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// THIS FILE IS AUTO-GENERATED. DO NOT EDIT MANUALLY.");
            sb.AppendLine($"// Generated at: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("using FMODUnity;");
            sb.AppendLine();
            sb.AppendLine($"namespace {NAMESPACE}");
            sb.AppendLine("{");
            sb.AppendLine("    public readonly struct FMODEventPath");
            sb.AppendLine("    {");
            sb.AppendLine("        public EventReference Reference { get; }");
            sb.AppendLine("        private FMODEventPath(string path) => Reference = RuntimeManager.PathToEventReference(path);");
            sb.AppendLine();

            foreach (var path in eventPaths)
            {
                var fieldName = PathToFieldName(path);
                sb.AppendLine($"        public static readonly FMODEventPath {fieldName} = new (\"{path}\");");
            }
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            // 出力ディレクトリがなければ作成する
            var dir = Path.GetDirectoryName(OUTPUT_SCRIPT_PATH);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir!);
            }
            
            File.WriteAllText(OUTPUT_SCRIPT_PATH, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[FMODEventCodeGenerator] Generated script: {OUTPUT_SCRIPT_PATH}");
        }

        private static void GenerateAssetIfNotExists()
        {
            if (File.Exists(OUTPUT_ASSET_PATH))
            {
                Debug.LogError("[FMODEventCodeGenerator] Asset already exists: " + OUTPUT_ASSET_PATH);
                return;
            }

            var asset = ScriptableObject.CreateInstance("AudioEventTable");
            if (asset is null)
            {
                Debug.LogError("[FMODEventCodeGenerator] Failed to create AudioEventTable asset.");
                return;
            }

            var dir = Path.GetDirectoryName(OUTPUT_ASSET_PATH);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir!);
            }
            
            AssetDatabase.CreateAsset(asset, OUTPUT_ASSET_PATH);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FMODEventCodeGenerator] Generated asset: {OUTPUT_ASSET_PATH}");
        }

        private static string PathToFieldName(string eventPath)
        {
            bool isSnapshot = eventPath.StartsWith("snapshot:/");
            var withoutPrefix = isSnapshot 
                ? eventPath.Replace("snapshot:/", "") 
                : eventPath.Replace("event:/", "");
            
            var snakeCase = System.Text.RegularExpressions.Regex.Replace(
                withoutPrefix,
                @"(?<=[a-z0-9])(?=[A-Z])",
                "_"
            );

            var fieldName = snakeCase
                .Replace("/", "_")
                .Replace(" ", "_")
                .Replace("-", "_")
                .ToUpper();

            return isSnapshot ? $"SNAPSHOT_{fieldName}" : fieldName;
        }
    }
}