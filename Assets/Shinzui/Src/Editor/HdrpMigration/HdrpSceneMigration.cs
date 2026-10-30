using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Shinzui.Infrastructure.Lighting;
using Shinzui.Infrastructure.Rendering;
using Object = UnityEngine.Object;

namespace Shinzui.Editor.HdrpMigration
{
    /// <summary>既存シーンとプレハブをGUIDを保って移行する</summary>
    public static class HdrpSceneMigration
    {
        private const string Marker = "Shinzui.HDRP.Phase2.v1";
        private const string HorrorPrefab = "Assets/Shinzui/Resources/HdrpHorrorDetection.prefab";
        private static int cameras, lights, missing, bounce, unknown;

        /// <summary>既存アセットへHDRPのカメラ、光源とVolumeを設定する</summary>
        [MenuItem("Shinzui/HDRP Migration/5 Migrate Scenes And Prefabs")]
        public static void Migrate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before migration");
            /* 未保存の編集内容へバッチ処理の保存を混ぜない */
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before running HDRP migration");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Close Prefab Mode before running HDRP migration");
            cameras = lights = missing = bounce = unknown = 0;
            int prefabs = 0, scenes = 0, volumes = 0;
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:VolumeProfile", new[] { "Assets/Shinzui", "Assets/Settings" }))
                    volumes += HdrpVolumeMigration.Migrate(AssetDatabase.GUIDToAssetPath(guid));
                AssetDatabase.SaveAssets();
                SetupHorrorDetection();
                ConfigureBuiltInEffects();

