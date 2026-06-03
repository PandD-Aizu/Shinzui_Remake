using UnityEngine;

namespace Shinzui.Domain.ValueObjects.Player
{
    public record PlayerSpeedStatus
    {
        public float MoveSpeed { get; init; } = 5.0f;                 // 基本の移動速度
        public float RunSpeedMultiplier { get; init; } = 2.0f;        // ダッシュ時の速度倍率
        public float CrouchSpeedMultiplier { get; init; } = 0.5f;     // しゃがみ時の速度倍率
        public Vector3 CurrentVelocity { get; init; } = Vector3.zero; // 現在の速度ベクトル
        public float SpeedChangeRate { get; init; } = 10.0f;          // 速度の変化率（加速/減速の速さ）

        public PlayerSpeedStatus() { }

        public PlayerSpeedStatus(
            float moveSpeed,
            float runSpeedMultiplier,
            float crouchSpeedMultiplier,
            Vector3 currentVelocity,
            float speedChangeRate)
        {
            MoveSpeed = moveSpeed;
            RunSpeedMultiplier = runSpeedMultiplier;
            CrouchSpeedMultiplier = crouchSpeedMultiplier;
            CurrentVelocity = currentVelocity;
            SpeedChangeRate = speedChangeRate;
        }
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}