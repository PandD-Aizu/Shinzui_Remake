namespace Shinzui.Domain.ValueObjects.Player
{
    public record PlayerStamina
    {
        public float CurrentStamina { get; init; } = 100.0f;
        public float MaxStamina { get; init; } = 100.0f;
        public float MinStamina { get; init; } = 0.0f;
        public float StaminaIncreaseRate { get; init; } = 10.0f;
        public float StaminaDecreaseRate { get; init; } = 15.0f;
        
        public PlayerStamina() { }

        public PlayerStamina(
            float currentStamina,
            float maxStamina,
            float minStamina,
            float staminaIncreaseRate,
            float staminaDecreaseRate)
        {
            CurrentStamina = currentStamina;
            MaxStamina = maxStamina;
            MinStamina = minStamina;
            StaminaIncreaseRate = staminaIncreaseRate;
            StaminaDecreaseRate = staminaDecreaseRate;
        }
    }
}