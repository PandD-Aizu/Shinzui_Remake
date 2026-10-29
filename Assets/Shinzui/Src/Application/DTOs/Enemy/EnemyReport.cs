using Shinzui.Domain.ValueObjects.Enemy;
using UnityEngine;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>敵自身の判断をDirectorが観測するための報告</summary>
    public readonly struct EnemyReport
    {
        public int Id { get; }
        public Vector3 Position { get; }
        public EnemyIntent ActiveIntent { get; }
        public bool CanReceiveCommand { get; }
        public bool IsVisibleToPlayer { get; }
        public EnemyAwarenessLevel Awareness { get; }
        public bool IsEngaged => Awareness == EnemyAwarenessLevel.Engaged;
        public bool IsBackstage { get; }

        /// <summary>敵の行動と警戒状態を保持</summary>
        /// <param name="id">敵の識別子</param>
        /// <param name="position">敵自身の位置</param>
        /// <param name="activeIntent">選択中の行動</param>
        /// <param name="canReceiveCommand">行動可能か</param>
        /// <param name="isVisibleToPlayer">プレイヤーから視認できるか</param>
        /// <param name="awareness">警戒段階</param>
        /// <param name="isBackstage">舞台裏にいるか</param>
        public EnemyReport(int id, Vector3 position, EnemyIntent activeIntent, bool canReceiveCommand,
            bool isVisibleToPlayer = false, EnemyAwarenessLevel awareness = EnemyAwarenessLevel.Unaware,
            bool isBackstage = false)
        {
            Id = id;
            Position = position;
            ActiveIntent = activeIntent;
            CanReceiveCommand = canReceiveCommand;
            IsVisibleToPlayer = isVisibleToPlayer;
            Awareness = awareness;
            IsBackstage = isBackstage;
        }
    }
}
