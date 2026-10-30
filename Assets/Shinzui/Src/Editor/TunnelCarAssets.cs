using UnityEditor;
using UnityEngine;

namespace Shinzui.Editor
{
    /// <summary>提供された車モデルを実寸と接地原点に揃えて保存する</summary>
    public static class TunnelCarAssets
    {
        /// <summary>2種類の車に衝突形状を付けてResourcesへ保存する</summary>
        [MenuItem("Shinzui/Tunnel Cars/Create Prefabs")]
        public static void CreatePrefabs()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Shinzui/Resources/Cars"))
                AssetDatabase.CreateFolder("Assets/Shinzui/Resources", "Cars");

            CreatePrefab("KeiCar", "KeiCar.glb");
            CreatePrefab("KeiHatchback", "Kei_Hatchback.glb");
            AssetDatabase.SaveAssets();
        }

        /// <summary>全長3.4mに合わせ、底面中央を原点にした車を保存する</summary>
        /// <param name="name">プレハブ名</param>
        /// <param name="file">取り込み済みGLBファイル名</param>
        private static void CreatePrefab(string name, string file)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/Models/Cars/" + file);
            if (model == null) throw new System.InvalidOperationException("Car GLB is not imported: " + file);
            var root = new GameObject(name);
            try
            {
                // 元の縦横比とマテリアルを保ち、異なる単位系をメートルに揃える
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.transform.SetParent(root.transform, false);
                Bounds bounds = MeasureBounds(visual);
                visual.transform.localScale *= 3.4f / Mathf.Max(bounds.size.x, bounds.size.z);
                bounds = MeasureBounds(visual);
                visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                bounds = MeasureBounds(visual);

                // 配置判定とプレイヤーの衝突に同じ占有寸法を使う
                int stageLayer = LayerMask.NameToLayer("Stage");
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    if (stageLayer >= 0) child.gameObject.layer = stageLayer;
                var collider = root.AddComponent<BoxCollider>();
                collider.center = bounds.center;
                collider.size = bounds.size;
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Shinzui/Resources/Cars/" + name + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>車の描画範囲を集計する</summary>
        /// <param name="visual">車モデル</param>
        /// <returns>ワールド座標の描画範囲</returns>
        private static Bounds MeasureBounds(GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
