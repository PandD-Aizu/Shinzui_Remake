using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;

/// <summary>Port the existing dust graph through Shader Graph's serializer, retaining all authored nodes and VFX inputs</summary>
public static class HdrpDustGraphMigration
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>Resolve the installed internal editor API by exact type name</summary>
    /// <param name="name">Full installed type name</param>
    /// <returns>Resolved editor type</returns>
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

    /// <summary>Add an HDRP VFX-compatible unlit target without rebuilding the authored particle graph</summary>
    public static void Convert()
    {
        const string path = "Assets/Shinzui/ShaderGraph/DustEffect.shadergraph";
        var graphType = Type("UnityEditor.ShaderGraph.GraphData");
        var multiJson = Type("UnityEditor.ShaderGraph.Serialization.MultiJson");
        var graph = Activator.CreateInstance(graphType);
        multiJson.GetMethod("Deserialize").MakeGenericMethod(graphType).Invoke(null, new object[] { graph, File.ReadAllText(path), null, false });
        graphType.GetMethod("OnEnable", Flags).Invoke(graph, null);
        var targetType = Type("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDTarget");
        var target = Activator.CreateInstance(targetType);
        targetType.GetMethod("TrySetActiveSubTarget", Flags).Invoke(target, new object[] { Type("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDUnlitSubTarget") });
        targetType.GetField("m_SupportVFX", Flags).SetValue(target, true);
        var subTarget = targetType.GetProperty("activeSubTarget", Flags).GetValue(target);
        var data = subTarget.GetType().GetProperty("systemData", Flags).GetValue(subTarget);
        SetEnum(data, "surfaceType", "Transparent");
        SetEnum(data, "renderQueueType", "Transparent");
        SetEnum(data, "blendingMode", "Additive");
        SetEnum(data, "doubleSidedMode", "Enabled");
        data.GetType().GetProperty("alphaTest", Flags).SetValue(data, true);
        var method = graphType.GetMethods(Flags).Single(m => m.Name == "SetTargetActive" && m.GetParameters()[0].ParameterType.Name == "Target");
        method.Invoke(graph, new object[] { target, false });
        graphType.GetMethod("ValidateGraph", Flags).Invoke(graph, null);
        string serialized = (string)multiJson.GetMethod("Serialize").Invoke(null, new[] { graph });
        File.WriteAllText(path, serialized);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Shinzui/VFXGraph/DustVFX.vfx", ImportAssetOptions.ForceUpdate);
        UnityEngine.Debug.Log("HDRP_DUST_GRAPH_CONVERTED");
    }

    /// <summary>Set a version-checked enum property on HDRP's installed editor target data</summary>
    /// <param name="data">HDRP system data</param>
    /// <param name="property">Property name</param>
    /// <param name="value">Enum member</param>
    private static void SetEnum(object data, string property, string value)
    {
        var info = data.GetType().GetProperty(property, Flags);
        info.SetValue(data, Enum.Parse(info.PropertyType, value));
    }

    /// <summary>Replace the URP-only enemy particle output while transferring its blocks, inputs and flow links</summary>
    public static void ConvertEnemyOutput()
    {
        const string path = "Assets/Shinzui/VFXGraph/EnemyVFX.vfx";
        var resourceType = Type("UnityEditor.VFX.VisualEffectResource");
        var resource = resourceType.GetMethod("GetResourceAtPath").Invoke(null, new object[] { path });
        var graph = Member(resource, "graph");
        var children = ((System.Collections.IEnumerable)Member(graph, "children")).Cast<object>().ToArray();
        foreach (var source in children.Where(c => c.GetType().Name == "VFXURPLitPlanarPrimitiveOutput"))
        {
            var destination = UnityEngine.ScriptableObject.CreateInstance(Type("UnityEditor.VFX.HDRP.VFXLitPlanarPrimitiveOutput"));
            var method = destination.GetType().GetMethod("GetSettings", Flags);
            var settings = ((System.Collections.IEnumerable)method.Invoke(destination, new[] { (object)true, Enum.Parse(method.GetParameters()[1].ParameterType, "Default") })).Cast<object>();
            foreach (var setting in settings)
            {
                var field = (FieldInfo)Member(setting, "field");
                var sourceField = source.GetType().GetField(field.Name, Flags);
                if (sourceField == null) continue;
                object value = sourceField.GetValue(source);
                if (field.FieldType.IsEnum && value != null)
                {
                    if (!Enum.IsDefined(field.FieldType, value.ToString())) continue;
                    value = Enum.Parse(field.FieldType, value.ToString());
                }
                if (value == null || field.FieldType.IsInstanceOfType(value)) Call(destination, "SetSettingValue", field.Name, value);
            }
            var blocks = ((System.Collections.IEnumerable)Member(source, "children")).Cast<object>().ToArray();
            foreach (var block in blocks) Call(destination, "AddChild", block, -1, false);
            var oldSlots = ((System.Collections.IEnumerable)Member(source, "inputSlots")).Cast<object>().ToArray();
            foreach (var slot in ((System.Collections.IEnumerable)Member(destination, "inputSlots")).Cast<object>())
            {
                var old = oldSlots.FirstOrDefault(s => (string)Member(s, "name") == (string)Member(slot, "name"));
                if (old != null) Type("UnityEditor.VFX.VFXSlot").GetMethod("CopyLinksAndValue").Invoke(null, new[] { slot, old, (object)false });
            }
            var flow = ((Array)Member(source, "inputFlowSlot")).GetValue(0);
            foreach (var link in ((System.Collections.IEnumerable)Member(flow, "link")).Cast<object>().ToArray())
                Call(destination, "LinkFrom", Member(link, "context"), Member(link, "slotIndex"), 0);
            Call(source, "UnlinkAll");
            Type("UnityEditor.VFX.VFXModel").GetMethod("ReplaceModel").Invoke(null, new[] { (object)destination, source, true, true });
        }
        Type("UnityEditor.VFX.VisualEffectResourceExtensions").GetMethod("WriteAssetWithSubAssets").Invoke(null, new[] { resource });
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        UnityEngine.Debug.Log("HDRP_ENEMY_VFX_CONVERTED");
    }

    /// <summary>Read installed VFX model members, whose editor types are internal</summary>
    /// <param name="instance">Editor model</param><param name="name">Member name</param>
    /// <returns>Member value</returns>
    private static object Member(object instance, string name) => instance.GetType().GetProperties(Flags).FirstOrDefault(p => p.Name == name)?.GetValue(instance)
        ?? instance.GetType().GetField(name, Flags)?.GetValue(instance);

    /// <summary>Invoke a known installed VFX API overload by compatible argument types</summary>
    /// <param name="instance">Editor model</param><param name="name">Method name</param><param name="args">Arguments</param>
    private static void Call(object instance, string name, params object[] args)
    {
        var method = instance.GetType().GetMethods(Flags).First(m => m.Name == name && m.GetParameters().Length == args.Length
            && m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(v => v));
        method.Invoke(instance, args);
    }
}
