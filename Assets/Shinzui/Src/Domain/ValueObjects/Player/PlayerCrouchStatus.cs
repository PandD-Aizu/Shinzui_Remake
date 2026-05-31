namespace Shinzui.Domain.ValueObjects.Player
{
    public record PlayerCrouchStatus
    {
        public float StandingHeight { get; init; } = 2.0f;   // 立ち状態の高さ
        public float CrouchingHeight { get; init; } = 1.0f;  // しゃがみ時の高さ
        public float HeightChangeRate { get; init; } = 5.0f; // しゃがみと立ちの高さの変化率
        
        public PlayerCrouchStatus() { }

        public PlayerCrouchStatus(
            float standingHeight,
            float crouchingHeight,
            float heightChangeRate)
        {
            this.StandingHeight = standingHeight;
            this.CrouchingHeight = crouchingHeight;
            this.HeightChangeRate = heightChangeRate;
        }
    }
}