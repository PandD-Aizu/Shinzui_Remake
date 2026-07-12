using System;
using UnityEngine;
using UnityEngine.AI;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases;
using VContainer;
using Random = UnityEngine.Random;

namespace Shinzui.Presentation
{
    /// <summary>
    /// 敵の行動AIプレゼンター
    /// プレイヤー情報へのアクセスは抽象化された IPlayerTracker を経由
    /// </summary>
    public class EnemyPresenter : MonoBehaviour
    {
        private EnemyMoveUseCase _enemyMoveUseCase;
        private NavMeshAgent _agent;
        private IPlayerTracker _playerTracker;
        private PlayerDeathUseCase _playerDeathUseCase;

        [Header("Player Death")]
        [SerializeField] private float playerDeathDistance = 1.2f;
        [SerializeField] private float deathAttemptCooldown = 1.0f;

        [Header("References")]
        [SerializeField] private NavMeshAgent agentOverride;

        private float _timer;                   // 時間計測用タイマー
        private float _wanderInterval = -1.0f;  // 徘徊目的地を更新するインターバル
        private float _wanderRadius = -1.0f;    // 徘徊範囲
        private float _chaseDistance = -1.0f;   // 索敵範囲
        private float _deathAttemptTimer;
        private float _baseAgentSpeed = -1.0f;
        private float _strobeEffectRemaining;
        private float _strobeSpeedMultiplier = 1.0f;
        private bool _strobeStopsMovement;

        public Vector3 StrobeTargetPosition
        {
            get
            {
                if (_agent != null)
                {
                    return _agent.transform.position + Vector3.up;
                }

                return transform.position + Vector3.up;
            }
        }

        [Inject]
        public void Construct(IPlayerTracker playerTracker, PlayerDeathUseCase playerDeathUseCase)
        {
            _playerTracker = playerTracker;
            _playerDeathUseCase = playerDeathUseCase;
        } 
        
        void Start()
        {
            _enemyMoveUseCase = new EnemyMoveUseCase();
            _agent = ResolveAgent();

            if (_agent != null)
            {
                _baseAgentSpeed = _agent.speed;
            }

            (_wanderInterval, _wanderRadius, _chaseDistance) = _enemyMoveUseCase.GetWanderingInfo();
            _timer = _wanderInterval;
            
            if (_wanderInterval < 0) Debug.LogWarning("EnemyPresenter: Wander interval is negative");
            if (_wanderRadius < 0) Debug.LogWarning("EnemyPresenter: WanderRadius is negative");
            if (_chaseDistance < 0) Debug.LogWarning("EnemyPresenter: Chase distance is negative");
        }

        private NavMeshAgent ResolveAgent()
        {
            if (agentOverride != null)
            {
                return agentOverride;
            }

            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                return agent;
            }

            agent = GetComponentInParent<NavMeshAgent>();
            if (agent != null)
            {
                return agent;
            }

            agent = GetComponentInChildren<NavMeshAgent>();
            if (agent != null)
            {
                return agent;
            }

            return FindNearestAgent(8.0f);
        }

        private NavMeshAgent FindNearestAgent(float maxDistance)
        {
            NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            NavMeshAgent nearestAgent = null;
            float nearestSqrDistance = maxDistance * maxDistance;

            foreach (NavMeshAgent agent in agents)
            {
                float sqrDistance = (agent.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance > nearestSqrDistance)
                {
                    continue;
                }

                nearestSqrDistance = sqrDistance;
                nearestAgent = agent;
            }

            return nearestAgent;
        }

        void Update()
        {
            UpdateStrobeEffect(Time.deltaTime);
            UpdateDeathAttemptTimer(Time.deltaTime);

            if (_playerTracker == null) return;

            Vector3 playerPos = _playerTracker.PlayerPosition;
            TryKillPlayerIfClose(playerPos);

            var tunnelBounds = _playerTracker.CurrentTunnelBounds;

            // トンネル境界情報が未取得の場合は動作しない
            if (!tunnelBounds.HasValue) return;

            Vector3 tunnelStartPos = tunnelBounds.Value.start;
            Vector3 tunnelEndPos = tunnelBounds.Value.end;

            Vector3 dummy = playerPos;
            float centerZ = (tunnelStartPos.z + tunnelEndPos.z) / 2.0f;               // トンネルの中央
            float tunnelDistance = Mathf.Abs(tunnelStartPos.z - tunnelEndPos.z);      // トンネルの長さ

            if (playerPos.z > centerZ) dummy.z -= tunnelDistance;
            else dummy.z += tunnelDistance;

            float distanceToPlayer = Vector3.Distance(transform.position, playerPos);
            float distanceToDummy = Vector3.Distance(transform.position, dummy);

            // プレイヤーまたはループ対岸のダミー位置が索敵範囲内であれば追跡状態にする
            if (distanceToPlayer < _chaseDistance || distanceToDummy < _chaseDistance)
            {
                _enemyMoveUseCase.UpdateEnemyState(2); // IsChasing
            }
            else
            {
                _enemyMoveUseCase.UpdateEnemyState(1); // Wandering
            }
            
            // 敵が徘徊状態なら、一定時間間隔で移動先を決めて移動する
            if (_enemyMoveUseCase.IsWandering)
            {
                _timer += Time.deltaTime;
                if (_timer > _wanderInterval)
                {
                    Vector3 targetPosition = RandomTarget(transform.position, _wanderRadius);
                    if (_agent != null)
                    {
                        _agent.SetDestination(targetPosition);
                    }
                    _timer = 0;
                }
            }

            // 敵が追跡状態ならプレイヤーまたはダミーのうち、より近いほうを追跡する
            if (_enemyMoveUseCase.IsChasing)
            {
                Vector3 target = distanceToPlayer < distanceToDummy ? playerPos : dummy;
                if (_agent != null)
                {
                    _agent.SetDestination(target);
                }
            }
            
            // ループトンネルの境界ワープ処理
            if (transform.position.z > tunnelEndPos.z) Warp(tunnelStartPos);
            if (transform.position.z < tunnelStartPos.z) Warp(tunnelEndPos);
        }

