using R3;
using Shinzui.Domain.Entities;
using Shinzui.Domain.ValueObjects.Enemy;
using UnityEngine;
using UnityEngine.AI;

namespace Shinzui.Application.UseCases
{
    public class EnemyMoveUseCase : MonoBehaviour
    {
        private readonly EnemyEntity _enemyEntity = new EnemyEntity(); // 敵のエンティティ
        public EnemyEntity EnemyEntity => _enemyEntity;
        
        public bool IsWandering { get; private set; }
        public bool IsChasing { get; private set; }

        /// <summary>
        /// 徘徊情報を取得する
        /// </summary>
        /// <returns>徘徊スパン、徘徊範囲</returns>
        public (float, float) GetWanderingInfo()
        {
            return (EnemyEntity._wanderInterval, EnemyEntity._wanderRadius);
        }

        /// <summary>
        /// 購読の設定
        /// </summary>
        void Start()
        {
            _enemyEntity.MovementState
                .AsObservable()
                .Subscribe(state =>
                {
                    IsWandering = state == EnemyMovementState.Wandering;
                    IsChasing = state == EnemyMovementState.IsChasing;
                }
            );
        }

        /// <summary>
        /// 敵のMovementStateを変更する
        /// 0 -> Idle
        /// 1 -> Wandering
        /// 2 -> IsChasing
        /// </summary>
        /// <param name="stateNum"></param>
        public void UpdateEnemyState(int stateNum)
        {
            switch (stateNum)
            {
                case 0:
                    _enemyEntity.UpdateState(EnemyMovementState.Idle);
                    break;
                case 1:
                    _enemyEntity.UpdateState(EnemyMovementState.Wandering);
                    break;
                case 3:
                    _enemyEntity.UpdateState(EnemyMovementState.IsChasing);
                    break;
                default:
                    break;
            }
        }
        
        /// <summary>
        /// 敵の移動先を設定する
        /// </summary>
        /// <param name="agent">NavMeshAgentを持つ敵</param>
        /// <param name="target">移動先のターゲットの座標</param>
        public void SetDestination(NavMeshAgent agent, Vector3 target)
        {
            agent.SetDestination(target);
        }
    }
}