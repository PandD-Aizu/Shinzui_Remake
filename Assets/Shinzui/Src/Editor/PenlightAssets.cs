using Shinzui.Infrastructure.ItemSpawn;
using Shinzui.View.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Shinzui.Infrastructure.Lighting;

namespace Shinzui.Editor
{
    /// <summary>支給モデルから取得用と赤色点灯済みの設置用アセットを作成する</summary>
    public static class PenlightAssets
    {
        /// <summary>発光材質、プレハブ、抽選設定を更新する</summary>
        [MenuItem("Shinzui/Items/Rebuild Penlight")]
        public static void Rebuild()
        {
            const string folder = "Assets/Shinzui/Models/Penlight/";
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "PenlightRed.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Lit"));
                AssetDatabase.CreateAsset(material, folder + "PenlightRed.mat");
            }

            /* 小型ケミカルライトの発光をHDRPのニト値で設定する */
            material.SetColor("_BaseColor", new Color(.7f, .005f, .005f));
            material.SetColor("_EmissiveColor", new Color(1.2f, .005f, .005f));
            material.SetFloat("_UseEmissiveIntensity", 0);
            HDMaterial.ValidateMaterial(material);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);

            var root = new GameObject("PlacedPenlight");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Work_Light_Stick.glb");
            var model = Object.Instantiate(source, root.transform);
            model.name = "Model";

            // 発光面を上に向け、全メッシュの下端を床の高さへ合わせる
            model.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            foreach (var renderer in renderers)
                if (renderer.name == "Diffuser_EmissionSurface") renderer.sharedMaterial = material;

            var lamp = new GameObject("Red Light").AddComponent<Light>();
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = new Vector3(0, .07f, 0);
            lamp.type = LightType.Point;
            lamp.color = Color.red;
            HdrpLightUtility.SetIntensity(lamp, 8f, LightUnit.Lumen);
            lamp.range = 2f;
            lamp.shadows = LightShadows.Soft;
            lamp.shadowBias = .01f;
            lamp.shadowNormalBias = .02f;
            lamp.lightmapBakeType = LightmapBakeType.Realtime;
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Shinzui/Resources/PlacedPenlight.prefab");

            // 未使用品は消灯し、既存の長押し取得経路に接続する
            Object.DestroyImmediate(lamp.gameObject);
            foreach (var renderer in renderers)
                if (renderer.name == "Diffuser_EmissionSurface")
                    renderer.sharedMaterial = source.GetComponentsInChildren<Renderer>()[0].sharedMaterial;
            root.name = "Penlight";
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, bounds.size.y / 2, 0);
            collider.size = bounds.size;
            collider.isTrigger = true;
            var interactable = new SerializedObject(root.AddComponent<InteractableComponent>());
            interactable.FindProperty("displayName").stringValue = "[E] ペンライトを拾う";
            interactable.FindProperty("interactableId").stringValue = "ItemTest_penlight";
            interactable.FindProperty("isOneTime").boolValue = true;
            interactable.FindProperty("destroyOnInteract").boolValue = true;
            interactable.FindProperty("holdToPickup").boolValue = true;
            interactable.FindProperty("interactMessage").stringValue = "";
            interactable.ApplyModifiedPropertiesWithoutUndo();
            var pickup = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Shinzui/Prefabs/Penlight.prefab");
            Object.DestroyImmediate(root);

            // 電池と同程度の重みで棚などの生成面に出現させる
            const string definitionPath = "Assets/Shinzui/Resources/PenlightItemSpawnDefinition.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ItemSpawnDefinitionSO>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemSpawnDefinitionSO>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }
            var settings = new SerializedObject(definition);
            settings.FindProperty("itemId").stringValue = "penlight";
            settings.FindProperty("displayName").stringValue = "ペンライト";
            settings.FindProperty("category").intValue = 2;
            settings.FindProperty("prefab").objectReferenceValue = pickup;
            settings.FindProperty("baseWeight").floatValue = .5f;
            settings.FindProperty("maxCount").intValue = 10;
            settings.FindProperty("collisionRadius").floatValue = .17f;
            settings.FindProperty("surfaceOffset").floatValue = .003f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
}
