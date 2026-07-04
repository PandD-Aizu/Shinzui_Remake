using R3;
using Shinzui.Domain.ValueObjects.Enemy;
using UnityEngine;

namespace Shinzui.Domain.Entities
{
    public class EnemyEntity
    {
        public ReactiveProperty<EnemyMovementState> MovementState { get; } = new(EnemyMovementState.Wandering);
        public readonly float _wanderInterval = 10.0f;  //徘徊目的地を更新するインターバル
        public readonly float _wanderRadius = 10.0f;    //徘徊範囲

        public void UpdateState(EnemyMovementState state)
        {
            MovementState.Value = state;
        }
        
    }
}