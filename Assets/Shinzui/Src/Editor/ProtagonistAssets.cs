using System;
using System.Linq;
using Shinzui.View;
using Shinzui.View.Flashlight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shinzui.Editor
{
    /// <summary>主人公の骨格モデルと主観表示をシーンへ接続</summary>
    public static class ProtagonistAssets
    {
        public const string Folder = "Assets/Shinzui/Art/Protagonist";
        public const string PrefabPath = "Assets/Shinzui/Prefabs/ProtagonistBody.prefab";

        /// <summary>Humanoid設定と共通素材を適用して主人公Prefabを保存</summary>
        [MenuItem("Shinzui/Player/Build Protagonist Assets")]
        public static void BuildAssets()
        {
            AssetDatabase.Refresh();
            string path = Folder + "/Protagonist.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.isReadable = true;
            importer.optimizeGameObjects = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            // 指を含む名前をHumanoidの関節へ明示的に対応付け
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var names = source.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToHashSet();
            HumanDescription description = importer.humanDescription;
            description.human = HumanTrait.BoneName.Where(n => names.Contains(n.Replace(" ", "")))
                .Select(n => new HumanBone { humanName = n, boneName = n.Replace(" ", ""),
                    limit = new HumanLimit { useDefaultValues = true } }).ToArray();
            description.armStretch = .05f;
            description.legStretch = .05f;
            description.upperArmTwist = .5f;
            description.lowerArmTwist = .5f;
            description.upperLegTwist = .5f;
            description.lowerLegTwist = .5f;
            description.hasTranslationDoF = false;
            importer.humanDescription = description;
            importer.SaveAndReimport();

            // 元モデルのテクスチャをHDRP用共通マテリアルへ接続
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Protagonist.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Lit"));
                AssetDatabase.CreateAsset(material, Folder + "/Protagonist.mat");
            }
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Protagonist_BaseColor.png"));
            material.SetFloat("_Smoothness", .22f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);

            var root = new GameObject("ProtagonistBody");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), root.transform);
            model.name = "Model";
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterial = material;
                renderer.updateWhenOffscreen = true;
                renderer.quality = SkinQuality.Bone4;
                renderer.enabled = renderer.name != "HeadMesh";
            }
            var animator = model.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("Protagonist Humanoid avatar is invalid");
            animator.enabled = false;
            var view = root.AddComponent<PlayerBodyView>();
            var settings = new SerializedObject(view);
            settings.FindProperty("model").objectReferenceValue = model.transform;
            settings.FindProperty("headRenderer").objectReferenceValue = renderers.Single(r => r.name == "HeadMesh");
            settings.FindProperty("showHead").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
        }

        /// <summary>本番のプレイヤーを含むシーンへ主人公の表示を追加</summary>
        [MenuItem("Shinzui/Player/Install Protagonist In Release Scenes")]
        public static void InstallReleaseScenes()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first");
            Scene previous = SceneManager.GetActiveScene();
            foreach (string name in new[] { "SaveStage", "StageTemp" })
            {
                string path = "Assets/Shinzui/Scenes/ReleaseScenes/" + name + ".unity";
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                foreach (var player in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerView>(true)))
                    Install(player);
                EditorSceneManager.SaveScene(scene);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            SceneManager.SetActiveScene(previous);
        }

        /// <summary>カメラと移動コライダーを維持し、表示モデルと持ち手を設定</summary>
        /// <param name="player">接続先のプレイヤー</param>
        public static void Install(PlayerView player)
        {
            var flashlight = player.GetComponentInChildren<FlashlightView>(true);
            var controller = player.GetComponent<CharacterController>();
            if (flashlight == null || controller == null || player.MainCamera == null)
                throw new InvalidOperationException("Player camera, controller or flashlight is missing");

            // 従来の仮カプセルと頭の箱だけを隠し、当たり判定とカメラ追従先を残す
            var capsule = player.GetComponent<MeshRenderer>();
            if (capsule != null) capsule.enabled = false;
            Transform cameraTarget = player.transform.Find("PlayerHead");
            if (cameraTarget != null && cameraTarget.TryGetComponent(out MeshRenderer box)) box.enabled = false;

            var view = player.GetComponentInChildren<PlayerBodyView>(true);
            if (view == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), player.transform);
                view = instance.GetComponent<PlayerBodyView>();
            }
            float foot = controller.center.y - controller.height * .5f;
            Vector3 eye = player.transform.InverseTransformPoint(player.MainCamera.transform.position);
            view.transform.localPosition = new Vector3(0f, foot, eye.z - .06f);
            view.transform.localRotation = Quaternion.identity;
            view.transform.localScale = Vector3.one * ((eye.y - foot) / 1.65f);
            Transform model = view.transform.Find("Model");
            view.Configure(player, flashlight, model,
                model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "HeadMesh"));
            view.SetHeadVisible(false);

            // 発光位置を腕の届く範囲へ寄せ、消灯やストロボの既存処理を維持
            var settings = new SerializedObject(flashlight);
            settings.FindProperty("modelLightOffset").vector3Value = new Vector3(-.45f, -.32f, .52f);
            settings.ApplyModifiedPropertiesWithoutUndo();
            Transform flashlightModel = flashlight.HeldLightTransform.Find("FlashlightModel");
            flashlightModel.localScale = new Vector3(.6f, .6f, 1f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(flashlightModel);
            player.MainCamera.nearClipPlane = .03f;
            EditorUtility.SetDirty(view);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view.transform);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }
    }
}
