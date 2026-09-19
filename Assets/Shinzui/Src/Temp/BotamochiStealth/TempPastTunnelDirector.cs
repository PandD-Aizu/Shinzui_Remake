using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Shinzui.DI.GenerateTunnel;
using Shinzui.Temp;
using Shinzui.View;
using Shinzui.View.GenerateTunnel;

namespace Shinzui.Temp.BotamochiStealth
{
    /// <summary>
    /// 生成された現代トンネルの中からSpecial Tunnel（過去トンネル）を特定し、
    /// その中へ彼女（会話ゴール）とステルス敵を実行時配置する仮実装
    /// 彼女は最奥、敵は入口寄りに置き、プレイヤーが敵をやり過ごして奥の彼女へ向かう構図にする
    /// 会話完了時にはシードを更新して現代トンネルをその場で再生成し、次の階層へ進める
    /// Tempアセンブリ内で完結する
    /// </summary>
    [DefaultExecutionOrder(500)]
    public sealed class TempPastTunnelDirector : MonoBehaviour
    {
        public static TempPastTunnelDirector Instance { get; private set; }

        [Header("参照（未設定なら実行時に自動探索）")]
        [SerializeField] private TempGirlfriendDialogue girlfriend;
        [SerializeField] private GenerateTunnelTestBootstrap generationSettings;
        [Tooltip("過去トンネル用のステルス敵。生成後にSpecial Tunnelの入口寄りへ配置する")]
        [SerializeField] private TempPatrolChaseEnemy stealthEnemy;
        [Tooltip("敵の巡回ウェイポイント（Special Tunnelの幅方向に置き直す）")]
        [SerializeField] private Transform[] enemyWaypoints;

        [Header("配置")]
        [Tooltip("Special Tunnelの端から内側へ寄せる距離(m)")]
        [SerializeField] private float depthInset = 14.0f;
        [Tooltip("床スナップ用の上方レイキャスト高さ(m)")]
        [SerializeField] private float floorRaycastHeight = 6.0f;
        [Tooltip("NavMeshへスナップする際の許容半径(m)")]
        [SerializeField] private float navSampleRadius = 8.0f;
        [Tooltip("床から彼女のピボットを持ち上げる量(m)、モデルの高さの半分程度")]
        [SerializeField] private float groundClearance = 0.9f;
        [Tooltip("次の階層でプレイヤーを床から持ち上げる量(m)")]
        [SerializeField] private float playerSpawnClearance = 1.2f;
        [Tooltip("生成完了を待つ最大時間(秒)")]
        [SerializeField] private float maxWaitSeconds = 15.0f;

        // プレイセッション内でのみ保持（プレイ停止＝ドメインリロードでリセット）
        private static int s_floorNumber = 1;
        private static int? s_pendingSeed;

        private bool _placed;
        private bool _advancing;
        private Vector3? _playerSpawn;

        public int FloorNumber => s_floorNumber;

        private void Awake()
        {
            Instance = this;

            // 前の階で決めたシードを生成設定へ適用する
            if (s_pendingSeed.HasValue)
            {
                GenerateTunnelTestBootstrap.RuntimeSeedOverride = s_pendingSeed.Value;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (girlfriend == null)
            {
                girlfriend = FindAnyObjectByType<TempGirlfriendDialogue>();
            }
            if (generationSettings == null)
            {
                generationSettings = FindAnyObjectByType<GenerateTunnelTestBootstrap>();
            }
            if (stealthEnemy == null)
            {
                stealthEnemy = FindAnyObjectByType<TempPatrolChaseEnemy>();
            }

            if (girlfriend != null)
            {
                girlfriend.ConfigureReturn(this);
            }

            // 次の階層でプレイヤーを戻す座標として、シーンに配置された初期位置を覚えておく
            var player = FindAnyObjectByType<PlayerView>();
            if (player != null)
            {
                _playerSpawn = player.transform.position;
            }

            // 生成前のスタート地点でNavMesh未ベイクのままAgentが起動して警告が出るのを防ぐ
            if (stealthEnemy != null)
            {
                stealthEnemy.gameObject.SetActive(false);
            }

            StartCoroutine(PlaceOccupantsWhenReady());
        }

        private IEnumerator PlaceOccupantsWhenReady()
        {
            float deadline = Time.realtimeSinceStartup + maxWaitSeconds;
            Transform special = null;

            while (Time.realtimeSinceStartup < deadline)
            {
                special = FindSpecialTunnel();
                if (special != null && HasBakedNavMesh())
                {
                    break;
                }
                yield return null;
            }

            if (special == null)
            {
                Debug.LogWarning("[TempPastTunnelDirector] Special Tunnel が生成マップ内に見つかりませんでした。配置をスキップします（生成が無効なシーンでは正常）。", this);
                if (stealthEnemy != null)
                {
                    stealthEnemy.gameObject.SetActive(true);
                }
                yield break;
            }

            PlaceOccupants(special);
        }

        private static Transform FindSpecialTunnel()
        {
            GameObject mapRoot = GameObject.Find("MapRoot");
            if (mapRoot == null)
            {
                return null;
            }

            foreach (Transform t in mapRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Special Tunnel", StringComparison.Ordinal))
                {
                    return t;
                }
            }
            return null;
        }

        private static bool HasBakedNavMesh()
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            return tri.vertices != null && tri.vertices.Length > 0;
        }

