using UnityEngine;
using UnityEngine.AI;
using Shinzui.Application.UseCases;

namespace Shinzui.Presentation
{
    public class EnemyPresenter : MonoBehaviour
    {
        private EnemyMoveUseCase _enemyMoveUseCase;
        private NavMeshAgent _agent;
        private Transform _player;

        private float _timer;                            //時間計測用タイマー
        private readonly float _wanderInterval = 10.0f;  //徘徊目的地を更新するインターバル
        private readonly float _wanderRadius = 10.0f;    //徘徊範囲

        void Start()
        {
            _enemyMoveUseCase = gameObject.AddComponent<EnemyMoveUseCase>();
            _agent = transform.parent.GetComponent<NavMeshAgent>();
            _player = GameObject.Find("Player").GetComponent<Transform>();
            _timer = _wanderInterval;
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