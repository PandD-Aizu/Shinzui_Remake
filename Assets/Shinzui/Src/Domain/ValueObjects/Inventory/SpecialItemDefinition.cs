namespace Shinzui.Domain.ValueObjects.Inventory
{
    /// <summary>
    /// 特殊アイテムがプレイヤーへ与える常時効果
    /// </summary>
    public record SpecialItemModifiers
    {
        public static SpecialItemModifiers None { get; } = new();

        public float MoveSpeedMultiplier { get; init; } = 1.0f;
        public float StaminaRecoveryMultiplier { get; init; } = 1.0f;
        public bool PreventDeathOnce { get; init; }

        public SpecialItemModifiers() { }

        public SpecialItemModifiers(float moveSpeedMultiplier, float staminaRecoveryMultiplier, bool preventDeathOnce)
        {
            MoveSpeedMultiplier = moveSpeedMultiplier;
            StaminaRecoveryMultiplier = staminaRecoveryMultiplier;
            PreventDeathOnce = preventDeathOnce;
        }
    }

    /// <summary>
    /// 通常アイテムの表示情報に、特殊アイテム専用の常時効果を加えた定義
    /// </summary>
    public record SpecialItemDefinition : ItemDefinition
    {
        public SpecialItemModifiers Modifiers { get; init; } = SpecialItemModifiers.None;

        public SpecialItemDefinition() { }

        public SpecialItemDefinition(
            string id,
            string name,
            string description,
            string iconAssetAddress,
            SpecialItemModifiers modifiers)
            : base(id, name, description, iconAssetAddress, ItemType.Special, 1)
        {
            Modifiers = modifiers ?? SpecialItemModifiers.None;
        }
    }
}
