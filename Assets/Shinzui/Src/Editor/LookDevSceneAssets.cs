using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Editor
{
    /// <summary>
    /// 描画パイプライン比較用に生成済みトンネルを固定シーンへ書き出す
    /// </summary>
    public static class LookDevSceneAssets
    {
        private const string Folder = "Assets/Shinzui/LookDev";
        private const string SnapshotPath = Folder + "/TunnelSnapshot.prefab";
        private const string ProfilePath = Folder + "/LookDev_URP_VolumeProfile.asset";
        private const string SourceProfilePath = "Assets/Shinzui/GraphicSettings/QualityHigh_VolumeProfile.asset";
        private const string ScenePath = "Assets/Shinzui/Scenes/DevScenes/LookDev_URP.unity";
        private const float Radius = 45f;
        private static readonly Vector3 CameraPosition = new(-28f, 1.7f, 1.2f);
        private static readonly Vector3 CameraEuler = new(4f, 90f, 0f);

        /// <summary>
        /// プレイ中のカメラ周辺にある形状と光源を静的なプレハブとして保存する
        /// </summary>
        [MenuItem("Shinzui/LookDev/Capture Tunnel Snapshot")]
        public static void CaptureSnapshot()
        {
            Camera camera = Camera.main;
            GameObject mapRoot = GameObject.Find("MapRoot");
            if (camera == null || mapRoot == null)
            {
                Debug.LogError("LookDev: Main CameraとMapRootがあるプレイ中のシーンで実行する");
                return;
            }

            EnsureFolder(Folder);
            EnsureFolder(Folder + "/Meshes");
            EnsureFolder(Folder + "/Materials");
            Vector3 center = camera.transform.position;
            var root = new GameObject("TunnelSnapshot");
            var savedMeshes = new Dictionary<Mesh, Mesh>();
            var savedMaterials = new Dictionary<Material, Material>();

            // 周辺の静的形状を複製し、実行時生成のメッシュと材質はアセット化する
            foreach (MeshRenderer source in mapRoot.GetComponentsInChildren<MeshRenderer>())
            {
                MeshFilter filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                if (source.bounds.SqrDistance(center) > Radius * Radius) continue;
                if (System.Array.Exists(source.sharedMaterials, m => m != null && m.shader.name.StartsWith("Custom/Portal"))) continue;

                var copy = new GameObject(source.name);
                copy.transform.SetParent(root.transform, false);
                copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                copy.transform.localScale = source.transform.lossyScale;
                copy.AddComponent<MeshFilter>().sharedMesh = Persist(filter.sharedMesh, savedMeshes, Folder + "/Meshes");
                var renderer = copy.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = source.shadowCastingMode;
                var materials = source.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null) materials[i] = Persist(materials[i], savedMaterials, Folder + "/Materials");
                }

                renderer.sharedMaterials = materials;
            }

            // 天井灯と懐中電灯を含む周辺の光源を設定ごと複製する
            foreach (Light source in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (!source.isActiveAndEnabled || source.type == LightType.Directional) continue;
                if ((source.transform.position - center).sqrMagnitude > Radius * Radius) continue;

                var copy = new GameObject(source.name);
                copy.transform.SetParent(root.transform, false);
                copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                EditorUtility.CopySerialized(source, copy.AddComponent<Light>());
                var data = source.GetComponent<UniversalAdditionalLightData>();
                if (data != null)
                {
                    var copyData = copy.GetComponent<UniversalAdditionalLightData>();
                    if (copyData == null) copyData = copy.AddComponent<UniversalAdditionalLightData>();
                    EditorUtility.CopySerialized(data, copyData);
                }
                foreach (MonoBehaviour extra in source.GetComponents<MonoBehaviour>())
                {
                    if (extra == null || extra.GetType().Name != "VolumetricAdditionalLight") continue;
                    EditorUtility.CopySerialized(extra, copy.AddComponent(extra.GetType()));
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, SnapshotPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"LookDev: {savedMeshes.Count} meshes, {savedMaterials.Count} materials saved to {SnapshotPath}");
        }

        /// <summary>
        /// 保存済みスナップショットと中立な画作り用Volumeで比較シーンを作る
        /// </summary>
        [MenuItem("Shinzui/LookDev/Build URP LookDev Scene")]
        public static void BuildScene()
        {
            var snapshot = AssetDatabase.LoadAssetAtPath<GameObject>(SnapshotPath);
            if (snapshot == null)
            {
                Debug.LogError("LookDev: 先にCapture Tunnel Snapshotを実行する");
                return;
            }

            // 比較の公平さを保つため、ゲーム固有の演出を外した中立な画作りのプロファイルを作り直す
            AssetDatabase.DeleteAsset(ProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            // パイプライン既定のプロファイルが下敷きになるため、全項目を既定値で上書きして打ち消す
            // 自動露出とボリュメトリックフォグは実機の見え方を左右するのでゲームの設定を引き継ぐ
            var source = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SourceProfilePath);
            foreach (VolumeComponent original in source.components)
            {
                string name = original.GetType().Name;
                if (name == "ProbeVolumesOptions") continue;
                if (name is "ExposureVolume" or "VolumetricFogVolumeComponent")
                {
                    VolumeComponent copy = Object.Instantiate(original);
                    copy.name = original.name;
                    profile.components.Add(copy);
                    continue;
                }

                VolumeComponent neutral = profile.Add(original.GetType(), true);
                if (name is "ScanlineVolume" or "InverseScanlineVolume")
                {
                    foreach (VolumeParameter parameter in neutral.parameters)
                    {
                        if (parameter is ClampedFloatParameter clamped) clamped.value = clamped.min;
                    }
                }
            }

            // 比較用の画作りはACESと控えめなブルーム、周辺減光のみとする
            profile.TryGet(out Tonemapping tonemapping);
            tonemapping.mode.value = TonemappingMode.ACES;
            profile.TryGet(out Bloom bloom);
            bloom.intensity.value = 0.6f;
            bloom.threshold.value = 1.0f;
            profile.TryGet(out Vignette vignette);
            vignette.intensity.value = 0.25f;

            // 単フレームの撮影でも順応途中の値にならないよう露出を固定する
            profile.TryGet(out Infrastructure.Rendering.Exposure.ExposureVolume exposure);
            exposure.mode.value = Infrastructure.Rendering.Exposure.ExposureMode.Fixed;
            exposure.eyeAdaptation.value = Infrastructure.Rendering.Exposure.EyeAdaptationMode.Instant;
            exposure.exposureCompensation.value = 1.0f;
            exposure.mode.overrideState = exposure.eyeAdaptation.overrideState = exposure.exposureCompensation.overrideState = true;

            foreach (VolumeComponent component in profile.components)
            {
                if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
            }

            EditorUtility.SetDirty(profile);

            // スナップショットと目線高さのカメラ、全体Volumeを配置する
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(snapshot);
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetPositionAndRotation(CameraPosition, Quaternion.Euler(CameraEuler));
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60;
            camera.nearClipPlane = 0.05f;
            camera.backgroundColor = Color.black;
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            // 環境光は暗所を前提に黒とし、光源以外からの明るさを排除する
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.skybox = null;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// アセットでないメッシュや材質を複製して保存し、既存アセットはそのまま返す
        /// </summary>
        /// <param name="source">保存対象</param>
        /// <param name="saved">保存済みの対応表</param>
        /// <param name="folder">保存先フォルダ</param>
        /// <returns>アセットとして参照できるオブジェクト</returns>
        private static T Persist<T>(T source, Dictionary<T, T> saved, string folder) where T : Object
        {
            if (AssetDatabase.Contains(source)) return source;
            if (saved.TryGetValue(source, out T asset)) return asset;

            asset = Object.Instantiate(source);
            asset.name = source.name;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{source.name.Replace('|', '_')}.{(source is Mesh ? "asset" : "mat")}");
            AssetDatabase.CreateAsset(asset, path);
            saved[source] = asset;
            return asset;
        }

        /// <summary>
        /// フォルダがなければ親フォルダの下に作成する
        /// </summary>
        /// <param name="path">作成するフォルダのパス</param>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
