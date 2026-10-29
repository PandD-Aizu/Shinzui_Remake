using System;
using UnityEngine;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>
    /// 統括AIがゲーム全体を判断するための状態
    /// </summary>
    public readonly struct EnemyWorldState
    {
        /// <summary>統括側だけが利用する全知の状態と出入り地点の検証を保持</summary>
        /// <param name="playerPosition">プレイヤー位置</param>
        /// <param name="enemyReports">敵の報告</param>
        /// <param name="hasTunnelBounds">ループ境界があるか</param>
        /// <param name="tunnelStart">トンネル始点</param>
        /// <param name="tunnelEnd">トンネル終点</param>
        /// <param name="playerNoiseRadius">旧ノイズ半径</param>
        /// <param name="isVisibleToPlayer">候補の視認判定</param>
        /// <param name="canReachExit">敵ごとの経路判定</param>
        public EnemyWorldState(
            Vector3 playerPosition,
            ReadOnlyMemory<EnemyReport> enemyReports,
            bool hasTunnelBounds,
            Vector3 tunnelStart,
            Vector3 tunnelEnd,
            float playerNoiseRadius = 0f, Func<Vector3, bool> isVisibleToPlayer = null, Func<int, Vector3, bool> canReachExit = null)
        {
            CanReachExit = canReachExit;
            IsVisibleToPlayer = isVisibleToPlayer;
            PlayerPosition = playerPosition;
            EnemyReports = enemyReports;
            HasTunnelBounds = hasTunnelBounds;
            TunnelStart = tunnelStart;
            TunnelEnd = tunnelEnd;
            PlayerNoiseRadius = playerNoiseRadius;
        }

        public Func<int, Vector3, bool> CanReachExit { get; }
        public Func<Vector3, bool> IsVisibleToPlayer { get; }
        public Vector3 PlayerPosition { get; }
        public ReadOnlyMemory<EnemyReport> EnemyReports { get; }
        public bool HasTunnelBounds { get; }
        public Vector3 TunnelStart { get; }
        public Vector3 TunnelEnd { get; }
        public float PlayerNoiseRadius { get; }
    }
}
