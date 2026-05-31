namespace Shinzui.Domain.ValueObjects.Player
{
    public record PlayerCrouchStatus
    {
        public float standingHeight { get; init; } = 2.0f;   // 立ち状態の高さ
        public float crouchingHeight { get; init; } = 1.0f;  // しゃがみ時の高さ
        public float heightChangeRate { get; init; } = 5.0f; // しゃがみと立ちの高さの変化率
        
        public PlayerCrouchStatus() { }

        public PlayerCrouchStatus(
            float standingHeight,
            float crouchingHeight,
            float heightChangeRate)
        {
            this.standingHeight = standingHeight;
            this.crouchingHeight = crouchingHeight;
            this.heightChangeRate = heightChangeRate;
        }
    }
}