using System;
using System.Collections.Generic;
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
                left.SetFloat("_Phase", 0.2f);
                left.SetFloat("_HorizonRadius", 0.28f);
                left.SetVector("_EyeShape", new Vector4(1.08f, 0.72f, -0.12f, 0));
                left.SetVector("_PupilOffset", new Vector4(0.035f, 0.012f, 0, 0));
                right.SetFloat("_Phase", 2.1f);
                right.SetFloat("_SwirlSpeed", -0.55f);
                right.SetFloat("_HorizonRadius", 0.21f);
                right.SetVector("_EyeShape", new Vector4(0.86f, 0.57f, 0.18f, 0));
                right.SetVector("_PupilOffset", new Vector4(-0.07f, 0.055f, 0, 0));
                var body = MaterialAsset("ObsidianVeil", Shader.Find("Shinzui/BlackHoleBody"), new Color(0.014f, 0.021f, 0.026f));
                // These are saved once and remain freely editable by artists afterwards.
                EditorUtility.SetDirty(left);
                EditorUtility.SetDirty(right);
                EditorUtility.SetDirty(body);
                BuildPrefab(left, right, body);
            }
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewPath)) BuildPreview();
            AssetDatabase.SaveAssets();
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
        /// ブラックホール眼と胴体を持つ敵Prefabを作成
        /// </summary>
        /// <param name="left">左目のマテリアル</param>
        /// <param name="right">右目のマテリアル</param>
        /// <param name="body">胴体のマテリアル</param>
        private static void BuildPrefab(Material left, Material right, Material body)
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
                var cloak = new GameObject("Tattered Veil", typeof(MeshFilter), typeof(MeshRenderer));
                cloak.transform.SetParent(visual, false);
                cloak.GetComponent<MeshFilter>().sharedMesh = CreateVeilMesh();
                cloak.GetComponent<MeshRenderer>().sharedMaterial = body;
                Sphere("Hood", visual, new Vector3(0f, 2.00f, 0f), new Vector3(1.02f, 1.04f, 0.68f), body);
                Sphere("Left Shoulder", visual, new Vector3(-0.42f, 1.54f, -0.03f), new Vector3(0.4f, 0.48f, 0.48f), body);
                Sphere("Right Shoulder", visual, new Vector3(0.42f, 1.54f, -0.03f), new Vector3(0.4f, 0.48f, 0.48f), body);
                Sphere("Left Hanging Arm", visual, new Vector3(-0.52f, 1.06f, 0f), new Vector3(0.22f, 1.0f, 0.26f), body);
                Sphere("Right Hanging Arm", visual, new Vector3(0.53f, 1.13f, 0f), new Vector3(0.22f, 0.93f, 0.26f), body);
                Eye("Left Singularity", visual, new Vector3(-0.225f, 2.03f, 0.348f), 0.80f, left);
                Eye("Right Singularity", visual, new Vector3(0.225f, 2.065f, 0.348f), 0.88f, right);
                AddLensing(visual);

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

        private static void Sphere(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Mesh CreateVeilMesh()
        {
            string path = Folder + "/Veil.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved) return saved;
            const int segments = 64;
            float[] heights = { 0.13f, 0.38f, 0.7f, 1.1f, 1.48f, 1.68f, 1.82f };
            float[] widths = { 0.27f, 0.36f, 0.41f, 0.45f, 0.52f, 0.38f, 0.23f };
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int ring = 0; ring < heights.Length; ring++)
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                float fold = 1f + 0.085f * Mathf.Sin(a * 11f + ring * 0.26f) + 0.04f * Mathf.Cos(a * 7f);
                float y = heights[ring] + (ring == 0 ? 0.18f * (0.5f + 0.5f * Mathf.Sin(a * 9f)) : 0f);
                vertices.Add(new Vector3(Mathf.Cos(a) * widths[ring] * fold, y, Mathf.Sin(a) * widths[ring] * 0.65f * fold));
                uv.Add(new Vector2((float)i / segments, (float)ring / (heights.Length - 1)));
                if (ring == heights.Length - 1 || i == segments) continue;
                int n = ring * (segments + 1) + i;
                triangles.AddRange(new[] { n, n + segments + 1, n + 1, n + 1, n + segments + 1, n + segments + 2 });
            }
            var mesh = new Mesh { name = "Tattered Veil" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
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
