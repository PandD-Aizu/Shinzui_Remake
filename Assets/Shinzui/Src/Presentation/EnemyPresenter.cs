using UnityEngine;
using UnityEngine.AI;
using Shinzui.Application.UseCases;
using UnityEngine.Android;

namespace Shinzui.Presentation
{
    public class EnemyPresenter : MonoBehaviour
    {
        private EnemyMoveUseCase _enemyMoveUseCase;
        private NavMeshAgent _agent;
        private Transform _player;

        private float _timer;           //時間計測用タイマー
        private float _wanderInterval;  //徘徊目的地を更新するインターバル
        private float _wanderRadius;    //徘徊範囲
        
        private Vector3 tunnelStartPos;  //プレイヤーがいるトンネルのスタート地点
        private Vector3 tunnelEndPos;    //プレイヤーがいるトンネルのゴール地点

        void Start()
        {
            _enemyMoveUseCase = gameObject.AddComponent<EnemyMoveUseCase>();
            _agent = transform.parent.GetComponent<NavMeshAgent>();
            _player = GameObject.Find("Player").GetComponent<Transform>();
            (_wanderInterval, _wanderRadius) = _enemyMoveUseCase.GetWanderingInfo();
        }

        void Update()
        {
            // 敵が徘徊状態なら、一定時間間隔で移動先を決めて移動する
            if (_enemyMoveUseCase.IsWandering)
            {
                _timer += Time.deltaTime;
                if (_timer > _wanderInterval)
                {
                    Debug.Log("wandering: " + _agent.name);
                    Vector3 targetPosition = RandomTarget(transform.position, _wanderRadius);
                    _enemyMoveUseCase.SetDestination(_agent, targetPosition);
                    _timer = 0;
                }
            }

            if (_enemyMoveUseCase.IsChasing)
            {
                Vector3 playerPos = _player.position;
                Vector3 dummy = playerPos;
                
                // TODO: tunnelの取得ができるようになったらコメントアウトを外す
                /*tunnnelStartPos = PlayerMoveUseCase.currentTunnelStart.transform.position;
                tunnnelEndPos = PlayerMoveUseCase.currentTunnelEnd.transform.position;
                
                float centerZ = (tunnelStartPos.z + tunnelEndPos.z)/2;                 //トンネルの中央
                float tunnelDistance = Mathf.Abs(tunnelStartPos.z - tunnelEndPos.z); //トンネルの長さ

                if (playerPos.z > centerZ) dummy.z -= tunnelDistance;
                else dummy.z += tunnelDistance;
                
                Vector3 target = Vector3.Distance(transform.position, playerPos) < Vector3.Distance(transform.position, dummy) ? playerPos : dummy;
                
                _enemyMoveUseCase.SetDestination(_agent, target);*/ // <-これを使用するとき、下のコードは使わない
                _enemyMoveUseCase.SetDestination(_agent, _player.position);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (_agent == null || _player == null) return;

            if (other.name == _player.name)
            {
                _enemyMoveUseCase.UpdateEnemyState(2); //敵のMovementStateをIsChasingに変更する
            }
        }

        void OnTriggerExit(Collider other)
        {
            _enemyMoveUseCase.UpdateEnemyState(1); //敵のMovementStateをWanderingに変更する
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
    }
}