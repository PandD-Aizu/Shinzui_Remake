using System;

namespace Shinzui.Domain.Entities.Enemy
{
    /// <summary>遭遇の緊張度と舞台裏の最短滞在時間を管理</summary>
    public sealed class MenaceGauge
    {
        private float _remaining;
        public float Value { get; private set; }
        public bool IsRecovering { get; private set; }
        public bool CanEmerge => IsRecovering && _remaining <= 0f && Value <= .3f;

        /// <summary>緊張度と休息時間を更新</summary>
        /// <param name="rate">毎秒の増減量</param>
        /// <param name="deltaTime">経過秒数</param>
        public void Tick(float rate, float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            Value = Math.Max(0f, Math.Min(1f, Value + rate * deltaTime));
            if (IsRecovering) _remaining = Math.Max(0f, _remaining - deltaTime);
        }

        /// <summary>新たに退避した敵の最低待機時間も保証</summary>
        /// <param name="minimumDuration">この敵の最低滞在秒数</param>
        public void BeginBackstage(float minimumDuration)
        {
            IsRecovering = true;
            _remaining = Math.Max(_remaining, minimumDuration);
        }

        /// <summary>全ての退避が終了した時に休息状態を解除</summary>
        public void EndBackstage() { IsRecovering = false; _remaining = 0f; }
    }
}
