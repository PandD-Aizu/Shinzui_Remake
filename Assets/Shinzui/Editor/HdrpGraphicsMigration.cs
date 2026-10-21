using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

/// <summary>Explicit, local asset migration after portal and generated-gameplay validation</summary>
public static class HdrpGraphicsMigration
{
    public const string Root = "Assets/Shinzui/Graphics/HDRP";
    private static readonly string[] Scenes = { "Assets/Shinzui/Scenes/Title.unity", "Assets/Shinzui/Scenes/StageTemp.unity" };

    /// <summary>Create feature-complete HDRP assets; runtime preferences control the per-camera cost</summary>
    public static void CreateProductionAssets()
    {
        Directory.CreateDirectory(Root);
        AssetDatabase.Refresh();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            string qualityName = QualitySettings.names[i].ToLowerInvariant();
            string assetName = qualityName == "low" ? "Low" : qualityName == "high" ? "High" :
                qualityName == "medium" || qualityName == "midium" ? "Medium" : "Ultra";
            string path = Root + "/Quality" + assetName + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            var settings = asset.currentPlatformRenderPipelineSettings;
            settings.supportRayTracing = true;
            settings.supportedRayTracingMode = RenderPipelineSettings.SupportedRayTracingMode.Both;
            settings.supportSSR = true;
            settings.supportSSGI = true;
            settings.supportSSAO = true;
            settings.supportVolumetrics = true;
            settings.supportCustomPass = true;
            settings.supportMotionVectors = true;
            asset.currentPlatformRenderPipelineSettings = settings;
            EditorUtility.SetDirty(asset);
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = asset;
        }
        GraphicsSettings.defaultRenderPipeline = QualitySettings.renderPipeline;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Direct3D11 });
        PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetDynamicBatchingForPlatform(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.enableFrameTimingStats = true;
        PlayerSettings.allowHDRDisplaySupport = true;
        PlayerSettings.useHDRDisplay = false;
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root + "/TunnelAtmosphere.asset");
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, Root + "/TunnelAtmosphere.asset");
            var exposure = profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(8);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var fog = profile.Add<Fog>(true);
            fog.enabled.Override(true);
            fog.enableVolumetricFog.Override(true);
            fog.meanFreePath.Override(120);
            fog.albedo.Override(new Color(.3f, .34f, .37f));
            fog.depthExtent.Override(64);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(.15f);
            var grading = profile.Add<ColorAdjustments>(true);
            grading.saturation.Override(-12);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(.22f);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
        }
        PreserveRuntimeShaders();
        AssetDatabase.SaveAssets();
        Debug.Log("HDRP_PRODUCTION_ASSETS_READY");
    }

    /// <summary>Migrate production prefab and scene light units once, preserving generated layout and gameplay components</summary>
    public static void ConvertPrefabsAndScenes()
    {
        var paths = AssetDatabase.GetDependencies(Scenes, true)
            .Concat(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Shinzui" }).Select(AssetDatabase.GUIDToAssetPath))
            .Where(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) && !p.Contains("/Graphics/HDRP/"))
            .Distinct().OrderBy(p => AssetDatabase.GetDependencies(p, true).Length).ToArray();
        foreach (string path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (ConvertHierarchy(root)) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects()) ConvertHierarchy(root);
            var volume = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "HDRP Tunnel Atmosphere");
            if (volume == null) volume = new GameObject("HDRP Tunnel Atmosphere");
            var component = volume.GetComponent<Volume>() ?? volume.AddComponent<Volume>();
            component.isGlobal = true;
            component.priority = 10;
            component.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root + "/TunnelAtmosphere.asset");
            RenderSettings.fog = false;
            EditorSceneManager.SaveScene(scene);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scenes[0], true) };
        AssetDatabase.SaveAssets();
        Debug.Log("HDRP_PRODUCTION_SCENES_CONVERTED");
    }

    /// <summary>Convert physical lighting and camera components without replacing gameplay behaviours</summary>
    /// <param name="root">Prefab or scene hierarchy root</param>
    /// <returns>Whether a serialized component changed</returns>
    private static bool ConvertHierarchy(GameObject root)
    {
        bool changed = false;
        foreach (var light in root.GetComponentsInChildren<Light>(true))
        {
            if (light.GetComponent<HDAdditionalLightData>() != null) continue;
            float intensity = light.intensity;
            var extra = light.gameObject.AddComponent<HDAdditionalLightData>();
            light.intensity = intensity * (light.type == LightType.Directional ? 10 : 100);
            extra.volumetricDimmer = .35f;
            if (light.TryGetComponent<Shinzui.Infrastructure.Lighting.PhysicalLight>(out var physical)) physical.SyncFromLight();
            changed = true;
        }
        foreach (var camera in root.GetComponentsInChildren<Camera>(true))
        {
            if (camera.GetComponent<HDAdditionalCameraData>() != null) continue;
            var extra = camera.gameObject.AddComponent<HDAdditionalCameraData>();
            extra.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            extra.backgroundColorHDR = Color.black;
            extra.volumeLayerMask = ~0;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            changed = true;
        }
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null || !AssetDatabase.GetAssetPath(source).StartsWith("Packages/")) continue;
                string folder = Root + "/PackageMaterials";
                Directory.CreateDirectory(folder);
                string path = folder + "/" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) + ".mat";
                var owned = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (owned == null)
                {
                    owned = new Material(source);
                    AssetDatabase.CreateAsset(owned, path);
                    if (source.shader.name.StartsWith("Universal Render Pipeline/") || source.shader.name.StartsWith("Standard")) ConvertMaterial(owned);
                }
                materials[i] = owned;
                changed = true;
            }
            renderer.sharedMaterials = materials;
            if (renderer.sharedMaterials.Any(m => m != null && m.shader != null && m.shader.name.StartsWith("Shinzui/BlackHole")))
            {
                renderer.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>Collect production scene dependencies and authored game materials</summary>
    /// <returns>Unique material paths in the authorized production content</returns>
    private static IEnumerable<string> MaterialPaths() => AssetDatabase.GetDependencies(Scenes, true)
        .Concat(AssetDatabase.FindAssets("t:Material", new[] { "Assets/Shinzui" }).Select(AssetDatabase.GUIDToAssetPath))
        .Where(p => p.StartsWith("Assets/") && p.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)).Distinct();

    /// <summary>Port ordinary PBR materials with explicit texture-channel conversion</summary>
    public static void ConvertMaterials()
    {
        var converted = new List<string>();
        foreach (string path in MaterialPaths())
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null) continue;
            string shader = material.shader.name;
            if (!shader.StartsWith("Universal Render Pipeline/") && !shader.StartsWith("Standard")) continue;
            ConvertMaterial(material);
            EditorUtility.SetDirty(material);
            converted.Add(path);
        }
        // The upgraded concrete is the production tunnel material at every quality; mip limits control cost.
        var ultra = Resources.Load<Shinzui.View.Rendering.UltraEnvironmentProfile>("UltraEnvironmentProfile");
        if (ultra != null && ultra.sourceMaterial != null && ultra.ultraMaterial != null && ultra.ultraMaterial.shader.name == "HDRP/Lit")
        {
            ultra.sourceMaterial.shader = ultra.ultraMaterial.shader;
            ultra.sourceMaterial.CopyPropertiesFromMaterial(ultra.ultraMaterial);
            ultra.sourceMaterial.SetFloat("_NormalScale", .6f);
            ultra.sourceMaterial.SetFloat("_SmoothnessRemapMax", .62f);
            EditorUtility.SetDirty(ultra.sourceMaterial);
        }
        Directory.CreateDirectory("Artifacts/GraphicsValidation");
        File.WriteAllLines("Artifacts/GraphicsValidation/converted-materials.txt", converted);
        AssetDatabase.SaveAssets();
        Debug.Log("HDRP_MATERIALS_CONVERTED " + converted.Count);
    }

    /// <summary>Preserve base, normal, alpha, emission and specular workflow while packing the HDRP mask</summary>
    /// <param name="material">Authored material to migrate once</param>
    private static void ConvertMaterial(Material material)
    {
        bool unlit = material.shader.name.Contains("Unlit");
        string baseName = material.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
        Texture baseMap = material.HasProperty(baseName) ? material.GetTexture(baseName) : null;
        var scale = material.HasProperty(baseName) ? material.GetTextureScale(baseName) : Vector2.one;
        var offset = material.HasProperty(baseName) ? material.GetTextureOffset(baseName) : Vector2.zero;
        Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
        Texture normal = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
        float normalScale = material.HasProperty("_BumpScale") ? material.GetFloat("_BumpScale") : 1;
        float smoothness = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : .35f;
        float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0;
        Texture metalMap = material.HasProperty("_MetallicGlossMap") ? material.GetTexture("_MetallicGlossMap") : null;
        Texture occlusion = material.HasProperty("_OcclusionMap") ? material.GetTexture("_OcclusionMap") : null;
        Texture specular = material.HasProperty("_SpecGlossMap") ? material.GetTexture("_SpecGlossMap") : null;
        bool specularWorkflow = material.IsKeywordEnabled("_SPECULAR_SETUP") || material.shader.name.Contains("Specular");
        Color specularColor = material.HasProperty("_SpecColor") ? material.GetColor("_SpecColor") : new Color(.04f, .04f, .04f);
        Texture emissionMap = material.HasProperty("_EmissionMap") ? material.GetTexture("_EmissionMap") : null;
        Color emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
        bool transparent = material.renderQueue >= 2501 || (material.HasProperty("_Surface") && material.GetFloat("_Surface") > .5f);
        bool alphaClip = material.IsKeywordEnabled("_ALPHATEST_ON") || (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > .5f);
        float cutoff = material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : .5f;
        bool doubleSided = material.HasProperty("_Cull") && material.GetFloat("_Cull") == 0;
        Texture glossMap = specularWorkflow ? specular : metalMap;
        if (material.HasProperty("_SmoothnessTextureChannel") && material.GetFloat("_SmoothnessTextureChannel") > .5f) glossMap = baseMap;
        Texture mask = !unlit && (metalMap != null || occlusion != null || glossMap != null) ? PackMask(material, metalMap, occlusion, glossMap, metallic, smoothness) : null;
        material.shader = Shader.Find(unlit ? "HDRP/Unlit" : "HDRP/Lit");
        material.shaderKeywords = Array.Empty<string>();
        material.SetTexture(unlit ? "_UnlitColorMap" : "_BaseColorMap", baseMap);
        material.SetTextureScale(unlit ? "_UnlitColorMap" : "_BaseColorMap", scale);
        material.SetTextureOffset(unlit ? "_UnlitColorMap" : "_BaseColorMap", offset);
        material.SetColor(unlit ? "_UnlitColor" : "_BaseColor", color);
        if (!unlit)
        {
            material.SetTexture("_NormalMap", normal);
            material.SetFloat("_NormalScale", normalScale);
            material.SetTexture("_MaskMap", mask);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_SmoothnessRemapMin", 0);
            material.SetFloat("_SmoothnessRemapMax", 1);
            if (specularWorkflow)
            {
                material.SetFloat("_MaterialID", 4);
                material.SetTexture("_SpecularColorMap", specular);
                material.SetColor("_SpecularColor", specularColor);
            }
        }
        material.SetTexture("_EmissiveColorMap", emissionMap);
        material.SetColor("_EmissiveColor", emission * 30);
        material.SetFloat("_SurfaceType", transparent ? 1 : 0);
        material.SetFloat("_BlendMode", 0);
        material.SetFloat("_AlphaCutoffEnable", alphaClip ? 1 : 0);
        material.SetFloat("_AlphaCutoff", cutoff);
        material.SetFloat("_DoubleSidedEnable", doubleSided ? 1 : 0);
        material.renderQueue = transparent ? 3000 : alphaClip ? 2450 : 2000;
        HDShaderUtils.ResetMaterialKeywords(material);
    }

    /// <summary>Repack URP channels as HDRP metallic, occlusion, detail, smoothness without source texture changes</summary>
    /// <param name="owner">Material receiving the derived map</param>
    /// <param name="metal">Metallic red channel source</param>
    /// <param name="ao">Occlusion green channel source</param>
    /// <param name="gloss">Smoothness alpha channel source</param>
    /// <param name="metallic">Scalar metallic fallback</param>
    /// <param name="smoothness">Authored smoothness scale</param>
    /// <returns>Imported linear HDRP mask</returns>
    private static Texture PackMask(Material owner, Texture metal, Texture ao, Texture gloss, float metallic, float smoothness)
    {
        string folder = Root + "/Masks";
        Directory.CreateDirectory(folder);
        int size = Math.Min(2048, Math.Max(metal != null ? metal.width : 1, Math.Max(ao != null ? ao.width : 1, gloss != null ? gloss.width : 1)));
        var pack = new Material(Shader.Find("Hidden/Shinzui/HdrpMaskPack"));
        pack.SetTexture("_MetallicSource", metal != null ? metal : Texture2D.whiteTexture);
        pack.SetTexture("_OcclusionSource", ao != null ? ao : Texture2D.whiteTexture);
        pack.SetTexture("_GlossSource", gloss != null ? gloss : Texture2D.whiteTexture);
        pack.SetFloat("_MetallicValue", metallic);
        pack.SetFloat("_SmoothnessValue", smoothness);
        pack.SetFloat("_HasMetallic", metal != null ? 1 : 0);
        var target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(Texture2D.whiteTexture, target, pack);
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;
        string path = folder + "/" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(owner)) + "-mask.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(pack);
        RenderTexture.ReleaseTemporary(target);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture = false;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>Retain shaders found dynamically at runtime in Windows builds</summary>
    private static void PreserveRuntimeShaders()
    {
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var list = settings.FindProperty("m_AlwaysIncludedShaders");
        foreach (string name in new[] { "Custom/PortalProjection", "Custom/PortalMask", "Shinzui/BlackHoleBody", "Shinzui/BlackHoleEye", "Shinzui/BlackHoleLens", "Shinzui/SpiderWeb", "Hidden/Shinzui/HdrpHorrorDetectionNoise", "Hidden/Shinzui/HdrpGameOverDissolve", "Shinzui/HdrpParticles" })
        {
            var shader = Shader.Find(name);
            if (shader == null) throw new InvalidOperationException("Missing runtime shader: " + name);
            bool present = false;
            for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
            if (!present) { int index = list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue = shader; }
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
}
