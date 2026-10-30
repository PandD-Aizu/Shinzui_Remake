using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Shinzui.Editor.HdrpMigration
{
    /// <summary>Shader Graph自身のシリアライザーでHDRPターゲットを追加する</summary>
    internal static class HdrpGraphMigration
    {
        /// <summary>DustEffectのノードと接続を保持したままHDRP Litへ移行する</summary>
        internal static void MigrateDust()
        {
            const string path = "Assets/Shinzui/ShaderGraph/DustEffect.shadergraph";
            string source = File.ReadAllText(path);
            if (source.Contains("HighDefinition.ShaderGraph.HDTarget"))
            {
                Debug.Log($"HDRP Dust graph: converted=0, already HDRP=1: {path}");
                return;
            }
            /* HDRP 17.5の内部型だけをここへ隔離し保存はShader Graph標準APIへ任せる */
            Assembly sg = Assembly.Load("Unity.ShaderGraph.Editor");
            Assembly hd = Assembly.Load("Unity.RenderPipelines.HighDefinition.Editor");
            Type graphType = sg.GetType("UnityEditor.ShaderGraph.GraphData", true);
            Type targetType = sg.GetType("UnityEditor.ShaderGraph.Target", true);
            Type nodeType = sg.GetType("UnityEditor.ShaderGraph.AbstractMaterialNode", true);
            Type multiJson = sg.GetType("UnityEditor.ShaderGraph.Serialization.MultiJson", true);
            Type hdTargetType = hd.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDTarget", true);
            object graph = Activator.CreateInstance(graphType, true);
            graphType.GetProperty("assetGuid").SetValue(graph, AssetDatabase.AssetPathToGUID(path));
            multiJson.GetMethod("Deserialize").MakeGenericMethod(graphType).Invoke(null, new[] { graph, source, null, (object)false });
            graphType.GetMethod("OnEnable").Invoke(graph, null);
            try
            {
                MethodInfo getNodes = graphType.GetMethod("GetNodes").MakeGenericMethod(nodeType);
                object[] nodes = ((IEnumerable)getNodes.Invoke(graph, null)).Cast<object>().ToArray();
                object[] edges = ((IEnumerable)graphType.GetProperty("edges").GetValue(graph)).Cast<object>().ToArray();
                object[] targets = ((IEnumerable)graphType.GetProperty("activeTargets").GetValue(graph)).Cast<object>().ToArray();
                object target = Activator.CreateInstance(hdTargetType, true);
                Type litType = hd.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDLitSubTarget", true);
                if (!(bool)hdTargetType.GetMethod("TrySetActiveSubTarget").Invoke(target, new object[] { litType }))
                    throw new InvalidOperationException("HDRP Lit Shader Graph subtarget unavailable");
                hdTargetType.GetField("m_SupportVFX", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, true);
                object subTarget = hdTargetType.GetProperty("activeSubTarget").GetValue(target);
                object system = subTarget.GetType().GetProperty("systemData").GetValue(subTarget);
                SetEnum(system, "surfaceType", "Transparent");
                SetEnum(system, "renderQueueType", "Transparent");
                SetEnum(system, "blendingMode", "Additive");
                SetEnum(system, "doubleSidedMode", "Disabled");
                system.GetType().GetProperty("alphaTest").SetValue(system, true);

                /* 旧ターゲットを外してもノードとエッジが欠落した場合は保存しない */
                foreach (object old in targets)
                    graphType.GetMethod("SetTargetInactive").Invoke(graph, new[] { old, (object)true });
                graphType.GetMethod("SetTargetActive", new[] { targetType, typeof(bool) }).Invoke(graph, new[] { target, (object)false });
                graphType.GetMethod("ValidateGraph").Invoke(graph, null);
                object[] afterNodes = ((IEnumerable)getNodes.Invoke(graph, null)).Cast<object>().ToArray();
                object[] afterEdges = ((IEnumerable)graphType.GetProperty("edges").GetValue(graph)).Cast<object>().ToArray();
                if (nodes.Any(n => !afterNodes.Contains(n)) || edges.Any(e => !afterEdges.Contains(e)))
                {
                    Debug.LogWarning($"HDRP Dust graph skipped=1: {path}, node/edge removal detected, original file unchanged; use Graph Inspector > Graph Settings > HDRP Lit, Transparent/Additive, Alpha Clipping, Front, Support VFX, reconnect original blocks before saving");
                    return;
                }
                object result = sg.GetType("UnityEditor.ShaderGraph.FileUtilities", true)
                    .GetMethod("WriteShaderGraphToDisk").Invoke(null, new[] { path, graph });
                if (result == null) throw new IOException($"Shader Graph save failed: {path}");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"HDRP Dust graph converted=1: {path}, nodes preserved={nodes.Length}, edges preserved={edges.Length}, HDRP Lit / Transparent / Additive / Alpha Clipping / Front / Support VFX");
            }
            finally
            {
                graphType.GetMethod("OnDisable").Invoke(graph, null);
            }
        }

        /// <summary>内部設定の列挙値を型に合わせて設定する</summary>
        /// <param name="target">設定データ</param>
        /// <param name="name">プロパティ名</param>
        /// <param name="value">列挙名</param>
        private static void SetEnum(object target, string name, string value)
        {
            PropertyInfo property = target.GetType().GetProperty(name);
            property.SetValue(target, Enum.Parse(property.PropertyType, value));
        }
    }
}
