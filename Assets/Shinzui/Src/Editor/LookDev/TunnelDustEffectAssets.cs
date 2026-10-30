using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace Shinzui.Editor.LookDev
{
    /// <summary>塵の板面を消す設定をShader GraphとVFXの標準保存処理で反映する</summary>
    internal static class TunnelDustEffectAssets
    {
        private const string GraphPath = "Assets/Shinzui/ShaderGraph/DustEffect.shadergraph";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>塵を非ライティング加算へ変更し依存するVFXを再インポートする</summary>
        [MenuItem("Shinzui/LookDev/5 Fix Dust Effect")]
        public static void FixDustEffect()
        {
            TunnelLookMaterials.RequireEditMode();
            /* グラフ更新前の依存関係も取得して再インポート対象を固定する */
            string[] effects = AssetDatabase.FindAssets("t:VisualEffectAsset", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => AssetDatabase.GetDependencies(path, true).Contains(GraphPath))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            ConfigureGraph();
            int outputs = 0, overrides = 0;
            foreach (string path in effects)
                outputs += RefreshEffect(path, ref overrides);
            const string itemPath = "Assets/Shinzui/VFXGraph/ItemAcquisitionVFX.vfx";
            if (!effects.Contains(itemPath)) Debug.Log($"Dust VFX skipped (does not reference DustEffect): {itemPath}");
            AssetDatabase.SaveAssets();
            Debug.Log($"Dust fix summary: shader graphs=1, dependent VFX reimported={effects.Length}, dust outputs={outputs}, conflicting material overrides removed={overrides}, skipped unrelated item effect={(effects.Contains(itemPath) ? 0 : 1)}; HDRP Unlit/Additive, fog=off, shadow matte=off, alpha clip=0.001; verify soft motes under tunnel lights and in fog");
        }

        /// <summary>既存ノードと接続を保持してHDRP Unlitへ切り替える</summary>
        private static void ConfigureGraph()
        {
            /* 内部APIは導入済みHDRP 17.5の型と照合し独自JSON編集を行わない */
            Assembly sg = Assembly.Load("Unity.ShaderGraph.Editor");
            Assembly hd = Assembly.Load("Unity.RenderPipelines.HighDefinition.Editor");
            Type graphType = sg.GetType("UnityEditor.ShaderGraph.GraphData", true);
            Type nodeType = sg.GetType("UnityEditor.ShaderGraph.AbstractMaterialNode", true);
            Type targetType = sg.GetType("UnityEditor.ShaderGraph.Target", true);
            Type hdType = hd.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDTarget", true);
            object graph = Activator.CreateInstance(graphType, true);
            Set(graph, "assetGuid", AssetDatabase.AssetPathToGUID(GraphPath));
            sg.GetType("UnityEditor.ShaderGraph.Serialization.MultiJson", true).GetMethod("Deserialize")
                .MakeGenericMethod(graphType).Invoke(null, new[] { graph, File.ReadAllText(GraphPath), null, (object)false });
            graphType.GetMethod("OnEnable").Invoke(graph, null);
            try
            {
                MethodInfo getNodes = graphType.GetMethod("GetNodes").MakeGenericMethod(nodeType);
                object[] nodes = ((IEnumerable)getNodes.Invoke(graph, null)).Cast<object>().ToArray();
                object[] edges = ((IEnumerable)Get(graph, "edges")).Cast<object>().ToArray();
                object[] targets = ((IEnumerable)Get(graph, "activeTargets")).Cast<object>().ToArray();
                object target = targets.Single(t => t.GetType() == hdType);
                Type unlitType = hd.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDUnlitSubTarget", true);
                if (!(bool)hdType.GetMethod("TrySetActiveSubTarget").Invoke(target, new object[] { unlitType }))
                    throw new InvalidOperationException("HDRP Unlit Shader Graph subtarget unavailable");
                hdType.GetField("m_SupportVFX", PrivateInstance).SetValue(target, true);
                object subTarget = Get(target, "activeSubTarget");
                object system = Get(subTarget, "systemData");
                SetEnum(system, "surfaceType", "Transparent");
                SetEnum(system, "renderQueueType", "Transparent");
                SetEnum(system, "blendingMode", "Additive");
                Set(system, "alphaTest", true);
                Set(system, "transparentZWrite", false);
                Set(system, "materialNeedsUpdateHash", -1);
                object builtin = Get(subTarget, "builtinData");
                Set(builtin, "transparencyFog", false);
                Set(builtin, "distortion", false);
                Set(builtin, "transparentDepthPrepass", false);
                Set(builtin, "transparentDepthPostpass", false);
                Set(Get(subTarget, "unlitData"), "enableShadowMatte", false);

                /* ゼロのアルファも通過していた閾値だけを直し既存の柔らかな形状と距離フェードを保持する */
                object clip = nodes.Single(n => (string)Get(n, "name") == "SurfaceDescription.AlphaClipThreshold");
                object alpha = nodes.Single(n => (string)Get(n, "name") == "SurfaceDescription.Alpha");
                object alphaSlot = nodeType.GetMethod("GetSlotReference").Invoke(alpha, new object[] { 0 });
                object clipSlot = nodeType.GetMethod("GetSlotReference").Invoke(clip, new object[] { 0 });
                if (!edges.Any(e => Get(e, "inputSlot").Equals(alphaSlot)))
                    throw new InvalidOperationException("DustEffect alpha input is disconnected; original graph unchanged");
                if (edges.Any(e => Get(e, "inputSlot").Equals(clipSlot)))
                    throw new InvalidOperationException("DustEffect clip threshold is driven by a node; original graph unchanged");
                Type floatSlot = sg.GetType("UnityEditor.ShaderGraph.Vector1MaterialSlot", true);
                object slot = nodeType.GetMethod("FindSlot").MakeGenericMethod(floatSlot).Invoke(clip, new object[] { 0 });
                Set(slot, "value", 0.001f);
                foreach (object other in targets.Where(t => t != target))
                    graphType.GetMethod("SetTargetInactive").Invoke(graph, new[] { other, (object)true });
                graphType.GetMethod("SetTargetActive", new[] { targetType, typeof(bool) }).Invoke(graph, new[] { target, (object)false });
                graphType.GetMethod("ValidateGraph").Invoke(graph, null);
                object[] afterNodes = ((IEnumerable)getNodes.Invoke(graph, null)).Cast<object>().ToArray();
                object[] afterEdges = ((IEnumerable)Get(graph, "edges")).Cast<object>().ToArray();
                if (nodes.Any(n => !afterNodes.Contains(n)) || edges.Any(e => !afterEdges.Contains(e)))
                    throw new InvalidOperationException("DustEffect node/edge removal detected; original graph unchanged");
                object result = sg.GetType("UnityEditor.ShaderGraph.FileUtilities", true)
                    .GetMethod("WriteShaderGraphToDisk").Invoke(null, new[] { GraphPath, graph });
                if (result == null) throw new IOException($"Shader Graph save failed: {GraphPath}");
                AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"Dust graph saved: {GraphPath}, nodes preserved={nodes.Length}, edges preserved={edges.Length}, active target=HDRP Unlit, alpha connection preserved, clip=0.001; no Lit specular/decal/SSR contribution and no additional RGB premultiplication");
            }
            finally
            {
                graphType.GetMethod("OnDisable").Invoke(graph, null);
            }
        }

        /// <summary>出力の材質上書きを整理しVFX自身の保存処理で依存シェーダーを再構築する</summary>
        /// <param name="path">VFXアセットのパス</param>
        /// <param name="removed">取り除いた上書き数</param>
        /// <returns>塵のShader Graphを使用する出力数</returns>
        private static int RefreshEffect(string path, ref int removed)
        {
            Assembly vfx = Assembly.Load("Unity.VisualEffectGraph.Editor");
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            object resource = vfx.GetType("UnityEditor.VFX.VisualEffectObjectExtensions", true)
                .GetMethod("GetResource").Invoke(null, new object[] { asset });
            Type extensions = vfx.GetType("UnityEditor.VFX.VisualEffectResourceExtensions", true);
            object graph = extensions.GetMethod("GetOrCreateGraph").Invoke(null, new[] { resource });
            Type model = vfx.GetType("UnityEditor.VFX.VFXModel", true);
            MethodInfo getSetting = model.GetMethod("GetSettingValue");
            int outputs = 0;
            foreach (object output in ((IEnumerable)Get(graph, "children")).Cast<object>().ToArray())
            {
                if (output.GetType().Name != "VFXComposedParticleOutput") continue;
                var shader = getSetting.Invoke(output, new object[] { "shaderGraph" }) as UnityEngine.Object;
                if (AssetDatabase.GetAssetPath(shader) != GraphPath) continue;
                outputs++;
                /* 合成出力は旧blendModeとuseAlphaClippingではなくShader Graphの材質設定を参照する */
                object settings = getSetting.Invoke(output, new object[] { "materialSettings" });
                object copy = ((ICloneable)settings).Clone();
                var properties = (IDictionary)copy.GetType().GetField("m_PropertyMap", PrivateInstance).GetValue(copy);
                string[] controlled = { "_SurfaceType", "_BlendMode", "_AlphaCutoffEnable", "_AlphaCutoff",
                    "_EnableFogOnTransparent", "_EnableBlendModePreserveSpecularLighting", "_EnableShadowMatte",
                    "_SupportDecals", "_ReceivesSSR", "_ReceivesSSRTransparent", "_ZWrite", "_TransparentZWrite" };
                int changed = 0;
                foreach (string key in controlled)
                {
                    if (!properties.Contains(key)) continue;
                    properties.Remove(key);
                    changed++;
                }
                if (changed > 0)
                {
                    model.GetMethod("SetSettingValue", new[] { typeof(string), typeof(object) })
                        .Invoke(output, new[] { (object)"materialSettings", copy });
                    EditorUtility.SetDirty((UnityEngine.Object)output);
                }
                removed += changed;
                Debug.Log($"Dust VFX output: {path}, {output.GetType().Name}, authoritative settings=Shader Graph material, removed overrides={changed}");
            }
            graph.GetType().GetMethod("SetExpressionGraphDirty").Invoke(graph, new object[] { true });
            EditorUtility.SetDirty((UnityEngine.Object)graph);
            extensions.GetMethod("WriteAssetWithSubAssets").Invoke(null, new[] { resource });
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"Dust VFX reimported: {path}, matching outputs={outputs}");
            return outputs;
        }

        /// <summary>内部型の公開プロパティを取得しAPI差分を明示する</summary>
        /// <param name="target">設定データ</param>
        /// <param name="name">プロパティ名</param>
        /// <returns>プロパティ値</returns>
        private static object Get(object target, string name)
        {
            return Property(target, name).GetValue(target);
        }

        /// <summary>内部型の公開プロパティを設定する</summary>
        /// <param name="target">設定データ</param>
        /// <param name="name">プロパティ名</param>
        /// <param name="value">設定値</param>
        private static void Set(object target, string name, object value)
        {
            Property(target, name).SetValue(target, value);
        }

        /// <summary>内部型の列挙値を名前で設定する</summary>
        /// <param name="target">設定データ</param>
        /// <param name="name">プロパティ名</param>
        /// <param name="value">列挙名</param>
        private static void SetEnum(object target, string name, string value)
        {
            PropertyInfo property = Property(target, name);
            property.SetValue(target, Enum.Parse(property.PropertyType, value));
        }

        /// <summary>導入済みパッケージに必要なAPIが存在することを確認する</summary>
        /// <param name="target">確認対象</param>
        /// <param name="name">プロパティ名</param>
        /// <returns>公開プロパティ</returns>
        private static PropertyInfo Property(object target, string name)
        {
            return target.GetType().GetProperty(name) ?? throw new MissingMemberException(target.GetType().FullName, name);
        }
    }
}
