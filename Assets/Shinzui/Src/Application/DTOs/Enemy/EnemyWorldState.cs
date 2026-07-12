using System;
using UnityEngine;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>
    /// 統括AIがゲーム全体を判断するための状態
    /// </summary>
    public readonly struct EnemyWorldState
    {
        public EnemyWorldState(
            Vector3 playerPosition,
            ReadOnlyMemory<EnemyReport> enemyReports,
            bool hasTunnelBounds,
            Vector3 tunnelStart,
            Vector3 tunnelEnd)
        {
            PlayerPosition = playerPosition;
            EnemyReports = enemyReports;
            HasTunnelBounds = hasTunnelBounds;
            TunnelStart = tunnelStart;
            TunnelEnd = tunnelEnd;
        }

        public Vector3 PlayerPosition { get; }
        public ReadOnlyMemory<EnemyReport> EnemyReports { get; }
        public bool HasTunnelBounds { get; }
        public Vector3 TunnelStart { get; }
        public Vector3 TunnelEnd { get; }
    }
}
