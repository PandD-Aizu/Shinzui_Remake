using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Shinzui.DI.GenerateTunnel;

namespace Shinzui.Temp
{
    /// <summary>
    /// 生成されたマップにドキュメントを置く。NavMesh上にしか置かないので壁には埋まらない
    /// documentPrefabsの中から拾ってないものをランダムに選んで置く。階が作り直されるたびに置き直す
    /// シーンに置かなくても勝手に起動する（documentPrefabsを設定するにはシーンに置いてInspectorで指定する）
    /// </summary>
    [DefaultExecutionOrder(500)]
    public sealed class TempDocumentSpawner : MonoBehaviour
    {
        private const string GeometryRootName = "Tunnel Map Geometry";

        [Tooltip("置く候補のドキュメント（TempDocumentItemが付いたプレハブ）")]
        [SerializeField] private TempDocumentItem[] documentPrefabs;

        [Header("出現数（min〜maxでランダム）")]
        [SerializeField, Min(0)] private int spawnCountMin = 2;
        [SerializeField, Min(0)] private int spawnCountMax = 4;

        [Header("配置")]
        [Tooltip("ドキュメント同士の最低距離(m)")]
        [SerializeField] private float minSeparation = 8.0f;
        [Tooltip("1個あたりの抽選し直し回数")]
        [SerializeField] private int maxPlacementAttemptsPerItem = 30;
        [Tooltip("床からの高さ(m)")]
        [SerializeField] private float groundClearance = 0.5f;

        [Header("完了待ち")]
        [Tooltip("マップ生成・NavMeshベイク待ちの最大秒数")]
        [SerializeField] private float maxWaitSeconds = 15.0f;

        private EntityId _lastGeometryEntityId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapInitialScene()
        {
            TryAutoBootstrap();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryAutoBootstrap();
        }

        /// <summary>
        /// トンネル生成シーンで、スポナーが置かれてなければ自分を作る
        /// </summary>
        private static void TryAutoBootstrap()
        {
            if (FindAnyObjectByType<TempDocumentSpawner>() != null)
            {
                return;
            }

            if (FindAnyObjectByType<GenerateTunnelLifetimeScope>() == null)
            {
                return;
            }

            var go = new GameObject("TempDocumentSpawner (Auto)");
            go.AddComponent<TempDocumentSpawner>();
        }

        private void Start()
        {
            StartCoroutine(WatchAndSpawnLoop());
        }

        /// <summary>
        /// マップができるのを待って置き、作り直されたら消してまた置く、をずっと繰り返す
        /// </summary>
        private IEnumerator WatchAndSpawnLoop()
        {
            while (true)
            {
                Transform geometry = null;
                float deadline = Time.realtimeSinceStartup + maxWaitSeconds;

                while (Time.realtimeSinceStartup < deadline)
                {
                    geometry = FindGeometryRoot();
                    if (geometry != null && HasBakedNavMesh())
                    {
                        break;
                    }
                    yield return null;
                }

                if (geometry == null || !HasBakedNavMesh())
                {
                    // まだ生成されてない
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                if (geometry.GetEntityId() != _lastGeometryEntityId)
                {
                    _lastGeometryEntityId = geometry.GetEntityId();
                    // MapRoot配下に置くとトンネル音響の収集がコライダーで例外を出すので自分の下に置く
                    SpawnDocuments(transform);
                }

                // 次の階層で作り直されるまで待つ
                while (geometry != null)
                {
                    yield return null;
                }

                ClearSpawned();
            }
        }

        private void ClearSpawned()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private static Transform FindGeometryRoot()
        {
            GameObject mapRoot = GameObject.Find("MapRoot");
            if (mapRoot == null)
            {
                return null;
            }
            return mapRoot.transform.Find(GeometryRootName);
        }

        private static bool HasBakedNavMesh()
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            return tri.vertices != null && tri.vertices.Length > 0 && tri.indices != null && tri.indices.Length >= 3;
        }

