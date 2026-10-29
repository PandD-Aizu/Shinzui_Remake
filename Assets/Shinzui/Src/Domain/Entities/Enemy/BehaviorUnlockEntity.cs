using System;
using Shinzui.Domain.ValueObjects.Enemy;

namespace Shinzui.Domain.Entities.Enemy
{
    /// <summary>一つのランで共有する戦術回数と解放済み行動</summary>
    public sealed class BehaviorUnlockEntity
    {
        private readonly int[] _counts = new int[5];
        public EnemyBehaviorFlags Unlocked { get; private set; }
        public float CooldownRemaining { get; private set; }

        /// <summary>戦術の成立を記録</summary>
        /// <param name="kind">成立した戦術</param>
        public void Record(PlayerTacticKind kind) => _counts[(int)kind]++;

        /// <summary>戦術の累積回数を取得</summary>
        /// <param name="kind">戦術</param>
        /// <returns>累積回数</returns>
        public int Count(PlayerTacticKind kind) => _counts[(int)kind];

        /// <summary>解放間隔を進める</summary>
        /// <param name="deltaTime">ゲーム内の経過秒数</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f && !float.IsInfinity(deltaTime))
                CooldownRemaining = Math.Max(0f, CooldownRemaining - deltaTime);
        }

        /// <summary>解放間隔を満たす未解放の行動を追加</summary>
        /// <param name="flag">追加する行動</param>
        /// <param name="cooldown">次の解放までの秒数</param>
        /// <returns>新たに解放したか</returns>
        public bool TryUnlock(EnemyBehaviorFlags flag, float cooldown)
        {
            if (flag == EnemyBehaviorFlags.None || (Unlocked & flag) != 0 || CooldownRemaining > 0f) return false;
            Unlocked |= flag;
            CooldownRemaining = Math.Max(0f, cooldown);
            return true;
        }
    }
}
