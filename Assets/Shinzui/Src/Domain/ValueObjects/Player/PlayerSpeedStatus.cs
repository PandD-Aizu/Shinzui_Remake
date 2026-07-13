using UnityEngine;

namespace Shinzui.Domain.ValueObjects.Player
{
    public record PlayerSpeedStatus
    {
        public float MoveSpeed { get; init; } = 2.0f;                 // 基本の移動速度
        public float RunSpeedMultiplier { get; init; } = 2.0f;        // ダッシュ時の速度倍率
        public float CrouchSpeedMultiplier { get; init; } = 0.5f;     // しゃがみ時の速度倍率
        public Vector3 CurrentVelocity { get; init; } = Vector3.zero; // 現在の速度ベクトル
        public float AccelerationRate { get; init; } = 8.0f;           // 加速度 (m/s^2)
        public float DecelerationRate { get; init; } = 10.0f;          // 制動力 (m/s^2)
        public float DirectionChangeDecelerationRate { get; init; } = 14.0f; // 急反転時の制動力 (m/s^2)
        public float StrafeSpeedMultiplier { get; init; } = 0.8f;     // 横移動時の速度倍率 (ペナルティ)
        public float BackwardSpeedMultiplier { get; init; } = 0.6f;   // 後退時の速度倍率 (ペナルティ)
        public float AirControlMultiplier { get; init; } = 0.25f;      // 空中での水平加減速倍率
        public float InputDeadZone { get; init; } = 0.12f;             // スティックのデッドゾーン
        public float ReversalDotThreshold { get; init; } = -0.35f;     // 急反転とみなす方向差
        public float GroundStickVelocity { get; init; } = -2.0f;       // 接地維持用の下向き速度

        public PlayerSpeedStatus() { }

        public PlayerSpeedStatus(
            float moveSpeed,
            float runSpeedMultiplier,
            float crouchSpeedMultiplier,
            Vector3 currentVelocity,
            float accelerationRate,
            float decelerationRate,
            float strafeSpeedMultiplier,
            float backwardSpeedMultiplier)
        {
            MoveSpeed = moveSpeed;
            RunSpeedMultiplier = runSpeedMultiplier;
            CrouchSpeedMultiplier = crouchSpeedMultiplier;
            CurrentVelocity = currentVelocity;
            AccelerationRate = accelerationRate;
            DecelerationRate = decelerationRate;
            StrafeSpeedMultiplier = strafeSpeedMultiplier;
            BackwardSpeedMultiplier = backwardSpeedMultiplier;
        }
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}
