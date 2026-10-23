using UnityEngine;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>
    /// 個別Enemyから統括AIへ渡す状態報告
    /// </summary>
    public readonly struct EnemyReport
    {
        /// <summary>敵の行動状態と双方の視認状態を保持</summary>
        /// <param name="id">敵の識別子</param>
        /// <param name="position">敵の位置</param>
        /// <param name="currentCommand">現在の命令</param>
        /// <param name="chaseDistance">索敵距離</param>
        /// <param name="canReceiveCommand">行動可能か</param>
        /// <param name="canSeePlayer">敵がプレイヤーを視認しているか</param>
        /// <param name="isVisibleToPlayer">プレイヤーが敵を視認しているか</param>
        public EnemyReport(
            int id,
            Vector3 position,
            EnemyCommand currentCommand,
            float chaseDistance,
            bool canReceiveCommand,
            bool canSeePlayer = false,
            bool isVisibleToPlayer = false)
        {
            Id = id;
            Position = position;
            CurrentCommand = currentCommand;
            ChaseDistance = chaseDistance;
            CanReceiveCommand = canReceiveCommand;
            CanSeePlayer = canSeePlayer;
            IsVisibleToPlayer = isVisibleToPlayer;
        }

        public int Id { get; }
        public Vector3 Position { get; }
        public EnemyCommand CurrentCommand { get; }
        public float ChaseDistance { get; }
        public bool CanReceiveCommand { get; }
        public bool CanSeePlayer { get; }
        public bool IsVisibleToPlayer { get; }
    }
}
