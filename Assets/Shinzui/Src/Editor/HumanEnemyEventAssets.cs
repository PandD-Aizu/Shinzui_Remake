using System;
using System.Linq;
using Shinzui.View;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Shinzui.Editor
{
    /// <summary>人型イベント敵と導入演出の保存済みアセットを構築</summary>
    public static class HumanEnemyEventAssets
    {
        public const string Folder = "Assets/Shinzui/Art/HumanEnemy";
        public const string EnemyPrefab = "Assets/Shinzui/Prefabs/HumanEventEnemy.prefab";
        public const string IntroPrefab = "Assets/Shinzui/Prefabs/StageIntroEvent.prefab";
        private const string Stage = "Assets/Shinzui/Scenes/StageTemp.unity";

        /// <summary>モデル設定と敵・導入Prefabを一度作成</summary>
        [MenuItem("Shinzui/Enemies/Human Event/Create Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before creating assets");
            AssetDatabase.Refresh();
            string modelPath = Folder + "/HumanEnemyRig.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = true;
                clip.loopPose = true;
                clip.lockRootRotation = true;
                clip.lockRootPositionXZ = true;
                clip.lockRootHeightY = true;
                clip.name = clip.name.Contains("Walk") ? "Walk" : "Idle";
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            // 元の色テクスチャをURPの肌マテリアルへ接続
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/HumanEnemySkin.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Texture0.jpg"));
                material.SetColor("_BaseColor", new Color(.8f, .8f, .8f));
                material.SetFloat("_Smoothness", .32f);
                AssetDatabase.CreateAsset(material, Folder + "/HumanEnemySkin.mat");
            }
            var controller = BuildController(modelPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefab) == null)
                BuildEnemy(modelPath, material, controller);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(IntroPrefab) == null) BuildIntroduction();
            AssetDatabase.SaveAssets();
        }

        /// <summary>待機と速度同期した歩行を切り替えるAnimatorを作成</summary>
        /// <param name="modelPath">歩行クリップを含むモデル</param>
        /// <returns>保存済みAnimator Controller</returns>
        private static AnimatorController BuildController(string modelPath)
        {
            string path = Folder + "/HumanEnemy.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null) return controller;
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
            var idle = clips.First(c => c.name == "Idle");
            var walk = clips.First(c => c.name == "Walk");
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("StrideRate", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            var idleState = machine.AddState("Idle"); idleState.motion = idle;
            var walkState = machine.AddState("Walk"); walkState.motion = walk;
            walkState.speedParameter = "StrideRate"; walkState.speedParameterActive = true;
            machine.defaultState = idleState;
            var start = idleState.AddTransition(walkState);
            start.hasExitTime = false; start.duration = .18f;
            start.AddCondition(AnimatorConditionMode.Greater, .05f, "Speed");
            var stop = walkState.AddTransition(idleState);
            stop.hasExitTime = false; stop.duration = .2f;
            stop.AddCondition(AnimatorConditionMode.Less, .04f, "Speed");
            return controller;
        }

        /// <summary>骨格モデルを既存の敵AIと接地補正へ接続</summary>
        /// <param name="modelPath">骨格モデルのパス</param>
        /// <param name="material">肌マテリアル</param>
        /// <param name="controller">歩行Animator</param>
        private static void BuildEnemy(string modelPath, Material material, AnimatorController controller)
        {
            var root = new GameObject("HumanEventEnemy");
            try
            {
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = .32f; agent.height = 2.2f; agent.speed = 1.25f;
                agent.acceleration = 3f; agent.angularSpeed = 140f; agent.stoppingDistance = .15f;
                var collider = root.AddComponent<CapsuleCollider>();
                collider.radius = .32f; collider.height = 2.15f; collider.center = Vector3.up * 1.075f;
                var enemy = root.AddComponent<EnemyView>();
                var enemySettings = new SerializedObject(enemy);
                enemySettings.FindProperty("agentOverride").objectReferenceValue = agent;
                enemySettings.FindProperty("playerDeathDistance").floatValue = 1.05f;
                enemySettings.ApplyModifiedPropertiesWithoutUndo();
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
                model.name = "Visual";
                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    renderer.sharedMaterial = material;
                    renderer.updateWhenOffscreen = true;
                }
                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var motion = root.AddComponent<HumanEnemyLocomotion>();
                var settings = new SerializedObject(motion);
                settings.FindProperty("animator").objectReferenceValue = animator;
                settings.FindProperty("agent").objectReferenceValue = agent;
                var bones = model.GetComponentsInChildren<Transform>();
                foreach (var side in new[] { ("leftLeg", "L"), ("rightLeg", "R") })
                {
                    var leg = settings.FindProperty(side.Item1);
                    leg.FindPropertyRelative("thigh").objectReferenceValue = bones.First(t => t.name == "Thigh." + side.Item2);
                    leg.FindPropertyRelative("shin").objectReferenceValue = bones.First(t => t.name == "Shin." + side.Item2);
                    leg.FindPropertyRelative("foot").objectReferenceValue = bones.First(t => t.name == "Foot." + side.Item2);
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefab);
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>敵とチュートリアル表示を持つ導入Prefabを作成</summary>
        private static void BuildIntroduction()
        {
            var root = new GameObject("StageIntroEvent");
            try
            {
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefab), root.transform);
                var view = root.AddComponent<StageIntroView>();
                var canvasObject = new GameObject("IntroOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
                canvasObject.transform.SetParent(root.transform, false);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var group = canvasObject.GetComponent<CanvasGroup>();
                group.interactable = false; group.blocksRaycasts = false; group.alpha = 0;
                var label = new GameObject("Tutorial", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(canvasObject.transform, false);
                var rect = label.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(.5f, .18f); rect.anchorMax = rect.anchorMin;
                rect.sizeDelta = new Vector2(1100, 160);
                var text = label.GetComponent<TextMeshProUGUI>();
                text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Shinzui/Font/NotoSansJP-VariableFont_wght SDF.asset");
                text.fontSize = 38; text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                text.text = "走る\n<size=65%>Shift を押しながら移動</size>";
                var serialized = new SerializedObject(view);
                serialized.FindProperty("eventEnemy").objectReferenceValue = enemy.GetComponent<EnemyView>();
                serialized.FindProperty("overlay").objectReferenceValue = group;
                serialized.FindProperty("message").objectReferenceValue = text;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, IntroPrefab);
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>タイトルから始まるステージへ導入イベントを一つ配置</summary>
        [MenuItem("Shinzui/Enemies/Human Event/Install in Gameplay Stage")]
        public static void Install()
        {
            CreateAssets();
            var scene = SceneManager.GetSceneByPath(Stage);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(Stage, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Save StageTemp before installing the introduction");
            if (scene.GetRootGameObjects().Any(g => g.GetComponent<StageIntroView>() != null)) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(IntroPrefab), scene);
            BindHud(instance.GetComponent<StageIntroView>(), scene);
            var enemy = instance.GetComponentInChildren<EnemyView>();
            var existing = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyView>()).FirstOrDefault(e => e != enemy);
            if (existing != null) enemy.transform.position = existing.EnemyPosition + Vector3.forward * 3f;
            // 明示された敵一覧を使うシーンも同じ個体を登録
            foreach (var scope in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Shinzui.DI.PlayerLifetimeScope>()))
            {
                var data = new SerializedObject(scope);
                var roster = data.FindProperty("enemyViews");
                if (roster.arraySize == 0) continue;
                roster.InsertArrayElementAtIndex(roster.arraySize);
                roster.GetArrayElementAtIndex(roster.arraySize - 1).objectReferenceValue = enemy;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }

        /// <summary>既存HUDを転倒中の一時非表示対象へ登録</summary>
        /// <param name="intro">導入演出</param>
        /// <param name="scene">対象シーン</param>
        public static void BindHud(StageIntroView intro, Scene scene)
        {
            var hud = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).FirstOrDefault(c => c.name == "HUDCanvas");
            var data = new SerializedObject(intro);
            data.FindProperty("gameplayHud").objectReferenceValue = hud;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
