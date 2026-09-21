using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Editor
{
    /// <summary>
    /// 蛍光管の発光材質と影付き光源を共通プレハブへ設定する
    /// </summary>
    public static class TunnelFluorescentLightAssets
    {
        private const string Folder = "Assets/Shinzui/Art/TunnelLighting";
        private const string PrefabPath = "Assets/Shinzui/Resources/TunnelFluorescentLight.prefab";

        /// <summary>
        /// 元モデルの形状を維持したまま蛍光管と器具本体の材質を分けて保存する
        /// </summary>
        [MenuItem("Shinzui/Tunnel/Rebuild Fluorescent Lighting")]
        public static void Rebuild()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Shinzui/Art", "TunnelLighting");
            }

            // 元メッシュを複製し、管の中央部分だけを発光用サブメッシュへ分離する
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/3DModels/Tun_Light_v2.fbx")
                .GetComponentInChildren<MeshFilter>().sharedMesh;
            var mesh = Object.Instantiate(source);
            mesh.name = "Fluorescent Housing And Tubes";
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            var housing = new List<int>();
            var tubes = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                bool luminous = IsTube(vertices[triangles[i]])
                    && IsTube(vertices[triangles[i + 1]]) && IsTube(vertices[triangles[i + 2]]);
                List<int> target = luminous ? tubes : housing;
                target.Add(triangles[i]);
                target.Add(triangles[i + 1]);
                target.Add(triangles[i + 2]);
            }

            mesh.subMeshCount = 2;
            mesh.SetTriangles(housing, 0);
            mesh.SetTriangles(tubes, 1);
            string meshPath = Folder + "/FluorescentMesh.asset";
            var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (savedMesh == null)
            {
                AssetDatabase.CreateAsset(mesh, meshPath);
                savedMesh = mesh;
            }
            else
            {
                EditorUtility.CopySerialized(mesh, savedMesh);
                Object.DestroyImmediate(mesh);
            }

            // 本体は塗装金属、管はやや暖かい白色のHDR発光とする
            Material body = GetMaterial("FluorescentHousing");
            body.SetColor("_BaseColor", new Color(0.55f, 0.57f, 0.55f));
            body.SetFloat("_Metallic", 0.35f);
            body.SetFloat("_Smoothness", 0.42f);
            Material tube = GetMaterial("FluorescentTube");
            tube.SetColor("_BaseColor", new Color(0.93f, 0.97f, 0.91f));
            tube.SetFloat("_Smoothness", 0.65f);
            tube.SetColor("_EmissionColor", new Color(0.94f, 0.97f, 1.0f) * 4.0f);
            tube.EnableKeyword("_EMISSION");
            tube.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            // 同一プレハブを更新して既存の配置と寸法を維持する
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                MeshFilter filter = root.GetComponentInChildren<MeshFilter>();
                filter.sharedMesh = savedMesh;
                filter.GetComponent<MeshRenderer>().sharedMaterials = new[] { body, tube };
                Transform lightTransform = root.transform.Find("Fluorescent Illumination");
                if (lightTransform == null)
                {
                    lightTransform = new GameObject("Fluorescent Illumination").transform;
                    lightTransform.SetParent(root.transform, false);
                }

                // 器具の外へ光源を出し、滑らかな配光と遮蔽物による影を付ける
                lightTransform.localPosition = new Vector3(0, -0.28f, 0);
                lightTransform.localRotation = Quaternion.Euler(90, 0, 0);
                Light light = lightTransform.GetComponent<Light>();
                if (light == null) light = lightTransform.gameObject.AddComponent<Light>();
                light.type = LightType.Spot;
                light.lightmapBakeType = LightmapBakeType.Realtime;
                light.color = Color.white;
                light.useColorTemperature = true;
                light.colorTemperature = 4600;
                light.intensity = 1.4f;
                light.range = 22;
                light.spotAngle = 170;
                light.innerSpotAngle = 135;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 1;
                light.shadowBias = 0.025f;
                light.shadowNormalBias = 0.12f;
                light.shadowNearPlane = 0.05f;
                light.renderMode = LightRenderMode.Auto;

                // 多数の器具でシャドウアトラスを圧迫しないよう低解像度の影を共有する
                UniversalAdditionalLightData data = light.GetComponent<UniversalAdditionalLightData>();
                if (data == null) data = light.gameObject.AddComponent<UniversalAdditionalLightData>();
                data.usePipelineSettings = false;
                var settings = new SerializedObject(data);
                settings.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = 0;
                settings.ApplyModifiedPropertiesWithoutUndo();

                // 近接する二光源を中央の一光源へまとめ、総光量を保ちつつ影計算を半減する
                Transform second = root.transform.Find("Fluorescent Illumination End");
                if (second != null) Object.DestroyImmediate(second.gameObject);

                // 近傍の天井への弱い反射光を補い、器具だけが黒い天井に浮くのを防ぐ
                Transform bounceTransform = root.transform.Find("Ceiling Bounce");
                if (bounceTransform == null)
                {
                    bounceTransform = new GameObject("Ceiling Bounce").transform;
                    bounceTransform.SetParent(root.transform, false);
                }

                bounceTransform.localPosition = new Vector3(0, -0.5f, 0);
                Light bounce = bounceTransform.GetComponent<Light>();
                if (bounce == null) bounce = bounceTransform.gameObject.AddComponent<Light>();
                bounce.type = LightType.Point;
                bounce.lightmapBakeType = LightmapBakeType.Realtime;
                bounce.color = new Color(0.92f, 0.95f, 1.0f);
                bounce.intensity = 0.025f;
                bounce.range = 2.2f;
                bounce.shadows = LightShadows.None;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            EditorUtility.SetDirty(savedMesh);
            EditorUtility.SetDirty(body);
            EditorUtility.SetDirty(tube);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 提供モデルの蛍光管中央に含まれる頂点か判定する
        /// </summary>
        /// <param name="vertex">元メッシュのローカル座標</param>
        /// <returns>発光するガラス管部分ならtrue</returns>
        private static bool IsTube(Vector3 vertex)
        {
            return Mathf.Abs(vertex.x) < 1.50f && vertex.y > 0.86f && vertex.y < 1.05f
                && Mathf.Abs(vertex.z) > 0.50f && Mathf.Abs(vertex.z) < 0.85f;
        }

        /// <summary>
        /// 再生成時も参照を維持するURP Litマテリアルを取得する
        /// </summary>
        /// <param name="name">材質の名前</param>
        /// <returns>保存済みまたは新規のマテリアル</returns>
        private static Material GetMaterial(string name)
        {
            string path = Folder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }
    }
}
