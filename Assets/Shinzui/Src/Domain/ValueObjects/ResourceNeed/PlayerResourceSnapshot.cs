using System;

namespace Shinzui.Domain.ValueObjects.ResourceNeed
{
    /// <summary>
    /// 1回のスポーン生成中などに固定利用できるイミュータブルなプレイヤー資源スナップショット
    /// </summary>
    public record PlayerResourceSnapshot
    {
        public DateTime Timestamp { get; init; }
        public PlayerHpState Hp { get; init; }
        public HealingResourceState Healing { get; init; }
        public LightResourceState Light { get; init; }
        public AmmoResourceState Ammo { get; init; }
        public WeaponResourceState Weapons { get; init; }
        public UtilityResourceState Utility { get; init; }
        public RareResourceState Rare { get; init; }

        public PlayerResourceSnapshot(
            PlayerHpState hp = null,
            HealingResourceState healing = null,
            LightResourceState light = null,
            AmmoResourceState ammo = null,
            WeaponResourceState weapons = null,
            UtilityResourceState utility = null,
            RareResourceState rare = null,
            DateTime? timestamp = null)
        {
            Timestamp = timestamp ?? DateTime.UtcNow;
            Hp = hp ?? PlayerHpState.Unavailable;
            Healing = healing ?? HealingResourceState.Empty;
            Light = light ?? LightResourceState.Empty;
            Ammo = ammo ?? AmmoResourceState.Empty;
            Weapons = weapons ?? WeaponResourceState.Empty;
            Utility = utility ?? UtilityResourceState.Empty;
            Rare = rare ?? RareResourceState.Empty;
        }

        public static PlayerResourceSnapshot Empty => new();
    }
}