        private void PlaceOccupants(Transform special)
        {
            if (_placed)
            {
                return;
            }

            Bounds bounds = CalculateWorldBounds(special);
            Vector3 center = bounds.center;

            // トンネルの長軸（X or Z）を判定 開放出口の通路がバウンディングボックスを
            // 実寸の2〜3倍に膨らませるため、端そのものではなく中心から少しずらした点を狙う
            bool longAxisIsZ = bounds.size.z >= bounds.size.x;
            float halfLength = 0.5f * (longAxisIsZ ? bounds.size.z : bounds.size.x);
            Vector3 axis = longAxisIsZ ? Vector3.forward : Vector3.right;
            Vector3 widthAxis = longAxisIsZ ? Vector3.right : Vector3.forward;

            // プレイヤー開始地点から遠い側を「奥」（彼女）、近い側を「入口」（敵）とする
            Vector3 playerStart = GetPlayerStartPosition();
            float deepSign = Vector3.Dot(center - playerStart, axis) >= 0.0f ? 1.0f : -1.0f;

            // 彼女：奥の歩行可能点
            Vector3 girlPoint = FindWalkablePointAlongAxis(center, axis, deepSign, Mathf.Max(0.0f, halfLength - depthInset));
            if (girlfriend != null)
            {
                GameObject go = girlfriend.gameObject;
                go.transform.SetParent(null, true);
                go.transform.position = girlPoint + Vector3.up * groundClearance;
                Vector3 look = center - girlPoint;
                look.y = 0.0f;
                if (look.sqrMagnitude > 0.001f)
                {
                    go.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
                }
                go.SetActive(true);
                girlfriend.ConfigureReturn(this);
                girlfriend.ResetForNextFloor();
            }

            // 敵：入口寄りの歩行可能点、巡回ウェイポイントは幅方向に振る
            if (stealthEnemy != null)
            {
                Vector3 enemyPoint = FindWalkablePointAlongAxis(center, axis, -deepSign, Mathf.Max(0.0f, halfLength - depthInset));

                if (enemyWaypoints != null)
                {
                    for (int i = 0; i < enemyWaypoints.Length; i++)
                    {
                        if (enemyWaypoints[i] == null)
                        {
                            continue;
                        }
                        float side = enemyWaypoints.Length > 1 ? Mathf.Lerp(-1.0f, 1.0f, i / (float)(enemyWaypoints.Length - 1)) : 0.0f;
                        Vector3 wp = enemyPoint + widthAxis * (side * 3.0f);
                        if (NavMesh.SamplePosition(wp, out NavMeshHit wpHit, 3.0f, NavMesh.AllAreas))
                        {
                            wp = wpHit.position;
                        }
                        enemyWaypoints[i].position = wp + Vector3.up * 0.1f;
                    }
                }

                GameObject eo = stealthEnemy.gameObject;
                eo.transform.position = enemyPoint + Vector3.up * 0.1f;
                eo.SetActive(true);
                // 前の階の会話で一時停止したままになっていることがあるので巡回を再開させる
                stealthEnemy.ResumeEnemy();
            }

            _placed = true;

            Debug.Log($"[TempPastTunnelDirector] 過去トンネル '{special.name}' に配置: 彼女={girlPoint} 敵={(stealthEnemy != null ? stealthEnemy.transform.position.ToString() : "なし")} (floor {s_floorNumber})。", this);
        }

        /// <summary>
        /// centerからaxis*sign方向へ1m刻みで探索し、NavMesh上に載る一番奥の点を返す
        /// 見つからなければStageレイヤーの床レイキャスト、それも失敗なら見込み座標を返す
        /// </summary>
        private Vector3 FindWalkablePointAlongAxis(Vector3 center, Vector3 axis, float sign, float maxDistance)
        {
            for (float d = maxDistance; d >= 0.0f; d -= 1.0f)
            {
                Vector3 probe = center + axis * (sign * d);
                if (NavMesh.SamplePosition(probe, out NavMeshHit navHit, 2.5f, NavMesh.AllAreas))
                {
                    return navHit.position;
                }
            }

            Vector3 fallback = center + axis * (sign * Mathf.Max(1.0f, maxDistance));
            int stageMask = 1 << LayerMask.NameToLayer("Stage");
            if (Physics.Raycast(fallback + Vector3.up * floorRaycastHeight, Vector3.down, out RaycastHit hit, floorRaycastHeight * 4.0f, stageMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return fallback;
        }

        private static Bounds CalculateWorldBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    b.Encapsulate(renderers[i].bounds);
                }
                return b;
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
            {
                Bounds b = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                {
                    b.Encapsulate(colliders[i].bounds);
                }
                return b;
            }

            return new Bounds(root.position, new Vector3(10.0f, 4.0f, 100.0f));
        }

