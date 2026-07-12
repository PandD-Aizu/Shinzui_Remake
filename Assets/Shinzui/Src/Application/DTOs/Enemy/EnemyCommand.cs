using UnityEngine;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>
    /// 統括AIから個別Enemyへ渡される命令
    /// </summary>
    public readonly struct EnemyCommand
    {
        public EnemyCommand(EnemyCommandType type, Vector3 targetPosition)
        {
            Type = type;
            TargetPosition = targetPosition;
        }

        public EnemyCommandType Type { get; }
        public Vector3 TargetPosition { get; }

        public static EnemyCommand Idle => new(EnemyCommandType.Idle, Vector3.zero);
        public static EnemyCommand Wander => new(EnemyCommandType.Wander, Vector3.zero);

        /// <summary>
        /// プレイヤー追跡命令を作成する
        /// </summary>
        /// <param name="playerPosition">追跡対象のプレイヤー位置</param>
        /// <returns>プレイヤー追跡命令</returns>
        public static EnemyCommand ChasePlayer(Vector3 playerPosition)
        {
            return new EnemyCommand(EnemyCommandType.ChasePlayer, playerPosition);
        }

        /// <summary>
        /// 指定位置の調査命令を作成する
        /// </summary>
        /// <param name="targetPosition">調査対象位置</param>
        /// <returns>指定位置の調査命令</returns>
        public static EnemyCommand InvestigatePosition(Vector3 targetPosition)
        {
            return new EnemyCommand(EnemyCommandType.InvestigatePosition, targetPosition);
        }
    }
}
