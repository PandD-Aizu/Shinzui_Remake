using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Shinzui.Temp
{
    /// <summary>
    /// 指定されたルートを徘徊（巡回）し、視覚スクリプト（TempEnemyVision）でプレイヤーを発見すると追跡する敵コンポーネント
    /// </summary>
    [RequireComponent(typeof(TempEnemyVision))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class TempPatrolChaseEnemy : MonoBehaviour
    {
        public enum EnemyState
        {
            Patrol,    // 徘徊（巡回）中
            Chase,     // プレイヤー追跡中
            Search     // 見失った後、見回し・探索中
        }

        [Header("Components")]
        [SerializeField] private TempEnemyVision enemyVision;
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private CharacterController characterController;

        [Header("Patrol Settings")]
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float patrolSpeed = 2.0f;
        [SerializeField] private float waypointWaitTime = 2.0f;

        [Header("Chase Settings")]
        [SerializeField] private float chaseSpeed = 4.5f;
        [SerializeField] private float lostTargetDelay = 3.0f;

        [Header("Current Status")]
        [SerializeField] private EnemyState currentState = EnemyState.Patrol;
        [SerializeField] private Transform currentTarget;

        private int _currentWaypointIndex = 0;
        private float _waitTimer = 0.0f;
        private float _searchTimer = 0.0f;
        private Vector3 _lastKnownPlayerPosition;

        private void Awake()
        {
            AutoSetupReferences();
        }

        private void Reset()
        {
            AutoSetupReferences();
        }

        private void OnValidate()
        {
            AutoSetupReferences();
        }

        [ContextMenu("Auto Setup Enemy References")]
        public void AutoSetupReferences()
        {
            if (enemyVision == null)
            {
                enemyVision = GetComponent<TempEnemyVision>();
                if (enemyVision == null)
                {
                    enemyVision = gameObject.AddComponent<TempEnemyVision>();
                }
            }

            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponent<NavMeshAgent>();
                if (navMeshAgent == null)
                {
                    navMeshAgent = gameObject.AddComponent<NavMeshAgent>();
                }
            }

            // NavMeshAgentの初期パラメーター調整
            if (navMeshAgent != null)
            {
                navMeshAgent.speed = patrolSpeed;
                navMeshAgent.angularSpeed = 360.0f;
                navMeshAgent.acceleration = 8.0f;
                navMeshAgent.stoppingDistance = 0.5f;
                navMeshAgent.radius = 0.5f;
                navMeshAgent.height = 2.0f;
            }

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
                if (characterController == null)
                {
                    characterController = gameObject.AddComponent<CharacterController>();
                }
            }

            if (characterController != null)
            {
                characterController.height = 2.0f;
                characterController.center = new Vector3(0, 1.0f, 0);
                characterController.radius = 0.5f;
            }

            AutoFindWaypointsIfNeeded();
        }

        private void Start()
        {
            AutoFindWaypointsIfNeeded();

            if (waypoints != null && waypoints.Length > 0)
            {
                MoveToCurrentWaypoint();
            }
        }

        private void AutoFindWaypointsIfNeeded()
        {
            bool needsWaypoints = false;
            if (waypoints == null || waypoints.Length == 0)
            {
                needsWaypoints = true;
            }
            else
            {
                bool allNull = true;
                foreach (Transform wp in waypoints)
                {
                    if (wp != null) { allNull = false; break; }
                }
                if (allNull) needsWaypoints = true;
            }

            if (needsWaypoints)
            {
                var foundList = new System.Collections.Generic.List<Transform>();
                int index = 1;
                while (true)
                {
                    GameObject wpObj = GameObject.Find("EnemyWaypoint" + index);
                    if (wpObj == null) wpObj = GameObject.Find("Waypoint" + index);
                    if (wpObj == null) break;
                    foundList.Add(wpObj.transform);
                    index++;
                }

                if (foundList.Count > 0)
                {
                    waypoints = foundList.ToArray();
                }
            }
        }

        private void Update()
        {
            bool isPlayerDetected = enemyVision != null && enemyVision.IsPlayerDetected;

            // 状態切り替えロジック
            if (isPlayerDetected)
            {
                currentState = EnemyState.Chase;
                _searchTimer = 0.0f;
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    _lastKnownPlayerPosition = playerObj.transform.position;
                    currentTarget = playerObj.transform;
                }
            }
            else if (currentState == EnemyState.Chase)
            {
                // 見失ったらSearch状態へ移行
                currentState = EnemyState.Search;
                _searchTimer = lostTargetDelay;
            }

            // 状態に応じた行動実行
            switch (currentState)
            {
                case EnemyState.Patrol:
                    UpdatePatrol();
                    break;
                case EnemyState.Chase:
                    UpdateChase();
                    break;
                case EnemyState.Search:
                    UpdateSearch();
                    break;
            }
        }

        #region Patrol Logic
        private void UpdatePatrol()
        {
            SetSpeed(patrolSpeed);

            if (waypoints == null || waypoints.Length == 0)
            {
                return;
            }

            Transform targetWaypoint = waypoints[_currentWaypointIndex];
            if (targetWaypoint == null)
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % waypoints.Length;
                return;
            }

            // XZ平面での距離チェック
            Vector3 posXZ = new Vector3(transform.position.x, 0.0f, transform.position.z);
            Vector3 wpXZ = new Vector3(targetWaypoint.position.x, 0.0f, targetWaypoint.position.z);
            float distanceToWaypoint = Vector3.Distance(posXZ, wpXZ);

            if (distanceToWaypoint <= 1.0f)
            {
                _waitTimer += Time.deltaTime;
                if (_waitTimer >= waypointWaitTime)
                {
                    _waitTimer = 0.0f;
                    _currentWaypointIndex = (_currentWaypointIndex + 1) % waypoints.Length;
                    MoveToCurrentWaypoint();
                }
            }
            else
            {
                MoveToward(targetWaypoint.position);
            }
        }

        private void MoveToCurrentWaypoint()
        {
            if (waypoints != null && waypoints.Length > _currentWaypointIndex)
            {
                Transform wp = waypoints[_currentWaypointIndex];
                if (wp != null)
                {
                    MoveToward(wp.position);
                }
            }
        }
        #endregion

        #region Chase Logic
        private void UpdateChase()
        {
            SetSpeed(chaseSpeed);

            if (currentTarget != null)
            {
                _lastKnownPlayerPosition = currentTarget.position;
                MoveToward(currentTarget.position);
            }
        }
        #endregion

        #region Search Logic
        private void UpdateSearch()
        {
            SetSpeed(patrolSpeed);
            MoveToward(_lastKnownPlayerPosition);

            _searchTimer -= Time.deltaTime;
            if (_searchTimer <= 0.0f)
            {
                // 探索時間終了、徘徊に戻る
                currentState = EnemyState.Patrol;
                MoveToCurrentWaypoint();
            }
        }
        #endregion

        #region Movement Helper
        private void SetSpeed(float speed)
        {
            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.speed = speed;
            }
        }

        /// <summary>
        /// 障害物の迂回および貫通防止移動
        /// </summary>
        private void MoveToward(Vector3 targetPosition)
        {
            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.isStopped = false;
                navMeshAgent.SetDestination(targetPosition);
                return;
            }

            float moveSpeed = (currentState == EnemyState.Chase) ? chaseSpeed : patrolSpeed;
            Vector3 direction = (targetPosition - transform.position);
            direction.y = 0;

            if (direction.sqrMagnitude < 0.01f) return;

            Vector3 desiredDir = direction.normalized;
            Vector3 moveDir = desiredDir;

            // 障害物の検知と迂回（SphereCast）
            float rayRadius = characterController != null ? characterController.radius : 0.4f;
            float checkDistance = 1.0f;
            Vector3 rayOrigin = transform.position + Vector3.up * 0.8f;

            if (Physics.SphereCast(rayOrigin, rayRadius, desiredDir, out RaycastHit hit, checkDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                // ターゲット（プレイヤー）本人でなければ壁・障害物とみなしてスライド
                if (currentTarget == null || (hit.transform != currentTarget && !hit.transform.IsChildOf(currentTarget)))
                {
                    Vector3 avoidDir = Vector3.ProjectOnPlane(desiredDir, hit.normal).normalized;
                    if (avoidDir.sqrMagnitude > 0.01f)
                    {
                        moveDir = avoidDir;
                    }
                }
            }

            // 回転を滑らかに合わせる
            if (moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 360.0f * Time.deltaTime);
            }

            // 衝突判定（CharacterController）による安全な移動（壁貫通防止）
            if (characterController != null)
            {
                if (!characterController.enabled)
                {
                    characterController.enabled = true;
                }

                Vector3 velocity = moveDir * moveSpeed;
                // 接地判定と重力追加
                velocity.y = characterController.isGrounded ? -2.0f : Physics.gravity.y;
                characterController.Move(velocity * Time.deltaTime);
            }
        }
        #endregion

        private void OnDrawGizmosSelected()
        {
            // 徘徊ルートの可視化
            if (waypoints != null && waypoints.Length > 0)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < waypoints.Length; i++)
                {
                    if (waypoints[i] == null) continue;
                    Gizmos.DrawSphere(waypoints[i].position, 0.3f);

                    int nextIndex = (i + 1) % waypoints.Length;
                    if (waypoints[nextIndex] != null)
                    {
                        Gizmos.DrawLine(waypoints[i].position, waypoints[nextIndex].position);
                    }
                }
            }
        }
    }
}