        private static Vector3 GetPlayerStartPosition()
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
        /// 彼女との会話が完了したときに呼ばれ、現代トンネルをその場で再生成して次の階層へ進める
        /// シーン再ロードは使わない（プレイヤーの再生成に伴う初期化順の不具合を避けるため）
        /// </summary>
        public void AdvanceToNextFloor()
        {
            if (_advancing)
            {
                return;
            }

            int currentSeed = generationSettings != null
                ? generationSettings.Seed
                : (GenerateTunnelTestBootstrap.RuntimeSeedOverride ?? 2777);

            int nextSeed = NextSeed(currentSeed, s_floorNumber);
            s_floorNumber += 1;
            s_pendingSeed = nextSeed;
            GenerateTunnelTestBootstrap.RuntimeSeedOverride = nextSeed;

            Debug.Log($"[TempPastTunnelDirector] 次の階層へ (floor {s_floorNumber}, seed {nextSeed})", this);

            _advancing = true;
            StartCoroutine(RegenerateAndReplace());
        }

        private IEnumerator RegenerateAndReplace()
        {
            _placed = false;
            SetOccupantsActive(false);

            bool started = GenerateTunnelBootstrapper.RegenerateActiveScene();
            if (!started)
            {
                Debug.LogWarning("[TempPastTunnelDirector] 再生成を開始できませんでした", this);
                SetOccupantsActive(true);
                _advancing = false;
                yield break;
            }

            // 新しいジオメトリと NavMesh のベイク完了を待つ
            float deadline = Time.realtimeSinceStartup + maxWaitSeconds;
            Transform special = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                special = FindSpecialTunnel();
                if (special != null && HasBakedNavMesh())
                {
                    break;
                }
                yield return null;
            }

            if (special == null)
            {
                Debug.LogWarning("[TempPastTunnelDirector] 再生成後に Special Tunnel が見つかりませんでした", this);
                SetOccupantsActive(true);
                _advancing = false;
                yield break;
            }

            WarpPlayerToStartTunnel();
            PlaceOccupants(special);
            _advancing = false;
        }

        private void SetOccupantsActive(bool active)
        {
            if (girlfriend != null)
            {
                girlfriend.gameObject.SetActive(active);
            }
            if (stealthEnemy != null)
            {
                stealthEnemy.gameObject.SetActive(active);
            }
        }

        private void WarpPlayerToStartTunnel()
        {
            var player = FindAnyObjectByType<PlayerView>();
            if (player == null)
            {
                return;
            }

            // シーン初期位置の XZ を使う（スタートトンネル中心には Cover 等が置かれていることがある）
            Vector3 start = _playerSpawn ?? GetPlayerStartPosition();
            start.y = GetPlayerStartPosition().y;

            Vector3 floorPoint = start;
            if (NavMesh.SamplePosition(start, out NavMeshHit navHit, navSampleRadius, NavMesh.AllAreas))
            {
                floorPoint = navHit.position;
            }

            // 実際の床コライダーへスナップ（NavMesh はエージェント高さ分浮いていることがある）
            int stageMask = 1 << LayerMask.NameToLayer("Stage");
            if (Physics.Raycast(floorPoint + Vector3.up * 2.0f, Vector3.down, out RaycastHit floorHit, 10.0f, stageMask, QueryTriggerInteraction.Ignore))
            {
                floorPoint = floorHit.point;
            }

            // CharacterController の底が床 + skinWidth に来るようにピボット高さを決める
            var cc = player.GetComponent<CharacterController>();
            float pivotAboveFloor = playerSpawnClearance;
            if (cc != null)
            {
                pivotAboveFloor = (cc.height * 0.5f) - cc.center.y + cc.skinWidth;
            }

            Vector3 target = floorPoint + Vector3.up * pivotAboveFloor;
            player.Warp(target - player.transform.position);
        }

        /// <summary>
        /// 階層カウンタとシード上書きをリセットする（デバッグ用）
        /// </summary>
        [ContextMenu("Reset Floor Progress")]
        public void ResetProgress()
        {
            s_floorNumber = 1;
            s_pendingSeed = null;
            GenerateTunnelTestBootstrap.RuntimeSeedOverride = null;
        }

        private static int NextSeed(int seed, int floor)
        {
            // 決定論的だが階層ごとに十分ばらける疑似乱数列
            unchecked
            {
                uint s = (uint)seed ^ 0x9E3779B9u ^ ((uint)floor * 2654435761u);
                s ^= s << 13;
                s ^= s >> 17;
                s ^= s << 5;
                return (int)s;
            }
        }
    }
}