        /// <summary>
        /// ストロボの効果を適用する
        /// </summary>
        /// <param name="stopMovement">移動を停止するかどうか</param>
        /// <param name="speedMultiplier">速度の倍率</param>
        /// <param name="duration">効果の持続時間</param>
        public void ApplyStrobeEffect(bool stopMovement, float speedMultiplier, float duration)
        {
            if (_agent == null || duration <= 0f) return;

            if (_baseAgentSpeed < 0f)
            {
                _baseAgentSpeed = _agent.speed;
            }

            if (stopMovement)
            {
                _strobeStopsMovement = true;
                _strobeEffectRemaining = Mathf.Max(_strobeEffectRemaining, duration);
                ApplyCurrentStrobeAgentState();
                return;
            }

            if (_strobeStopsMovement)
            {
                return;
            }

            _strobeSpeedMultiplier = Mathf.Min(_strobeSpeedMultiplier, Mathf.Clamp01(speedMultiplier));
            _strobeEffectRemaining = Mathf.Max(_strobeEffectRemaining, duration);
            ApplyCurrentStrobeAgentState();
        }

        /// <summary>
        /// ストロボ効果の更新処理
        /// </summary>
        /// <param name="deltaTime">経過時間</param>
        private void UpdateStrobeEffect(float deltaTime)
        {
            if (_agent == null || _strobeEffectRemaining <= 0f) return;

            _strobeEffectRemaining -= deltaTime;
            if (_strobeEffectRemaining <= 0f)
            {
                ClearStrobeEffect();
                return;
            }

            ApplyCurrentStrobeAgentState();
        }

        /// <summary>
        /// 現在のストロボ状態を適用する
        /// </summary>
        private void ApplyCurrentStrobeAgentState()
        {
            if (_agent == null) return;

            if (_strobeStopsMovement)
            {
                _agent.isStopped = true;
                _agent.speed = 0f;
            }
            else
            {
                _agent.isStopped = false;
                _agent.speed = _baseAgentSpeed * _strobeSpeedMultiplier;
            }
        }

        /// <summary>
        /// ストロボ効果をクリアする
        /// </summary>
        private void ClearStrobeEffect()
        {
            _strobeEffectRemaining = 0f;
            _strobeSpeedMultiplier = 1.0f;
            _strobeStopsMovement = false;

            if (_agent != null)
            {
                _agent.isStopped = false;
                if (_baseAgentSpeed >= 0f)
                {
                    _agent.speed = _baseAgentSpeed;
                }
            }
        }

        /// <summary>
        /// トンネルの境界を超えた場合に、反対側にワープさせる
        /// </summary>
        /// <param name="warpTarget">ワープ先の座標</param>
        private void Warp(Vector3 warpTarget)
        {
            if (transform.parent != null)
            {
                Vector3 pos = transform.parent.position;
                pos.z = warpTarget.z;
                transform.parent.position = pos;
            }
            else
            {
                Vector3 pos = transform.position;
                pos.z = warpTarget.z;
                transform.position = pos;
            }
        }

        /// <summary>
        /// 現在位置から徘徊範囲内でランダムに移動先を決める
        /// </summary>
        /// <param name="origin">基準位置</param>
        /// <param name="radius">範囲</param>
        /// <returns>ランダムな移動先</returns>
        Vector3 RandomTarget(Vector3 origin, float radius)
        {
            Vector3 randomDirection = Random.insideUnitSphere * radius;
            randomDirection += origin;
            
            NavMeshHit hit;
            NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas);
            return hit.position;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(gameObject.transform.position, _chaseDistance);

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(GetEnemyPosition(), playerDeathDistance);
        }

        private void TryKillPlayerIfClose(Vector3 playerPosition)
        {
            if (_playerDeathUseCase == null || _deathAttemptTimer > 0f)
            {
                return;
            }

            float distanceToPlayer = Vector3.Distance(GetEnemyPosition(), playerPosition);
            if (distanceToPlayer > playerDeathDistance)
            {
                return;
            }

            _playerDeathUseCase.TryKillPlayer();
            _deathAttemptTimer = deathAttemptCooldown;
        }

        private void UpdateDeathAttemptTimer(float deltaTime)
        {
            if (_deathAttemptTimer <= 0f)
            {
                return;
            }

            _deathAttemptTimer -= deltaTime;
        }

        private Vector3 GetEnemyPosition()
        {
            return _agent != null ? _agent.transform.position : transform.position;
        }
    }
}
