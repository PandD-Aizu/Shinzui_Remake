using System.Linq;
using Shinzui.View;
using UnityEditor;
using UnityEngine;

namespace Shinzui.Editor
{
    /// <summary>提供されたGLBから実行用ロッカープレハブを作成</summary>
    public static class HidingLockerAssets
    {
        public const string PrefabPath = "Assets/Shinzui/Resources/HidingLocker.prefab";
        /// <summary>実モデル、ドア衝突、内部視点と出口を構成して保存</summary>
        [MenuItem("Shinzui/Hiding Locker/Create Prefab")]
        public static void CreatePrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/Models/HidingLocker/ShinShinzui_HidingLocker.glb");
            if (model == null) throw new System.InvalidOperationException("Locker GLB is not imported");
            if (!AssetDatabase.IsValidFolder("Assets/Shinzui/Resources")) AssetDatabase.CreateFolder("Assets/Shinzui", "Resources");
            var root = new GameObject("HidingLocker");
            root.SetActive(false);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.transform.SetParent(root.transform, false);
            var locker = root.AddComponent<HidingLockerView>();
            locker.DoorHinge = visual.GetComponentsInChildren<Transform>().First(t => t.name == "Door_Hinge");
            locker.OpenAngle = 105f;
            foreach (var mesh in visual.GetComponentsInChildren<MeshFilter>())
            {
                mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                int stageLayer = LayerMask.NameToLayer("Stage");
                if (stageLayer >= 0) mesh.gameObject.layer = stageLayer;
            }
            var obstacle = root.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, 1.04f, .03f); obstacle.size = new Vector3(.78f, 2.08f, .76f);
            obstacle.carving = true;
            locker.CameraAnchor = Anchor(root, "Camera Inside Slit", new Vector3(0f, 1.7f, .28f));
            locker.ExitPoint = Anchor(root, "Player Exit", new Vector3(0f, .05f, 1.35f));
            locker.StandPoint = Anchor(root, "Enemy Stand", new Vector3(0f, 0f, .95f));
            root.AddComponent<HidingSpotInteractable>();

            // 実メッシュの開口部を調べ、目線が金属板に埋まるのを避ける
            root.SetActive(true);
            Physics.SyncTransforms();
            var door = locker.DoorHinge.GetComponentInChildren<MeshCollider>();
            float bestY = 1.7f, bestRun = 0f, run = 0f;
            for (float y = 1.3f; y <= 1.95f; y += .002f)
            {
                bool blocked = door.Raycast(new Ray(new Vector3(0f, y, .28f), Vector3.forward), out _, .6f);
                run = blocked ? 0f : run + .002f;
                if (run > bestRun) { bestRun = run; bestY = y - run * .5f; }
            }
            locker.CameraAnchor.localPosition = new Vector3(0f, bestY, .28f);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
        }
        /// <summary>モデル座標系に基準点を作成</summary>
        /// <param name="root">親</param>
        /// <param name="name">用途</param>
        /// <param name="position">ローカル位置</param>
        /// <returns>基準点</returns>
        private static Transform Anchor(GameObject root, string name, Vector3 position)
        {
            var point = new GameObject(name).transform;
            point.SetParent(root.transform, false); point.localPosition = position;
            return point;
        }
    }
}
