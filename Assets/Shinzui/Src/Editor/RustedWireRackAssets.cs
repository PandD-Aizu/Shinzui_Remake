using Shinzui.View.ItemSpawn;
using UnityEditor;
using UnityEngine;

namespace Shinzui.Editor
{
    /// <summary>ラックの各棚にアイテム生成面と衝突形状を設定する</summary>
    public static class RustedWireRackAssets
    {
        private const string PrefabPath = "Assets/Shinzui/Resources/RustedWireRack.prefab";
        private const string MeshPath = "Assets/Shinzui/Models/RustedWireRack/ShelfSpawnSurface.asset";

        /// <summary>実モデルの棚板の高さを使って4段の生成面を保存する</summary>
        [MenuItem("Shinzui/Rusted Wire Rack/Configure Shelves")]
        public static void ConfigureShelves()
        {
            // 描画しない上向きの平面を共有し、棚の縁と支柱から余白を取る
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Rack Shelf Spawn Surface" };
                mesh.vertices = new[] { new Vector3(-.45f, 0, -.1f), new Vector3(-.45f, 0, .1f),
                    new Vector3(.45f, 0, .1f), new Vector3(.45f, 0, -.1f) };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // 全体の箱は配置寸法として残し、棚の空間を塞がないよう無効にする
                root.GetComponent<BoxCollider>().enabled = false;
                var previous = root.transform.Find("Shelf Spawn Surfaces");
                if (previous != null) Object.DestroyImmediate(previous.gameObject);
                var holder = new GameObject("Shelf Spawn Surfaces").transform;
                holder.SetParent(root.transform, false);

                foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    if (!filter.name.StartsWith("Shelf_")) continue;
                    var bounds = filter.sharedMesh.bounds;
                    var shelf = new GameObject(filter.name.Substring(0, 8) + " Spawn Surface");
                    shelf.transform.SetParent(holder, false);
                    shelf.transform.position = filter.transform.TransformPoint(new Vector3(0, bounds.max.y, 0));
                    shelf.layer = root.layer;

                    // ワイヤー間の隙間でアイテムが落ちないよう棚板を薄い箱で支える
                    var collider = shelf.AddComponent<BoxCollider>();
                    collider.size = new Vector3(bounds.size.x, bounds.size.y, bounds.size.z);
                    collider.center = new Vector3(0, -bounds.size.y * .5f, 0);
                    var surface = shelf.AddComponent<WeightedSpawnSurface>();
                    surface.InitializeFromMesh(mesh);
                    surface.InitializeWeightMap(2, 1f);
                }

                // 四隅の支柱だけを塞ぎ、棚の正面からアイテムへ手が届く状態にする
                foreach (int x in new[] { -1, 1 })
                foreach (int z in new[] { -1, 1 })
                {
                    var post = new GameObject($"Rack Post {x} {z}");
                    post.transform.SetParent(holder, false);
                    post.transform.localPosition = new Vector3(x * .575f, .9f, z * .2f);
                    post.layer = root.layer;
                    post.AddComponent<BoxCollider>().size = new Vector3(.05f, 1.8f, .05f);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
