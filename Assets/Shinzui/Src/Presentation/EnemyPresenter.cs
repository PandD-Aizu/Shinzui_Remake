using System;
using UnityEngine;
using UnityEngine.AI;
using Shinzui.Application.UseCases;
using UnityEngine.Android;
using VContainer;
using Random = UnityEngine.Random;

namespace Shinzui.Presentation
{
    public class EnemyPresenter : MonoBehaviour
    {
        private EnemyMoveUseCase _enemyMoveUseCase;
        private NavMeshAgent _agent;
        private Transform _player;
        private PlayerMoveUseCase _playerMoveUseCase;

        private float _timer;                   //時間計測用タイマー
        private float _wanderInterval = -1.0f;  //徘徊目的地を更新するインターバル
        private float _wanderRadius = -1.0f;    //徘徊範囲
        private float _chaseDistance = -1.0f;   //索敵範囲

        [Inject]
        public void Construct(PlayerMoveUseCase playerMoveUseCase)
        {
            _playerMoveUseCase = playerMoveUseCase;
        } 
        
        void Start()
        {
            _enemyMoveUseCase = gameObject.AddComponent<EnemyMoveUseCase>();
            _agent = transform.parent.GetComponent<NavMeshAgent>();
            _player = GameObject.Find("Player").GetComponent<Transform>();
            (_wanderInterval, _wanderRadius, _chaseDistance) = _enemyMoveUseCase.GetWanderingInfo();
            _timer = _wanderInterval;
            
            if(_wanderInterval < 0) Debug.LogWarning("EnemyPresenter: Wander interval is negative");
            if(_wanderRadius < 0) Debug.LogWarning("EnemyPresenter: WanderRadius is negative");
            if(_chaseDistance < 0) Debug.LogWarning("EnemyPresenter: Chase distance is negative");
        }

        void Update()
        {
            Vector3 playerPos = _player.position;
            Vector3 tunnelStartPos = _playerMoveUseCase.currentTunnelStart.transform.position;
            Vector3 tunnelEndPos = _playerMoveUseCase.currentTunnelEnd.transform.position;
            
            Vector3 dummy = playerPos;
                
            float centerZ = (tunnelStartPos.z + tunnelEndPos.z)/2;                 //トンネルの中央
            float tunnelDistance = Mathf.Abs(tunnelStartPos.z - tunnelEndPos.z); //トンネルの長さ

            if (playerPos.z > centerZ) dummy.z -= tunnelDistance;
            else dummy.z += tunnelDistance;

            if (Vector3.Distance(transform.position, playerPos) < _chaseDistance ||
                Vector3.Distance(transform.position, dummy) < _chaseDistance)
            {
                _enemyMoveUseCase.UpdateEnemyState(2); //敵のMovementStateをIsChasingに変更する
            }
            else _enemyMoveUseCase.UpdateEnemyState(1); //敵のMovementStateをWanderingに変更する
            
            // 敵が徘徊状態なら、一定時間間隔で移動先を決めて移動する
            if (_enemyMoveUseCase.IsWandering)
            {
                _timer += Time.deltaTime;
                if (_timer > _wanderInterval)
                {
                    Vector3 targetPosition = RandomTarget(transform.position, _wanderRadius);
                    _enemyMoveUseCase.SetDestination(_agent, targetPosition);
                    _timer = 0;
                }
            }

            //敵が追跡状態ならプレイヤーまたはダミーのうち、より近いほうを追跡する
            if (_enemyMoveUseCase.IsChasing)
            {
                Vector3 target = Vector3.Distance(transform.position, playerPos) < Vector3.Distance(transform.position, dummy) ? playerPos : dummy;
                
                _enemyMoveUseCase.SetDestination(_agent, target);
            }
            
            if(transform.position.z > tunnelEndPos.z) _enemyMoveUseCase.Warp(tunnelStartPos);
            if(transform.position.z < tunnelStartPos.z) _enemyMoveUseCase.Warp(tunnelEndPos);
        }

        /// <summary>
        /// 現在位置から徘徊範囲内でランダムに移動先を決める
        /// </summary>
        /// <param name="origin">現在位置</param>
        /// <param name="radius">徘徊範囲</param>
        /// <returns></returns>
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