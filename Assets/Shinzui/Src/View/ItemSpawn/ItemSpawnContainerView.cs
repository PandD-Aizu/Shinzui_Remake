using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shinzui.View.ItemSpawn
{
    /// <summary>
    /// アイテムスポーンのUnityシーン描画・プレハブ生成・インスタンス破棄を担うViewコンポーネント。
    /// 生成アイテムは generatedRoot 配下に専用の子GameObject（SpawnedItems）を作成して配置し、
    /// マップ本体や親階層（MapRoot）、WeightedSpawnSurface等の一切を破壊せず隔離・保護する。
    /// ドメイン層やプレゼンテーション層の存在を知らず、純粋なUnityオブジェクトの生成と保持のみを行う。
    /// </summary>
    [DisallowMultipleComponent]
    public class ItemSpawnContainerView : MonoBehaviour
    {
        private const string SpawnedItemsContainerName = "SpawnedItems";

        [Header("Spawn Hierarchy Root")]
        [Tooltip("アイテム群の親ルート（未設定時は自身）。配下に自動で 'SpawnedItems' が作成されます。")]
        [SerializeField] private Transform generatedRoot;

        [Header("Target Surfaces")]
        [SerializeField] private List<WeightedSpawnSurface> spawnSurfaces = new();

        private readonly List<GameObject> _spawnedInstances = new();
        private Transform _itemsContainer;

        public Transform GeneratedRoot => generatedRoot != null ? generatedRoot : transform;
        public Transform ItemsContainer => GetOrCreateItemsContainer();
        public IReadOnlyList<GameObject> SpawnedInstances => _spawnedInstances;

        public Transform GetOrCreateItemsContainer()
        {
            Transform root = GeneratedRoot;

            if (_itemsContainer != null && _itemsContainer.parent == root)
            {
                return _itemsContainer;
            }

            Transform existing = root.Find(SpawnedItemsContainerName);
            if (existing != null)
            {
                _itemsContainer = existing;
                return _itemsContainer;
            }

            var containerObj = new GameObject(SpawnedItemsContainerName);
            containerObj.transform.SetParent(root, false);
            containerObj.transform.localPosition = Vector3.zero;
            containerObj.transform.localRotation = Quaternion.identity;
            containerObj.transform.localScale = Vector3.one;

            _itemsContainer = containerObj.transform;
            return _itemsContainer;
        }

        public IReadOnlyList<WeightedSpawnSurface> GetSurfaces()
        {
            if (spawnSurfaces != null && spawnSurfaces.Count > 0)
            {
                // nullや無効コンポーネントを除外して返却
                var validList = new List<WeightedSpawnSurface>(spawnSurfaces.Count);
                for (int i = 0; i < spawnSurfaces.Count; i++)
                {
                    if (spawnSurfaces[i] != null && spawnSurfaces[i].gameObject.activeInHierarchy)
                    {
                        validList.Add(spawnSurfaces[i]);
                    }
                }
                if (validList.Count > 0) return validList;
            }

            // 自身および子階層から検索
            var localSurfaces = GetComponentsInChildren<WeightedSpawnSurface>(false);
            if (localSurfaces != null && localSurfaces.Length > 0)
            {
                return localSurfaces;
            }

            // シーン全体（MapRoot等）から検索して自動フォールバック
            var sceneSurfaces = FindObjectsByType<WeightedSpawnSurface>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return sceneSurfaces ?? Array.Empty<WeightedSpawnSurface>();
        }

        public void SetSurfaces(IEnumerable<WeightedSpawnSurface> surfaces)
        {
            spawnSurfaces.Clear();
            if (surfaces != null)
            {
                spawnSurfaces.AddRange(surfaces);
            }
        }

        public GameObject SpawnItem(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[ItemSpawnContainerView] Cannot spawn item: Prefab is null.");
                return null;
            }

            Transform parent = GetOrCreateItemsContainer();
            GameObject instance = Instantiate(prefab, position, rotation, parent);
            _spawnedInstances.Add(instance);
            return instance;
        }

        public void ClearAllSpawned()
        {
            // 1. 追跡中のインスタンスを破棄
            for (int i = _spawnedInstances.Count - 1; i >= 0; i--)
            {
                GameObject instance = _spawnedInstances[i];
                if (instance != null)
                {
                    DestroyGameObject(instance);
                }
            }
            _spawnedInstances.Clear();

            // 2. 専用子コンテナ（SpawnedItems）直下の残存オブジェクトのみを安全に破棄
            // （generatedRoot / MapRoot の他の子要素、トンネルジオメトリ、WeightedSpawnSurface等は一切破棄しない）
            Transform root = GeneratedRoot;
            Transform container = _itemsContainer != null ? _itemsContainer : root.Find(SpawnedItemsContainerName);

            if (container != null)
            {
                for (int i = container.childCount - 1; i >= 0; i--)
                {
                    Transform child = container.GetChild(i);
                    if (child != null)
                    {
                        DestroyGameObject(child.gameObject);
                    }
                }
            }
        }

        private static void DestroyGameObject(GameObject obj)
        {
            if (obj == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(obj);
                return;
            }
#endif
            Destroy(obj);
        }

        private void OnDestroy()
        {
            _spawnedInstances.Clear();
        }
    }
}
