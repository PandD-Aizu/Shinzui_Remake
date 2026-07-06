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
    /// プレイヤー情報へのアクセスは抽象化された IPlayerTracker を経由します
    /// </summary>
    public class EnemyPresenter : MonoBehaviour
    {
        private EnemyMoveUseCase _enemyMoveUseCase;
        private NavMeshAgent _agent;
        private IPlayerTracker _playerTracker;

        private float _timer;                   // 時間計測用タイマー
        private float _wanderInterval = -1.0f;  // 徘徊目的地を更新するインターバル
        private float _wanderRadius = -1.0f;    // 徘徊範囲
        private float _chaseDistance = -1.0f;   // 索敵範囲

        [Inject]
        public void Construct(IPlayerTracker playerTracker)
        {
            _playerTracker = playerTracker;
        } 
        
        void Start()
        {
            _enemyMoveUseCase = new EnemyMoveUseCase();
            
            // 親オブジェクトにある NavMeshAgent を取得
            if (transform.parent != null)
            {
                _agent = transform.parent.GetComponent<NavMeshAgent>();
            }
            else
            {
                _agent = GetComponent<NavMeshAgent>();
            }

            (_wanderInterval, _wanderRadius, _chaseDistance) = _enemyMoveUseCase.GetWanderingInfo();
            _timer = _wanderInterval;
            
            if (_wanderInterval < 0) Debug.LogWarning("EnemyPresenter: Wander interval is negative");
            if (_wanderRadius < 0) Debug.LogWarning("EnemyPresenter: WanderRadius is negative");
            if (_chaseDistance < 0) Debug.LogWarning("EnemyPresenter: Chase distance is negative");
        }

        void Update()
        {
            if (_playerTracker == null) return;

            Vector3 playerPos = _playerTracker.PlayerPosition;
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
        }
    }
}