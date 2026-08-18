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
            if (enemyVision == null) enemyVision = GetComponent<TempEnemyVision>();
            if (navMeshAgent == null) navMeshAgent = GetComponent<NavMeshAgent>();
            if (characterController == null) characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            if (waypoints != null && waypoints.Length > 0)
            {
                MoveToCurrentWaypoint();
            }
        }

        private void Update()
        {
            bool isPlayerDetected = enemyVision != null && enemyVision.IsPlayerDetected;

            if (isPlayerDetected)
            {
                currentState = EnemyState.Chase;
                _searchTimer = 0.0f;
                if (enemyVision != null && enemyVision.PlayerTarget != null)
                {
                    currentTarget = enemyVision.PlayerTarget;
                    _lastKnownPlayerPosition = currentTarget.position;
                }
            }
            else if (currentState == EnemyState.Chase)
            {
                currentState = EnemyState.Search;
                _searchTimer = lostTargetDelay;
            }

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
        /// <summary>
        /// 巡回（徘徊）状態の移動およびウェイポイント待機処理を実行する
        /// </summary>
        private void UpdatePatrol()
        {
            SetSpeed(patrolSpeed);

            if (waypoints == null || waypoints.Length == 0) return;

            Transform targetWaypoint = waypoints[_currentWaypointIndex];
            if (targetWaypoint == null)
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % waypoints.Length;
                return;
            }

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

        /// <summary>
        /// 現在インデックスのウェイポイント座標に向かって移動を開始する
        /// </summary>
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
        /// <summary>
        /// プレイヤー追跡状態の移動処理を実行する
        /// </summary>
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
        /// <summary>
        /// プレイヤー見失い後の最終確認位置への捜索移動を実行する
        /// </summary>
        private void UpdateSearch()
        {
            SetSpeed(patrolSpeed);
            MoveToward(_lastKnownPlayerPosition);

            _searchTimer -= Time.deltaTime;
            if (_searchTimer <= 0.0f)
            {
                currentState = EnemyState.Patrol;
                MoveToCurrentWaypoint();
            }
        }
        #endregion

        #region Movement Helper
        /// <summary>
        /// NavMeshAgentの移動速度を設定する
        /// </summary>
        /// <param name="speed">移動速度</param>
        private void SetSpeed(float speed)
        {
            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.speed = speed;
            }
        }

        /// <summary>
        /// 目標座標に向けて移動する（NavMeshAgentまたはCharacterControllerでの衝突回避移動）
        /// </summary>
        /// <param name="targetPosition">目標座標</param>
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

            float rayRadius = characterController != null ? characterController.radius : 0.4f;
            float checkDistance = 1.0f;
            Vector3 rayOrigin = transform.position + Vector3.up * 0.8f;

            if (Physics.SphereCast(rayOrigin, rayRadius, desiredDir, out RaycastHit hit, checkDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (currentTarget == null || (hit.transform != currentTarget && !hit.transform.IsChildOf(currentTarget)))
                {
                    Vector3 avoidDir = Vector3.ProjectOnPlane(desiredDir, hit.normal).normalized;
                    if (avoidDir.sqrMagnitude > 0.01f)
                    {
                        moveDir = avoidDir;
                    }
                }
            }

            if (moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 360.0f * Time.deltaTime);
            }

            if (characterController != null)
            {
                if (!characterController.enabled)
                {
                    characterController.enabled = true;
                }

                Vector3 velocity = moveDir * moveSpeed;
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
