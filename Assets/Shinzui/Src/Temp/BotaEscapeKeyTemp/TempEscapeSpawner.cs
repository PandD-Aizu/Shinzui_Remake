using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Shinzui.DI.GenerateTunnel;
using Random = UnityEngine.Random;

namespace Shinzui.Temp
{
    /// <summary>
    /// 生成されたマップに脱出用の鍵と出口を置く。階が作り直されるたびに置き直す
    /// 鍵はスタートから離れたところ、出口はスタートのすぐ近く
    /// 鍵を持ってる間は鍵を置かない。シーンに出口が置いてあれば出口は置かない
    /// シーンに置かなくても勝手に起動する
    /// </summary>
    [DefaultExecutionOrder(500)]
    public sealed class TempEscapeSpawner : MonoBehaviour
    {
        private const string GeometryRootName = "Tunnel Map Geometry";
        private static readonly Color KeyColor = new Color(1.0f, 0.8f, 0.2f);
        private static readonly Color ExitColor = new Color(0.45f, 0.12f, 0.1f);

        [Tooltip("TempEscapeKeyItemが付いたプレハブ。空なら仮の黄色い板を出す")]
        [SerializeField] private TempEscapeKeyItem keyPrefab;
        [Tooltip("TempEscapeExitが付いたプレハブ。空なら仮の扉を出す")]
        [SerializeField] private TempEscapeExit exitPrefab;
        [Tooltip("鍵をスタート地点から最低これだけ離す(m)")]
        [SerializeField] private float minDistanceFromStart = 30.0f;
        [Tooltip("出口を置くスタート地点からの距離(m)")]
        [SerializeField] private float exitDistanceFromStart = 5.0f;
        [SerializeField] private int maxPlacementAttempts = 30;
        [SerializeField] private float groundClearance = 0.3f;
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
        /// トンネル生成シーンでスポナーが無ければ作る
        /// </summary>
        private static void TryAutoBootstrap()
        {
            if (FindAnyObjectByType<TempEscapeSpawner>() != null || FindAnyObjectByType<GenerateTunnelLifetimeScope>() == null)
            {
                return;
            }
            new GameObject("TempEscapeSpawner (Auto)").AddComponent<TempEscapeSpawner>();
        }

        private void Start()
        {
            StartCoroutine(WatchAndSpawnLoop());
        }

        /// <summary>
        /// マップができたら置き、作り直されたら消してまた置く
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
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                // MapRootの下に置くとトンネル音響の収集で例外が出るので、どっちも自分の下に置く
                if (geometry.GetEntityId() != _lastGeometryEntityId)
                {
                    _lastGeometryEntityId = geometry.GetEntityId();
                    SpawnKey();
                    SpawnExit();
                }

