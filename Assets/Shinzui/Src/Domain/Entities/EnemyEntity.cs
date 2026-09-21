using Shinzui.Domain.ValueObjects.Enemy;

namespace Shinzui.Domain.Entities
{
    public class EnemyEntity
    {
        public EnemyMovementState MovementState { get; private set; } = EnemyMovementState.Wandering;
        public readonly float _wanderInterval = 10.0f;  //徘徊目的地を更新するインターバル
        public readonly float _wanderRadius = 10.0f;    //徘徊範囲
        public readonly float _chaseDistance = 10.0f;   //索敵範囲

        public void UpdateState(EnemyMovementState state)
        {
            MovementState = state;
        }
        
    }
}
