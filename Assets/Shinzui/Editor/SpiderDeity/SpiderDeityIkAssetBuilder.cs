using System;
using System.IO;
using System.Linq;
using Shinzui.Infrastructure.Animation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Shinzui.Editor.SpiderDeity
{
    /// <summary>Authors the spider's engine-side rig without changing gameplay layer dependencies.</summary>
    public static class SpiderDeityIkAssetBuilder
    {
        public const string ModelPath = "Assets/Shinzui/3DModels/SpiderDeity/SpiderDeity_Rigged.fbx";
        public const string Folder = "Assets/Shinzui/Prefabs/SpiderDeity";
        public const string PrefabPath = Folder + "/SpiderDeity_IK.prefab";
        public const string PreviewScenePath = "Assets/Shinzui/Scenes/SpiderDeityIKPreview.unity";

        [MenuItem("Tools/Shinzui/Spider Deity/Create or Update IK Assets")]
        public static void BuildAssets()
        {
            if (!UnityEngine.Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previousScenes = EditorSceneManager.GetSceneManagerSetup();
            EnsureFolder(Folder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = null;
            try
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), scene);
                if (!root) throw new InvalidOperationException("SpiderDeity rigged FBX is missing.");
                // Preserve the imported model as a nested prefab instead of duplicating its mesh.
                var wrapper = new GameObject("SpiderDeity_IK");
                SceneManager.MoveGameObjectToScene(wrapper, scene);
                root.transform.SetParent(wrapper.transform, false);
                var importedAnimator = root.GetComponent<Animator>();
                if (importedAnimator) Object.DestroyImmediate(importedAnimator);
                root = wrapper;
                var animator = root.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var rig = Child(root.transform, "LegRig").gameObject.AddComponent<Rig>();
                var targets = Child(rig.transform, "Targets");
                targets.gameObject.AddComponent<RigTransform>();
                var constraints = Child(rig.transform, "Constraints");
                foreach (string side in new[] { "L", "R" })
                for (int number = 1; number <= 4; number++)
                {
                    string leg = $"Leg_{side}{number:00}";
                    var upper = Find(root, leg + "_Upper");
                    var tip = Find(root, leg + "_Tip");
                    var target = Child(targets, "IK_" + leg);
                    target.SetPositionAndRotation(tip.position, tip.rotation);
                    var constraint = Child(constraints, leg + "_IK").gameObject.AddComponent<ChainIKConstraint>();
                    var data = constraint.data;
                    data.root = upper;
                    data.tip = tip;
                    data.target = target;
                    data.chainRotationWeight = 1f;
                    data.tipRotationWeight = 0f;
                    data.maintainTargetPositionOffset = false;
                    data.maintainTargetRotationOffset = false;
                    data.maxIterations = 40;
                    data.tolerance = 0.0001f;
                    constraint.data = data;
                    constraint.weight = 1f;
                }

                // A procedural-only rig needs no Animator Controller. Keeping controls
                // scene-driven also avoids Animator defaults overwriting IK targets.
                animator.runtimeAnimatorController = null;
                var builder = root.AddComponent<RigBuilder>();
                builder.layers.Add(new RigLayer(rig));
                builder.enabled = false;
                root.AddComponent<SpiderDeityIkRig>();
                // Prevent the renderer from freezing while feet are moved beyond its bind bounds.
                foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    renderer.updateWhenOffscreen = true;
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);

                var motion = root.AddComponent<SpiderLegIkPreviewMotion>();
                motion.Configure(Enumerable.Range(0, targets.childCount).Select(targets.GetChild).ToArray());
                CreatePreviewEnvironment(scene);
                EditorSceneManager.SaveScene(scene, PreviewScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("SPIDER_IK_ASSETS_CREATED " + PrefabPath);
            }
            finally
            {
                if (!UnityEngine.Application.isBatchMode && previousScenes.All(s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            }
        }

        [MenuItem("Tools/Shinzui/Spider Deity/Open IK Preview")]
        public static void OpenPreview()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(PreviewScenePath);
        }

        private static void CreatePreviewEnvironment(Scene scene)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.name = "Preview Ground";
            ground.transform.localScale = Vector3.one * 0.3f;
            ground.transform.position = new Vector3(0, -0.003f, 0);
            const string materialPath = Folder + "/PreviewGround.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(0.11f, 0.14f, 0.18f));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            ground.GetComponent<Renderer>().sharedMaterial = material;
            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(1.25f, 0.95f, 1.8f);
            camera.transform.LookAt(new Vector3(0, 0.48f, 0));
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 20f;
            camera.fieldOfView = 35f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.028f, 0.037f, 0.055f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var key = new GameObject("Preview Key Light").AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(key.gameObject, scene);
            key.type = LightType.Directional;
            key.intensity = 2f;
            key.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
            var fill = new GameObject("Preview Fill Light").AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(fill.gameObject, scene);
            fill.type = LightType.Directional;
            fill.intensity = 0.7f;
            fill.color = new Color(0.6f, 0.75f, 1f);
            fill.transform.rotation = Quaternion.Euler(25f, 150f, 0f);
        }

        private static Transform Find(GameObject root, string name) =>
            root.GetComponentsInChildren<Transform>().Single(t => t.name == name);

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
