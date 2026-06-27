using R3;
using Shinzui.Domain.ValueObjects.Enemy;
using UnityEngine;

namespace Shinzui.Domain.Entities
{
    public class EnemyEntity
    {
        public ReactiveProperty<EnemyMovementState> MovementState { get; } = new(EnemyMovementState.Idle);

        public void UpdateState(EnemyMovementState state)
        {
            MovementState.Value = state;
        }
        
    }
}