                // 次の階で作り直されたら、置いたものを消す
                while (geometry != null)
                {
                    yield return null;
                }
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    Destroy(transform.GetChild(i).gameObject);
                }
            }
        }

        private static Transform FindGeometryRoot()
        {
            GameObject mapRoot = GameObject.Find("MapRoot");
            return mapRoot != null ? mapRoot.transform.Find(GeometryRootName) : null;
        }

        private static bool HasBakedNavMesh()
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            return tri.vertices.Length > 0 && tri.indices.Length >= 3;
        }

        /// <summary>
        /// スタート地点から離れたNavMesh上のランダムな位置に鍵を置く
        /// </summary>
        private void SpawnKey()
        {
            if (TempEscapeKey.HasKey())
            {
                return;
            }

            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            float[] areas = BuildCumulativeAreas(tri);

            // 近すぎたら引き直す。ずっと近いなら最後のでいい
            Vector3 start = GetStartPosition();
            Vector3 point = start;
            for (int i = 0; i < maxPlacementAttempts; i++)
            {
                point = SampleRandomPointOnNavMesh(tri, areas);
                if ((point - start).sqrMagnitude >= minDistanceFromStart * minDistanceFromStart)
                {
                    break;
                }
            }

            point += Vector3.up * groundClearance;
            if (keyPrefab != null)
            {
                Instantiate(keyPrefab, point, Quaternion.identity, transform);
            }
            else
            {
                CreatePlaceholderKey(point);
            }

            Debug.Log($"[TempEscapeSpawner] 鍵を{point}に置いた", this);
        }

        /// <summary>
        /// スタート地点のすぐ近くに、スタートの方を向けて出口を置く
        /// </summary>
        private void SpawnExit()
        {
            if (FindAnyObjectByType<TempEscapeExit>() != null)
            {
                return;
            }

            if (!NavMesh.SamplePosition(GetStartPosition(), out NavMeshHit start, 5.0f, NavMesh.AllAreas))
            {
                return;
            }

            // スタートからまっすぐ歩いて行ける方向を探す。壁にぶつかる方向だと壁の向こうに出てしまう
            for (int i = 0; i < maxPlacementAttempts; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                Vector3 target = start.position + new Vector3(dir.x, 0.0f, dir.y) * exitDistanceFromStart;
                if (NavMesh.Raycast(start.position, target, out NavMeshHit hit, NavMesh.AllAreas))
                {
                    continue;
                }

                Quaternion facing = Quaternion.LookRotation(new Vector3(-dir.x, 0.0f, -dir.y));
                if (exitPrefab != null)
                {
                    Instantiate(exitPrefab, hit.position, facing, transform);
                }
                else
                {
                    CreatePlaceholderExit(hit.position, facing);
                }

                Debug.Log($"[TempEscapeSpawner] 出口を{hit.position}に置いた", this);
                return;
            }
        }

        // 階を作り直した直後はプレイヤーがまだ前の階にいるので、スタートトンネルの位置を使う
        private static Vector3 GetStartPosition()
        {
            GameObject mapRoot = GameObject.Find("MapRoot");
            if (mapRoot != null)
            {
                foreach (Transform t in mapRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("Player Start Tunnel", StringComparison.Ordinal))
                    {
                        return t.position;
                    }
                }
            }
            return Vector3.zero;
        }

        /// <summary>
        /// keyPrefabが無いときの仮の鍵（光る黄色い板）
        /// </summary>
        private void CreatePlaceholderKey(Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "TempEscapeKey (Placeholder)";
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0.0f, Random.Range(0.0f, 360.0f), 0.0f));
            go.transform.localScale = new Vector3(0.15f, 0.05f, 0.4f);

            // 暗いと見つからないので光らせる
            Material material = go.GetComponent<Renderer>().material;
            material.color = KeyColor;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", KeyColor * 2.0f);

            // 小さくて狙いにくいので判定だけ大きくする。ぶつからないようにトリガーにしておく
            var box = go.GetComponent<BoxCollider>();
            box.size = new Vector3(4.0f, 10.0f, 1.5f);
            box.isTrigger = true;

            go.AddComponent<TempEscapeKeyItem>();
        }

        /// <summary>
        /// exitPrefabが無いときの仮の出口（赤茶色の扉）
        /// </summary>
        private void CreatePlaceholderExit(Vector3 position, Quaternion rotation)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "TempEscapeExit (Placeholder)";
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(position + Vector3.up * 1.2f, rotation);
            go.transform.localScale = new Vector3(1.4f, 2.4f, 0.2f);
            go.GetComponent<Renderer>().material.color = ExitColor;
            go.AddComponent<TempEscapeExit>();
        }

        // 面積で重み付けしないと細い通路に偏るので、三角形の面積を足していった表を作っておく
        private static float[] BuildCumulativeAreas(NavMeshTriangulation tri)
        {
            float[] areas = new float[tri.indices.Length / 3];
            float total = 0.0f;
            for (int i = 0; i < areas.Length; i++)
            {
                Vector3 a = tri.vertices[tri.indices[i * 3]];
                Vector3 b = tri.vertices[tri.indices[i * 3 + 1]];
                Vector3 c = tri.vertices[tri.indices[i * 3 + 2]];
                total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                areas[i] = total;
            }
            return areas;
        }

        private static Vector3 SampleRandomPointOnNavMesh(NavMeshTriangulation tri, float[] areas)
        {
            int index = Array.BinarySearch(areas, Random.value * areas[areas.Length - 1]);
            if (index < 0)
            {
                index = Mathf.Min(~index, areas.Length - 1);
            }

            Vector3 a = tri.vertices[tri.indices[index * 3]];
            Vector3 b = tri.vertices[tri.indices[index * 3 + 1]];
            Vector3 c = tri.vertices[tri.indices[index * 3 + 2]];

            // sqrt取らないと頂点寄りになる
            float r1 = Mathf.Sqrt(Random.value);
            float r2 = Random.value;
            Vector3 point = (1.0f - r1) * a + (r1 * (1.0f - r2)) * b + (r1 * r2) * c;

            return NavMesh.SamplePosition(point, out NavMeshHit hit, 1.0f, NavMesh.AllAreas) ? hit.position : point;
        }
    }
}
