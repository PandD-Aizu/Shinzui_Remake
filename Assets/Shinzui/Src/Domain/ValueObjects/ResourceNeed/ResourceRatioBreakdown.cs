using System;
using System.Collections.Generic;

namespace Shinzui.Domain.ValueObjects.ResourceNeed
{
    /// <summary>
    /// 各カテゴリの正規化された充足率（0.0: 完全不足 〜 1.0: 満たされている）
    /// </summary>
    public record ResourceRatioBreakdown
    {
        public float HealingRatio { get; init; }
        public float AmmoRatio { get; init; }
        public float LightRatio { get; init; }
        public float WeaponRatio { get; init; }
        public float UtilityRatio { get; init; }
        public float RareRatio { get; init; }

        public ResourceRatioBreakdown(
            float healingRatio,
            float ammoRatio,
            float lightRatio,
            float weaponRatio,
            float utilityRatio,
            float rareRatio)
        {
            HealingRatio = Math.Clamp(healingRatio, 0f, 1f);
            AmmoRatio = Math.Clamp(ammoRatio, 0f, 1f);
            LightRatio = Math.Clamp(lightRatio, 0f, 1f);
            WeaponRatio = Math.Clamp(weaponRatio, 0f, 1f);
            UtilityRatio = Math.Clamp(utilityRatio, 0f, 1f);
            RareRatio = Math.Clamp(rareRatio, 0f, 1f);
        }

        public float GetRatio(ResourceCategory category)
        {
            return category switch
            {
                ResourceCategory.Healing => HealingRatio,
                ResourceCategory.Ammo => AmmoRatio,
                ResourceCategory.LightResource => LightRatio,
                ResourceCategory.Weapon => WeaponRatio,
                ResourceCategory.Utility => UtilityRatio,
                ResourceCategory.Rare => RareRatio,
                _ => 1.0f
            };
        }

        public IReadOnlyDictionary<ResourceCategory, float> ToDictionary()
        {
            return new Dictionary<ResourceCategory, float>
            {
                { ResourceCategory.Healing, HealingRatio },
                { ResourceCategory.Ammo, AmmoRatio },
                { ResourceCategory.LightResource, LightRatio },
                { ResourceCategory.Weapon, WeaponRatio },
                { ResourceCategory.Utility, UtilityRatio },
                { ResourceCategory.Rare, RareRatio }
            };
        }
    }
}
