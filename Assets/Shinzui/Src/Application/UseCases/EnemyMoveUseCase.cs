using Shinzui.Domain.Entities;
using Shinzui.Domain.ValueObjects.Enemy;

namespace Shinzui.Application.UseCases
{
    /// <summary>
    /// 敵の移動状態やパラメータを統画するユースケース（ピュアC#）
    /// </summary>
    public class EnemyMoveUseCase
    {
        private readonly EnemyEntity _enemyEntity = new();
        public EnemyEntity EnemyEntity => _enemyEntity;

        public bool IsWandering => _enemyEntity.MovementState.Value == EnemyMovementState.Wandering;
        public bool IsChasing => _enemyEntity.MovementState.Value == EnemyMovementState.IsChasing;

        /// <summary>
        /// 徘徊情報を取得する
        /// </summary>
        /// <returns>徘徊目的地を更新するインターバル、徘徊範囲、索敵範囲</returns>
        public (float wanderInterval, float wanderRadius, float chaseDistance) GetWanderingInfo()
        {
            return (_enemyEntity._wanderInterval, _enemyEntity._wanderRadius, _enemyEntity._chaseDistance);
        }

        /// <summary>
        /// 敵の移動状態を変更する
        /// 1 -> Wandering
        /// 2 -> IsChasing
        /// その他 -> Idle
        /// </summary>
        public void UpdateEnemyState(int stateNum)
        {
            switch (stateNum)
            {
                case 1:
                    _enemyEntity.UpdateState(EnemyMovementState.Wandering);
                    break;
                case 2:
                    _enemyEntity.UpdateState(EnemyMovementState.IsChasing);
                    break;
                default:
                    _enemyEntity.UpdateState(EnemyMovementState.Idle);
                    break;
            }
        }
    }
}