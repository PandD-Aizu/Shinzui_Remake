namespace Shinzui.Domain.ValueObjects.Player
{
    public record PlayerCrouchStatus
    {
        public float StandingHeight { get; init; } = 2.0f;   // 立ち状態の高さ
        public float CrouchRatio { get; init; } = 0.5f;      // しゃがみ時の高さ倍率（0.5 = 50%）
        public float HeightChangeRate { get; init; } = 10.0f; // しゃがみと立ちの高さの変化率

        public float CrouchingHeight => StandingHeight * CrouchRatio; // 計算後のしゃがみ高さ

        public PlayerCrouchStatus() { }

        public PlayerCrouchStatus(
            float standingHeight,
            float crouchRatio,
            float heightChangeRate)
        {
            this.StandingHeight = standingHeight;
            this.CrouchRatio = crouchRatio;
            this.HeightChangeRate = heightChangeRate;
        }
    }
}