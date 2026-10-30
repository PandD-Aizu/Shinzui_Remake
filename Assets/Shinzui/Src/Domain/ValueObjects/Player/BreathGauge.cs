using System;

namespace Shinzui.Domain.ValueObjects.Player
{
    /// <summary>息止め四秒、半速回復、枯渇後二秒の再使用待ち</summary>
    public sealed class BreathGauge
    {
        public float Remaining { get; private set; } = 4f;
        public float Cooldown { get; private set; }
        public bool IsHolding { get; private set; }
        /// <summary>呼吸を更新し枯渇の瞬間だけ通知</summary>
        /// <param name="held">息止め入力</param>
        /// <param name="dt">経過秒数</param>
        /// <returns>今回枯渇したか</returns>
        public bool Tick(bool held, float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return false;
            bool recovering = Cooldown > 0f;
            Cooldown = Math.Max(0f, Cooldown - dt);
            IsHolding = held && !recovering && Remaining > 0f;
            if (!IsHolding) { Remaining = Math.Min(4f, Remaining + dt * .5f); return false; }
            Remaining = Math.Max(0f, Remaining - dt);
            if (Remaining > 0f) return false;
            IsHolding = false;
            Cooldown = 2f;
            return true;
        }
    }
}
