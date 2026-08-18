using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Shinzui.Domain.ValueObjects.ResourceNeed
{
    public record PlayerNeedWeightEvaluationConfig
    {
        // 充足目標量
        public int TargetHealingItems { get; init; } = 3;
        public int TargetBatteries { get; init; } = 4;
        public int TargetWeapons { get; init; } = 2;
        public int TargetUtilityItems { get; init; } = 5;
        public int TargetRareItems { get; init; } = 2;
        public int DefaultTargetReserveAmmoPerWeapon { get; init; } = 30;

        // 重み付け係数
        public float HealingHpWeight { get; init; } = 0.6f;
        public float HealingItemWeight { get; init; } = 0.4f;

        public float LightChargeWeight { get; init; } = 0.5f;
        public float LightBatteryWeight { get; init; } = 0.5f;

        public float AmmoLoadedWeight { get; init; } = 0.5f;
        public float AmmoReserveWeight { get; init; } = 0.5f;
        public float NoWeaponDefaultAmmoRatio { get; init; } = 1.0f; // 武器未所持時に弾薬不足を過剰増幅させないための充足率 (1.0 = 不足なし)

        // カテゴリ別カーブ・クランプ設定
        public IReadOnlyDictionary<ResourceCategory, NeedWeightCategoryConfig> CategoryConfigs { get; init; }

        public PlayerNeedWeightEvaluationConfig(
            int targetHealingItems = 3,
            int targetBatteries = 4,
            int targetWeapons = 2,
            int targetUtilityItems = 5,
            int targetRareItems = 2,
            int defaultTargetReserveAmmoPerWeapon = 30,
            float healingHpWeight = 0.6f,
            float healingItemWeight = 0.4f,
            float lightChargeWeight = 0.5f,
            float lightBatteryWeight = 0.5f,
            float ammoLoadedWeight = 0.5f,
            float ammoReserveWeight = 0.5f,
            float noWeaponDefaultAmmoRatio = 1.0f,
            IReadOnlyDictionary<ResourceCategory, NeedWeightCategoryConfig> categoryConfigs = null)
        {
            TargetHealingItems = Math.Max(1, targetHealingItems);
            TargetBatteries = Math.Max(1, targetBatteries);
            TargetWeapons = Math.Max(1, targetWeapons);
            TargetUtilityItems = Math.Max(1, targetUtilityItems);
            TargetRareItems = Math.Max(1, targetRareItems);
            DefaultTargetReserveAmmoPerWeapon = Math.Max(1, defaultTargetReserveAmmoPerWeapon);

            HealingHpWeight = Math.Max(0f, healingHpWeight);
            HealingItemWeight = Math.Max(0f, healingItemWeight);

            LightChargeWeight = Math.Max(0f, lightChargeWeight);
            LightBatteryWeight = Math.Max(0f, lightBatteryWeight);

            AmmoLoadedWeight = Math.Max(0f, ammoLoadedWeight);
            AmmoReserveWeight = Math.Max(0f, ammoReserveWeight);
            NoWeaponDefaultAmmoRatio = Math.Clamp(noWeaponDefaultAmmoRatio, 0f, 1f);

            var dict = new Dictionary<ResourceCategory, NeedWeightCategoryConfig>();
            if (categoryConfigs != null)
            {
                foreach (var kvp in categoryConfigs)
                {
                    dict[kvp.Key] = kvp.Value;
                }
            }

            foreach (ResourceCategory cat in Enum.GetValues(typeof(ResourceCategory)))
            {
                if (!dict.ContainsKey(cat))
                {
                    dict[cat] = new NeedWeightCategoryConfig(cat);
                }
            }

            CategoryConfigs = new ReadOnlyDictionary<ResourceCategory, NeedWeightCategoryConfig>(dict);
        }

        public NeedWeightCategoryConfig GetCategoryConfig(ResourceCategory category)
        {
            if (CategoryConfigs != null && CategoryConfigs.TryGetValue(category, out var config))
            {
                return config;
            }
            return new NeedWeightCategoryConfig(category);
        }

        public static PlayerNeedWeightEvaluationConfig CreateDefault()
        {
            return new PlayerNeedWeightEvaluationConfig();
        }
    }
}
