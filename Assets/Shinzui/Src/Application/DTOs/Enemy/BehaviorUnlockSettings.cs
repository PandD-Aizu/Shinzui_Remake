using System;
using Shinzui.Domain.ValueObjects.Enemy;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>戦術回数と解放する行動の対応</summary>
    [Serializable]
    public sealed class BehaviorUnlockRule
    {
        public PlayerTacticKind tactic;
        public int count;
        public EnemyBehaviorFlags behavior;

        /// <summary>解放条件を作成</summary>
        /// <param name="tactic">対象戦術</param>
        /// <param name="count">必要回数</param>
        /// <param name="behavior">解放行動</param>
        public BehaviorUnlockRule(PlayerTacticKind tactic, int count, EnemyBehaviorFlags behavior)
        { this.tactic = tactic; this.count = count; this.behavior = behavior; }
    }

    /// <summary>Inspectorで調整する学習条件</summary>
    [Serializable]
    public sealed class BehaviorUnlockSettings
    {
        public float unlockCooldown = 60f;
        public float sneakDistance = 8f;
        public float sneakDuration = 10f;
        public int ambushFloor = 2;
        public BehaviorUnlockRule[] rules =
        {
            new(PlayerTacticKind.SneakNearEnemy, 3, EnemyBehaviorFlags.SlowStalk),
            new(PlayerTacticKind.DecoyThrow, 3, EnemyBehaviorFlags.DecoyWariness),
            new(PlayerTacticKind.StrobeHitEnemy, 3, EnemyBehaviorFlags.StrobeResistance),
            new(PlayerTacticKind.EscapedSearch, 2, EnemyBehaviorFlags.SearchSideRooms),
            new(PlayerTacticKind.EscapedSearch, 3, EnemyBehaviorFlags.Backtrack),
            new(PlayerTacticKind.BrokeSightAtCorner, 3, EnemyBehaviorFlags.PredictiveChase)
        };
    }
}