                /* 依存するプレハブを先に移行しネストされた参照のGUIDを保持する */
                var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Shinzui" })
                    .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".prefab") && p != HorrorPrefab)
                    .OrderBy(p => AssetDatabase.GetDependencies(p, true).Count(d => d.EndsWith(".prefab"))).ThenBy(p => p).ToArray();
                foreach (string path in paths)
                {
                    if (!AssetDatabase.IsOpenForEdit(path)) { Debug.LogWarning($"HDRP prefab skipped (read-only): {path}"); unknown++; continue; }
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        ProcessRoot(root, path, !HasMarker(path));
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        SetMarker(path);
                        prefabs++;
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Shinzui/Scenes" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!AssetDatabase.IsOpenForEdit(path)) { Debug.LogWarning($"HDRP scene skipped (read-only): {path}"); unknown++; continue; }
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    foreach (GameObject root in scene.GetRootGameObjects()) ProcessRoot(root, path, !HasMarker(path));
                    /* グローバルな既定霧はPhase 1のプロファイルを使い局所領域を推測しない */
                    if (RenderSettings.fog)
                    {
                        Debug.LogWarning($"HDRP legacy RenderSettings fog disabled: {path}, mode={RenderSettings.fogMode}, density={RenderSettings.fogDensity}; review against HDRP global Fog");
                        RenderSettings.fog = false;
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    SetMarker(path);
                    scenes++;
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previous);
                Debug.Log($"HDRP scenes/prefabs: scenes={scenes}, prefabs={prefabs}, cameras added={cameras}, lights initialized={lights}, missing scripts removed={missing}, fake bounce removed={bounce}, volume components migrated={volumes}, review/skipped={unknown}");
            }
        }

        /// <summary>階層の欠落スクリプトとレンダリング設定を移行する</summary>
        /// <param name="root">処理する階層</param>
        /// <param name="path">保存先パス</param>
        /// <param name="initialize">初回の強度調整を行うか</param>
        private static void ProcessRoot(GameObject root, string path, bool initialize)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                if (count == 0) continue;
                missing += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                Debug.Log($"HDRP missing scripts removed={count}: {path} / {Hierarchy(child)}");
            }
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                bool added = !camera.TryGetComponent(out HDAdditionalCameraData data);
                if (added)
                {
                    data = camera.gameObject.AddComponent<HDAdditionalCameraData>();
                    data.clearColorMode = camera.clearFlags == CameraClearFlags.Skybox ? HDAdditionalCameraData.ClearColorMode.Sky
                        : camera.clearFlags == CameraClearFlags.Color ? HDAdditionalCameraData.ClearColorMode.Color : HDAdditionalCameraData.ClearColorMode.None;
                    data.backgroundColorHDR = camera.backgroundColor;
                    data.clearDepth = camera.clearFlags != CameraClearFlags.Nothing;
                    cameras++;
                }
                if (initialize && (camera.CompareTag("MainCamera") || Hierarchy(camera.transform).ToLowerInvariant().Contains("player")))
                    data.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
                if (initialize && data.customRenderingSettings)
                {
                    /* 個別フレーム設定でも演出と色ピラミッドを利用できるようにする */
                    data.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.CustomPass, true);
                    data.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.Refraction, true);
                }
                EditorUtility.SetDirty(data);
            }
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                string hierarchy = Hierarchy(light.transform);
                string role = (path + "/" + hierarchy).ToLowerInvariant();
                /* 器具の専用補助点光源だけを削除する */
                if (light.name == "Ceiling Bounce" && (role.Contains("tunnelledlight") || role.Contains("tunnelfluorescentlight")))
                {
                    Debug.Log($"HDRP fake GI removed: {path} / {hierarchy}, point light used only for ceiling bounce");
                    Object.DestroyImmediate(light.gameObject);
                    bounce++;
                    continue;
                }
                if (!initialize && light.GetComponent<HDAdditionalLightData>() != null) continue;
                InitializeLight(light, role, path, hierarchy);
            }
            foreach (Volume volume in root.GetComponentsInChildren<Volume>(true))
            {
                if (volume.sharedProfile == null)
                {
                    Debug.LogWarning($"HDRP volume without profile REVIEW: {path} / {Hierarchy(volume.transform)}");
                    unknown++;
                }
                else Debug.Log($"HDRP volume retained in place: {path} / {Hierarchy(volume.transform)} -> {AssetDatabase.GetAssetPath(volume.sharedProfile)}");
            }
        }

        /// <summary>役割に応じて物理単位を明示しライトを初期化する</summary>
        /// <param name="light">対象光源</param>
        /// <param name="role">役割の検索文字列</param>
        /// <param name="path">アセットパス</param>
        /// <param name="hierarchy">階層名</param>
        private static void InitializeLight(Light light, string role, string path, string hierarchy)
        {
            float intensity;
            LightUnit unit = LightUnit.Lumen;
            string reason;
            if (role.Contains("tunnelledlight")) { intensity = 3000; reason = "tunnel LED"; light.type = LightType.Spot; }
            else if (role.Contains("fluorescent")) { intensity = 3000; reason = "fluorescent"; }
            else if (role.Contains("flashlight")) { intensity = 750; reason = "flashlight"; light.type = LightType.Spot; }
            else if (role.Contains("penlight") || role.Contains("glowstick")) { intensity = 8; reason = "glow stick"; }
            else if (role.Contains("firealarm") || role.Contains("alarm")) { intensity = 30; reason = "alarm lamp"; }
            else if (role.Contains("match")) { intensity = 12; reason = "match"; }
            else if (role.Contains("blackhole")) { intensity = 80; reason = "black hole event"; }
            else if (role.Contains("intro") || role.Contains("event")) { intensity = 600; reason = "intro/event"; }
            else
            {
                /* 未分類の演出光は現在の強度を維持し機械的な増幅を避ける */
                intensity = light.intensity;
                unit = LightUnitUtils.GetNativeLightUnit(light.type);
                reason = "unclassified, native intensity preserved";
                unknown++;
                Debug.LogWarning($"HDRP light role REVIEW: {path} / {hierarchy}");
            }
            if (light.type == LightType.Directional)
            {
                unit = LightUnit.Lux;
                intensity = light.intensity;
                reason = "directional lux preserved";
            }
            var data = HdrpLightUtility.SetIntensity(light, intensity, unit);
            data.affectsVolumetric = true;
            data.volumetricDimmer = role.Contains("flashlight") ? .7f : .5f;
            if (role.Contains("flashlight"))
            {
                light.spotAngle = Mathf.Clamp(light.spotAngle, 20, 45);
                light.innerSpotAngle = Mathf.Min(light.innerSpotAngle, light.spotAngle * .75f);
                /* 角度によってルーメン換算が変わるため形状確定後に反映する */
                HdrpLightUtility.SetIntensity(light, intensity, unit);
            }
            var physical = light.GetComponent<PhysicalLight>();
            if (physical != null) physical.SyncFromLight();
            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(data);
            if (physical != null) EditorUtility.SetDirty(physical);
            lights++;
            Debug.Log($"HDRP light: {path} / {hierarchy}, {intensity} {unit}, role={reason}");
        }

        /// <summary>元の恐怖演出を読み取り常駐Volumeのプレハブへ保存する</summary>
        private static void SetupHorrorDetection()
        {
            const string source = "Assets/Shinzui/GraphicSettings/QualityHigh_Renderer.asset";
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Shinzui/Materials/HorrorDetection.mat");
            if (File.Exists(source))
            {
                string block = Regex.Split(File.ReadAllText(source), @"(?m)^--- !u!")
                    .FirstOrDefault(b => b.Contains("m_Name: HorrorDetection"));
                if (block == null || !block.Contains("FullScreenPassRendererFeature") ||
                    HdrpVolumeMigration.Scalar(block, "injectionPoint") != "600")
                    throw new InvalidOperationException($"Unknown HorrorDetection renderer configuration: {source}");
                string guid = Regex.Match(block, @"passMaterial:.*guid:\s*([a-f0-9]+)").Groups[1].Value;
                material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                Debug.Log($"HDRP HorrorDetection source (read-only YAML): {source}, FullScreenPass, injectionPoint=600 -> AfterPostProcess, fetchColorBuffer=1 -> explicit RTHandle copy, no layer/queue filter, passIndex=0");
            }
            else Debug.Log($"HDRP HorrorDetection: legacy source absent, retaining known fullscreen material: {AssetDatabase.GetAssetPath(material)}");
            if (material == null) throw new InvalidOperationException("Missing HorrorDetection material");
            bool exists = File.Exists(HorrorPrefab);
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(HorrorPrefab) : new GameObject("HDRP Horror Detection");
            try
            {
                var volume = root.GetComponent<CustomPassVolume>();
                if (volume == null) volume = root.AddComponent<CustomPassVolume>();
                volume.isGlobal = true;
                volume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
                volume.customPasses.RemoveAll(p => p is HdrpHorrorDetectionPass);
                volume.customPasses.Add(new HdrpHorrorDetectionPass
                {
                    name = "HorrorDetection", material = material,
                    targetColorBuffer = CustomPass.TargetBuffer.Camera, targetDepthBuffer = CustomPass.TargetBuffer.None
                });
                PrefabUtility.SaveAsPrefabAsset(root, HorrorPrefab);
                Debug.Log($"HDRP HorrorDetection volume configured=1: {HorrorPrefab}, runtime singleton, portal/reflection cameras excluded");
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        /// <summary>旧Rendererの環境遮蔽をHDRPへ移し必要なフレーム機能を有効にする</summary>
        private static void ConfigureBuiltInEffects()
        {
            const string source = "Assets/Shinzui/GraphicSettings/QualityHigh_Renderer.asset";
            int configured = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:VolumeProfile", new[] { "Assets/Shinzui/GraphicSettings" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("HDRP")) continue;
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile.TryGet(out ScreenSpaceAmbientOcclusion _)) continue;
                var ao = profile.Add<ScreenSpaceAmbientOcclusion>(true);
                ao.name = "ScreenSpaceAmbientOcclusion";
                ao.rayTracing.Override(false);
                ao.intensity.Override(path.Contains("Low") ? 1 : 3);
                ao.directLightingStrength.Override(.25f);
                ao.radius.Override(.25f);
                ao.quality.Override(path.Contains("Low") ? 0 : path.Contains("Midium") ? 1 : 2);
                AssetDatabase.AddObjectToAsset(ao, profile);
                EditorUtility.SetDirty(profile);
                configured++;
                Debug.Log($"HDRP SSAO: {path}, source={source}, intensity={ao.intensity.value}, direct=.25, URP radius .035 -> HDRP minimum .25m; screen-space shadows use HDRP light/shadow settings");
            }
            /* Phase 1と同じHDRP 17.5の設定コンテナを利用する */
            var global = GraphicsSettings.GetSettingsForRenderPipeline<HDRenderPipeline>();
            if (global == null) throw new InvalidOperationException("Run Phase 1 Setup Pipeline before asset migration");
            var serialized = new SerializedObject(global);
            var iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ManagedReference ||
                    iterator.managedReferenceValue?.GetType().Name != "RenderingPathFrameSettings") continue;
                var camera = iterator.FindPropertyRelative("m_Camera");
                var frame = (FrameSettings)camera.boxedValue;
                frame.SetEnabled(FrameSettingsField.CustomPass, true);
                frame.SetEnabled(FrameSettingsField.SSAO, true);
                frame.SetEnabled(FrameSettingsField.Refraction, true);
                frame.SetEnabled(FrameSettingsField.TransparentObjects, true);
                camera.boxedValue = frame;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(global);
                Debug.Log($"HDRP frame settings updated=1, SSAO profiles added={configured}");
                return;
            }
            throw new InvalidOperationException("HDRP RenderingPathFrameSettings container missing");
        }

        /// <summary>アセットの初期化済み記録を取得する</summary>
        /// <param name="path">アセットパス</param>
        /// <returns>初期化済みか</returns>
        private static bool HasMarker(string path) => (AssetImporter.GetAtPath(path)?.userData ?? "").Contains(Marker);

        /// <summary>再実行で手調整した物理強度を失わないよう記録する</summary>
        /// <param name="path">アセットパス</param>
        private static void SetMarker(string path)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null || HasMarker(path)) return;
            importer.userData = (importer.userData + "\n" + Marker).Trim();
            AssetDatabase.WriteImportSettingsIfDirty(path);
        }

        /// <summary>診断用の階層名を取得する</summary>
        /// <param name="transform">対象</param>
        /// <returns>階層名</returns>
        private static string Hierarchy(Transform transform) => transform.parent == null ? transform.name : Hierarchy(transform.parent) + "/" + transform.name;
    }
}
