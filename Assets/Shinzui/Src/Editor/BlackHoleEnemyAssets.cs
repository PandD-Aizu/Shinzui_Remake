using System;
using System.Linq;
using Shinzui.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Shinzui.Editor
{
    /// <summary>Authors ordinary assets; no runtime generation or additional gameplay layer.</summary>
    public static class BlackHoleEnemyAssets
    {
        public const string Folder = "Assets/Shinzui/Art/BlackHoleEnemy";
        public const string PrefabPath = "Assets/Shinzui/Prefabs/BlackHoleEnemy.prefab";
        public const string PreviewPath = "Assets/Shinzui/Scenes/BlackHoleEnemyPreview.unity";
        private const string StagePath = "Assets/Shinzui/Scenes/StageTemp.unity";

        /// <summary>
        /// ブラックホール眼の敵とプレビュー用アセットを作成
        /// </summary>
        [MenuItem("Shinzui/Enemies/Black Hole/Create Assets and Preview")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring assets.");
            EnsureFolder(Folder);
            var shader = Shader.Find("Shinzui/BlackHoleEye");
            if (!shader || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("BlackHoleEye shader is missing or has compile errors.");
            var left = MaterialAsset("EyeLeft", shader, new Color(0.65f, 0.69f, 0.65f));
            var right = MaterialAsset("EyeRight", shader, new Color(0.62f, 0.59f, 0.5f));
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
            {
                ConfigureEyeMaterial(left, false);
                ConfigureEyeMaterial(right, true);
                BuildPrefab(left, right);
            }
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewPath)) BuildPreview();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 既存Prefabを背景が屈折する二つの浮遊眼へ更新
        /// </summary>
        [MenuItem("Shinzui/Enemies/Black Hole/Apply Floating Eyes")]
        public static void ApplyFloatingEyes()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring assets.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var visual = root.transform.Find("Visual");

                // 胴体とフードのみ除去し、既存の行動と浮遊アニメーションを保持
                foreach (string name in new[] { "Tattered Veil", "Hood", "Left Shoulder", "Right Shoulder", "Left Hanging Arm", "Right Hanging Arm" })
                {
                    var body = visual.Find(name);
                    if (body) Object.DestroyImmediate(body.gameObject);
                }

                ConfigureEyeMaterial(visual.Find("Left Singularity").GetComponent<Renderer>().sharedMaterial, false);
                ConfigureEyeMaterial(visual.Find("Right Singularity").GetComponent<Renderer>().sharedMaterial, true);
                ConfigureFloatingEyes(visual);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 暗い事象の地平面と背景を映す細い光輪を設定
        /// </summary>
        /// <param name="material">保存対象の眼マテリアル</param>
        /// <param name="right">右眼の場合は位相と形状を変更</param>
        private static void ConfigureEyeMaterial(Material material, bool right)
        {
            // 輪郭の大きなうねりを抑え、左右でわずかな非対称性を残す
            material.SetFloat("_Phase", right ? 2.1f : 0.2f);
            material.SetFloat("_HorizonRadius", right ? 0.29f : 0.31f);
            material.SetFloat("_Deformation", 0.012f);
            material.SetFloat("_RingWidth", 0.008f);
            material.SetFloat("_Intensity", 0.75f);
            material.SetFloat("_SceneRefraction", 0.95f);
            material.SetFloat("_SwirlSpeed", right ? -0.55f : 0.7f);
            material.SetVector("_EyeShape", new Vector4(1f, right ? 0.83f : 0.9f, right ? 0.06f : -0.04f, 0f));
            material.SetVector("_PupilOffset", Vector4.zero);
            material.SetColor("_RingColor", new Color(0.78f, 0.8f, 0.76f));
            material.SetColor("_DiskColor", new Color(0.12f, 0.105f, 0.085f));
            EditorUtility.SetDirty(material);
        }

        /// <summary>
        /// 浮遊眼の間隔と周囲の重力レンズを揃える
        /// </summary>
        /// <param name="visual">眼とレンズを持つ描画ルート</param>
        private static void ConfigureFloatingEyes(Transform visual)
        {
            // 眼の間隔を確保し、フードのない空間へ配置
            var left = visual.Find("Left Singularity");
            var right = visual.Find("Right Singularity");
            left.localPosition = new Vector3(-0.32f, 2.03f, 0.348f);
            right.localPosition = new Vector3(0.32f, 2.065f, 0.348f);
            left.localScale = Vector3.one * 0.92f;
            right.localScale = Vector3.one * 0.86f;

            // 眼に対応する位置へ背景の屈折中心を移し、波打ちを抑制
            AddLensing(visual);
            var lens = visual.Find("Gravitational Lens");
            lens.localScale = new Vector3(2.2f, 1.6f, 1f);
            var material = lens.GetComponent<Renderer>().sharedMaterial;
            material.SetVector("_LensSize", new Vector4(2.2f, 1.6f, 0f, 0f));
            material.SetVector("_EyeA", new Vector4(0.32f, -0.015f, 0.76f, 0.2f));
            material.SetVector("_EyeB", new Vector4(-0.32f, 0.02f, 0.72f, 2.1f));
            material.SetFloat("_PullStrength", 2.8f);
            material.SetFloat("_TwistStrength", 0.16f);
            material.SetFloat("_PulseStrength", 0.035f);
            material.SetFloat("_FlowSpeed", 0.45f);
            EditorUtility.SetDirty(material);

            // 眼の中心を保ち、左右の間隔と屈折範囲をまとめて縮小
            var center = new Vector3(0f, 2.045f, 0.35f);
            foreach (var part in new[] { left, right, lens })
            {
                part.localPosition = center + (part.localPosition - center) * 0.425f;
                part.localScale *= 0.425f;
            }
        }

        [MenuItem("Shinzui/Enemies/Black Hole/Enable Surrounding Distortion")]
        public static void EnableSurroundingDistortion()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the prefab.");
            var shader = Shader.Find("Shinzui/BlackHoleLens");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("BlackHoleLens shader has not imported successfully.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                AddLensing(root.transform.Find("Visual"));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // All three gameplay quality levels must provide a current opaque scene copy.
            foreach (string name in new[] { "QualityLow", "QualityMidium", "QualityHigh" })
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Shinzui/GraphicSettings/" + name + ".asset");
                if (pipeline && !pipeline.supportsCameraOpaqueTexture)
                {
                    pipeline.supportsCameraOpaqueTexture = true;
                    EditorUtility.SetDirty(pipeline);
                }
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Shinzui/Enemies/Black Hole/Add to Gameplay Stage")]
        public static void AddToGameplayStage()
        {
            CreateAssets();
            var scene = SceneManager.GetSceneByPath(StagePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(StagePath, OpenSceneMode.Additive);
            else if (scene.isDirty) throw new InvalidOperationException("Save StageTemp before installing the enemy.");
            try
            {
                if (scene.GetRootGameObjects().Any(g => g.name == "BlackHoleEnemy")) return;
                var existing = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyView>()).FirstOrDefault();
                var agent = existing ? existing.ResolveAgent() : null;
                // Match the original spawn lane, six metres farther into the tunnel.
                var position = agent ? agent.transform.position + Vector3.forward * 6f - Vector3.up * agent.baseOffset : new Vector3(0f, 0f, 20f);
                if (NavMesh.SamplePosition(position, out var hit, 3f, NavMesh.AllAreas)) position = hit.position;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));
                // Preserve explicitly authored rosters; empty rosters discover EnemyView automatically.
                foreach (var scope in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Shinzui.DI.PlayerLifetimeScope>()))
                {
                    var serialized = new SerializedObject(scope);
                    var roster = serialized.FindProperty("enemyViews");
                    if (roster == null || roster.arraySize == 0) continue;
                    int index = roster.arraySize++;
                    roster.GetArrayElementAtIndex(index).objectReferenceValue = instance.GetComponent<EnemyView>();
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("BlackHoleEnemy added to StageTemp: " + position);
            }
            finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
        }

        /// <summary>
        /// 二つのブラックホール眼だけが浮遊する敵Prefabを作成
        /// </summary>
        /// <param name="left">左目のマテリアル</param>
        /// <param name="right">右目のマテリアル</param>
        private static void BuildPrefab(Material left, Material right)
        {
            var staging = new GameObject("BlackHoleEnemy Authoring");
            staging.SetActive(false);
            var root = new GameObject("BlackHoleEnemy");
            root.transform.SetParent(staging.transform, false);
            root.SetActive(false);
            try
            {
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.45f;
                agent.height = 2.5f;
                agent.speed = 1.2f;
                agent.angularSpeed = 160f;
                agent.acceleration = 8f;
                agent.stoppingDistance = 0.7f;
                var collider = root.AddComponent<CapsuleCollider>();
                collider.center = new Vector3(0f, 1.25f, 0f);
                collider.height = 2.5f;
                collider.radius = 0.45f;
                var view = root.AddComponent<EnemyView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("agentOverride").objectReferenceValue = agent;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                Eye("Left Singularity", visual, new Vector3(-0.225f, 2.03f, 0.348f), 0.80f, left);
                Eye("Right Singularity", visual, new Vector3(0.225f, 2.065f, 0.348f), 0.88f, right);
                ConfigureFloatingEyes(visual);

                // 見た目だけをポータルから隠し、接触用コライダーのレイヤーは維持
                int hiddenLayer = LayerMask.NameToLayer("PortalHidden");
                if (hiddenLayer >= 0)
                    foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                        renderer.gameObject.layer = hiddenLayer;

                var animation = root.AddComponent<Animation>();
                var clip = HoverClip();
                animation.AddClip(clip, clip.name);
                animation.clip = clip;
                animation.playAutomatically = true;
                animation.cullingType = AnimationCullingType.AlwaysAnimate;
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(staging); }
        }

        private static void Eye(string name, Transform parent, Vector3 position, float size, Material material)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Quad);
            eye.name = name;
            Object.DestroyImmediate(eye.GetComponent<Collider>());
            eye.transform.SetParent(parent, false);
            eye.transform.localPosition = position;
            eye.transform.localRotation = Quaternion.Euler(0, 180, 0);
            eye.transform.localScale = Vector3.one * size;
            var renderer = eye.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void AddLensing(Transform visual)
        {
            if (!visual) throw new InvalidOperationException("BlackHoleEnemy has no Visual transform.");
            if (visual.Find("Gravitational Lens")) return;
            var material = MaterialAsset("SurroundingLens", Shader.Find("Shinzui/BlackHoleLens"), Color.white);
            var lens = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lens.name = "Gravitational Lens";
            Object.DestroyImmediate(lens.GetComponent<Collider>());
            lens.transform.SetParent(visual, false);
            lens.transform.localPosition = new Vector3(0f, 2.045f, 0.35f);
            lens.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            lens.transform.localScale = new Vector3(1.95f, 1.6f, 1f);
            var renderer = lens.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static AnimationClip HoverClip()
        {
            string path = Folder + "/SlowHover.anim";
            var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (saved) return saved;
            var clip = new AnimationClip { name = "SlowHover", legacy = true, wrapMode = WrapMode.Loop };
            clip.SetCurve("Visual", typeof(Transform), "localPosition.y", new AnimationCurve(
                new Keyframe(0, 0), new Keyframe(1.6f, 0.09f), new Keyframe(3.2f, 0)));
            clip.SetCurve("Visual", typeof(Transform), "localEulerAnglesRaw.z", new AnimationCurve(
                new Keyframe(0, -2.2f), new Keyframe(1.6f, 2.2f), new Keyframe(3.2f, -2.2f)));
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static void BuildPreview()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.12f, 0.16f, 0.2f);
                var staging = new GameObject("Preview Authoring");
                staging.SetActive(false);
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), staging.transform);
                // Preview is visual only; the gameplay prefab keeps its agent enabled.
                enemy.GetComponent<NavMeshAgent>().enabled = false;
                enemy.GetComponent<EnemyView>().enabled = false;
                enemy.transform.SetParent(null, false);
                Object.DestroyImmediate(staging);
                var camera = new GameObject("Preview Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0.3f, 1.7f, 4.6f);
                camera.transform.LookAt(new Vector3(0, 1.35f, 0));
                camera.fieldOfView = 39;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.012f, 0.019f, 0.028f);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Preview Floor";
                floor.transform.localScale = Vector3.one * 2f;
                floor.GetComponent<MeshRenderer>().sharedMaterial = MaterialAsset("PreviewFloor", Shader.Find("Universal Render Pipeline/Lit"), new Color(0.065f, 0.08f, 0.10f));
                PreviewLight("Cold Rim", new Vector3(-2, 3, -1), new Color(0.22f, 0.66f, 1f), 5f);
                PreviewLight("Soft Key", new Vector3(2, 4, 3), new Color(0.8f, 0.86f, 1f), 3f);
                EditorSceneManager.SaveScene(scene, PreviewPath);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void PreviewLight(string name, Vector3 position, Color color, float intensity)
        {
            var light = new GameObject(name, typeof(Light)).GetComponent<Light>();
            light.type = LightType.Point;
            light.transform.position = position;
            light.color = color;
            light.intensity = intensity;
            light.range = 8f;
        }

        private static Material MaterialAsset(string name, Shader shader, Color color)
        {
            string path = Folder + "/" + name + ".mat";
            var saved = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (saved) return saved;
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_RingColor")) material.SetColor("_RingColor", color);
            else if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
