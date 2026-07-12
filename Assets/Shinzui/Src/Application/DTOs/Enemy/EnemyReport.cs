using UnityEngine;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>
    /// 個別Enemyから統括AIへ渡す状態報告
    /// </summary>
    public readonly struct EnemyReport
    {
        public EnemyReport(
            int id,
            Vector3 position,
            EnemyCommand currentCommand,
            float chaseDistance,
            bool canReceiveCommand)
        {
            Id = id;
            Position = position;
            CurrentCommand = currentCommand;
            ChaseDistance = chaseDistance;
            CanReceiveCommand = canReceiveCommand;
        }

        public int Id { get; }
        public Vector3 Position { get; }
        public EnemyCommand CurrentCommand { get; }
        public float ChaseDistance { get; }
        public bool CanReceiveCommand { get; }
    }
}