        /// <summary>
        /// 拾ってないドキュメントからmin〜max個選んで、NavMesh上のランダムな位置にminSeparation以上離して置く
        /// </summary>
        private void SpawnDocuments(Transform parent)
        {
            var candidates = new List<TempDocumentItem>();
            if (documentPrefabs != null)
            {
                foreach (TempDocumentItem prefab in documentPrefabs)
                {
                    if (prefab != null && !TempDocumentCollector.IsCollected(prefab.DocumentId)) candidates.Add(prefab);
                }
            }
            if (candidates.Count == 0)
            {
                Debug.LogWarning("[TempDocumentSpawner] 置けるドキュメントが無いのでスキップしました（documentPrefabs未設定か全部拾い済み）。", this);
                return;
            }

            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            if (tri.vertices.Length == 0 || tri.indices.Length < 3)
            {
                Debug.LogWarning("[TempDocumentSpawner] NavMeshが空のため配置をスキップしました。", this);
                return;
            }

            // 面積で重み付けしないと細い通路に偏る
            int triangleCount = tri.indices.Length / 3;
            float[] cumulativeAreas = new float[triangleCount];
            float totalArea = 0.0f;
            for (int i = 0; i < triangleCount; i++)
            {
                Vector3 a = tri.vertices[tri.indices[i * 3]];
                Vector3 b = tri.vertices[tri.indices[i * 3 + 1]];
                Vector3 c = tri.vertices[tri.indices[i * 3 + 2]];
                totalArea += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                cumulativeAreas[i] = totalArea;
            }

            if (totalArea <= 0.0f)
            {
                return;
            }

            int count = Mathf.Min(Random.Range(spawnCountMin, spawnCountMax + 1), candidates.Count);
            var placedPoints = new List<Vector3>(count);

            for (int i = 0; i < count; i++)
            {
                Vector3? point = null;

                for (int attempt = 0; attempt < maxPlacementAttemptsPerItem; attempt++)
                {
                    Vector3 candidate = SampleRandomPointOnNavMesh(tri, cumulativeAreas, totalArea);

                    bool tooClose = false;
                    for (int p = 0; p < placedPoints.Count; p++)
                    {
                        if ((placedPoints[p] - candidate).sqrMagnitude < minSeparation * minSeparation)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        point = candidate;
                        break;
                    }
                }

                // 狭いマップだと見つからないので、そのときは近くても置く
                if (!point.HasValue)
                {
                    point = SampleRandomPointOnNavMesh(tri, cumulativeAreas, totalArea);
                }

                // 同じドキュメントを2回置かないように、選んだら候補から外す
                int pick = Random.Range(0, candidates.Count);
                TempDocumentItem prefab = candidates[pick];
                candidates.RemoveAt(pick);

                placedPoints.Add(point.Value);
                Instantiate(prefab, point.Value + Vector3.up * groundClearance, Quaternion.identity, parent);
            }

            Debug.Log($"[TempDocumentSpawner] ドキュメントを{placedPoints.Count}箇所に配置しました。", this);
        }

        /// <summary>
        /// NavMeshの三角形を面積の大きさで重み付けして1つ選び、その中のランダムな点を返す
        /// cumulativeAreasは三角形の面積を順に足していった値
        /// </summary>
        private static Vector3 SampleRandomPointOnNavMesh(NavMeshTriangulation tri, float[] cumulativeAreas, float totalArea)
        {
            float target = Random.value * totalArea;
            int lo = 0;
            int hi = cumulativeAreas.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (cumulativeAreas[mid] < target)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            Vector3 a = tri.vertices[tri.indices[lo * 3]];
            Vector3 b = tri.vertices[tri.indices[lo * 3 + 1]];
            Vector3 c = tri.vertices[tri.indices[lo * 3 + 2]];

            // sqrt取らないと頂点寄りになる
            float r1 = Mathf.Sqrt(Random.value);
            float r2 = Random.value;
            Vector3 point = (1.0f - r1) * a + (r1 * (1.0f - r2)) * b + (r1 * r2) * c;

            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
            {
                return hit.position;
            }
            return point;
        }
    }
}
