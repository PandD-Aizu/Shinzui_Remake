using System;
using Shinzui.Domain.ValueObjects.Enemy;

namespace Shinzui.Domain.Entities.Enemy
{
    /// <summary>視覚と聴覚の疑いを蓄積し、視線喪失後の警戒を保持</summary>
    public sealed class SuspicionMeter
    {
        private readonly float _decay;
        private readonly float _holdTime;
        private float _holdRemaining;
        private bool _sawThisFrame;
        public float Value { get; private set; }
        public EnemyAwarenessLevel Level => Value >= 1f ? EnemyAwarenessLevel.Engaged
            : Value >= .65f ? EnemyAwarenessLevel.Alerted
            : Value >= .3f ? EnemyAwarenessLevel.Suspicious : EnemyAwarenessLevel.Unaware;

        /// <summary>疑いの減衰と警戒保持時間を設定</summary>
        /// <param name="decayPerSecond">毎秒の減衰量</param>
        /// <param name="alertedHoldTime">視線喪失後の保持秒数</param>
        public SuspicionMeter(float decayPerSecond = .12f, float alertedHoldTime = 8f)
        {
            _decay = decayPerSecond;
            _holdTime = alertedHoldTime;
        }

        /// <summary>視覚刺激を加算</summary>
        /// <param name="amount">疑いの増分</param>
        public void AddSight(float amount)
        {
            if (amount <= 0f) return;
            _sawThisFrame = true;
            Value = Math.Min(1f, Value + amount);
        }

        /// <summary>音だけでは追跡確定に到達しないよう加算</summary>
        /// <param name="amount">疑いの増分</param>
        public void AddNoise(float amount)
        {
            if (amount <= 0f) return;
            Value = Math.Max(Value, Math.Min(.99f, Value + amount));
        }

        /// <summary>近距離の視認から追跡を確定</summary>
        public void ForceEngage()
        {
            _sawThisFrame = true;
            Value = 1f;
        }

        /// <summary>刺激の加算後に視線喪失と減衰を更新</summary>
        /// <param name="dt">経過秒数</param>
        /// <param name="hasStimulus">このフレームに刺激があったか</param>
        public void Tick(float dt, bool hasStimulus)
        {
            if (dt <= 0f) return;

            // 発見中は保持時間を更新し、視線喪失直後にAlertedへ移る
            if (Level == EnemyAwarenessLevel.Engaged)
            {
                _holdRemaining = _holdTime;
                if (!_sawThisFrame) Value = .99f;
            }

            // 保持終了までの時間を除いて疑いを減衰する
            float held = Math.Min(dt, _holdRemaining);
            _holdRemaining = Math.Max(0f, _holdRemaining - dt);
            if (!hasStimulus)
                Value = Math.Max(0f, Math.Max(held > 0f ? .65f : 0f, Value - _decay * held)
                    - _decay * (dt - held));
            _sawThisFrame = false;
        }
    }
}
