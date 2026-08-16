using System;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Domain.DomainServices.ResourceNeed
{
    /// <summary>
    /// プレイヤースナップショットと設定から充足率およびNeed Weightを計算するドメインサービス
    /// </summary>
    public class PlayerResourceNeedEvaluator
    {
        public PlayerResourceNeedEvaluationResult Evaluate(
            PlayerResourceSnapshot snapshot,
            PlayerNeedWeightEvaluationConfig config)
        {
            if (snapshot == null) snapshot = PlayerResourceSnapshot.Empty;
            if (config == null) config = PlayerNeedWeightEvaluationConfig.CreateDefault();

            // 1. Healing Ratio 計算
            float healingRatio = CalculateHealingRatio(snapshot, config);

            // 2. Ammo Ratio 計算
            float ammoRatio = CalculateAmmoRatio(snapshot, config);

            // 3. Light Ratio 計算
            float lightRatio = CalculateLightRatio(snapshot, config);

            // 4. Weapon Ratio 計算
            float weaponRatio = CalculateWeaponRatio(snapshot, config);

            // 5. Utility Ratio 計算
            float utilityRatio = CalculateUtilityRatio(snapshot, config);

            // 6. Rare Ratio 計算
            float rareRatio = CalculateRareRatio(snapshot, config);

            var breakdown = new ResourceRatioBreakdown(
                healingRatio,
                ammoRatio,
                lightRatio,
                weaponRatio,
                utilityRatio,
                rareRatio
            );

            // 7. Ratio -> Need Weight 変換 (カーブ評価 + Min/Maxクランプ)
            var weights = new Dictionary<ResourceCategory, float>();
            foreach (ResourceCategory category in Enum.GetValues(typeof(ResourceCategory)))
            {
                var catConfig = config.GetCategoryConfig(category);
                float ratio = breakdown.GetRatio(category);
                float rawWeight = catConfig.Curve != null ? catConfig.Curve.Evaluate(ratio) : catConfig.DefaultWeight;
                float clampedWeight = Math.Clamp(rawWeight, catConfig.MinWeight, catConfig.MaxWeight);
                weights[category] = clampedWeight;
            }

            return new PlayerResourceNeedEvaluationResult(snapshot, breakdown, weights);
        }

        private float CalculateHealingRatio(PlayerResourceSnapshot snapshot, PlayerNeedWeightEvaluationConfig config)
        {
            float healItemRatio = Math.Clamp((float)snapshot.Healing.HealingItemCount / config.TargetHealingItems, 0f, 1f);

            if (snapshot.Hp.HasHp)
            {
                float totalWeight = config.HealingHpWeight + config.HealingItemWeight;
                if (totalWeight <= 0f) return healItemRatio;

                float hpPart = (config.HealingHpWeight / totalWeight) * snapshot.Hp.HpRatio;
                float itemPart = (config.HealingItemWeight / totalWeight) * healItemRatio;
                return Math.Clamp(hpPart + itemPart, 0f, 1f);
            }

            return healItemRatio;
        }

        private float CalculateAmmoRatio(PlayerResourceSnapshot snapshot, PlayerNeedWeightEvaluationConfig config)
        {
            var ammoState = snapshot.Ammo;
            if (!ammoState.HasAnyWeapon)
            {
                // 武器を持っていない場合は、弾薬不足を過剰増幅させない（安全なデフォルト充足率）
                return config.NoWeaponDefaultAmmoRatio;
            }

            float totalWeight = config.AmmoLoadedWeight + config.AmmoReserveWeight;
            if (totalWeight <= 0f) totalWeight = 1.0f;

            float sumRatio = 0f;
            int count = ammoState.WeaponAmmoList.Count;

            for (int i = 0; i < count; i++)
            {
                var weapon = ammoState.WeaponAmmoList[i];
                float loaded = weapon.LoadedRatio;
                float reserve = weapon.ReserveRatio;

                float weaponRatio = (config.AmmoLoadedWeight / totalWeight) * loaded + (config.AmmoReserveWeight / totalWeight) * reserve;
                sumRatio += Math.Clamp(weaponRatio, 0f, 1f);
            }

            return Math.Clamp(sumRatio / count, 0f, 1f);
        }

        private float CalculateLightRatio(PlayerResourceSnapshot snapshot, PlayerNeedWeightEvaluationConfig config)
        {
            float batteryRatio = Math.Clamp((float)snapshot.Light.BatteryCount / config.TargetBatteries, 0f, 1f);

            if (snapshot.Light.HasFlashlight)
            {
                float totalWeight = config.LightChargeWeight + config.LightBatteryWeight;
                if (totalWeight <= 0f) return batteryRatio;

                float chargeRatio = Math.Clamp(snapshot.Light.StrobeCharge, 0f, 1f);
                float chargePart = (config.LightChargeWeight / totalWeight) * chargeRatio;
                float batteryPart = (config.LightBatteryWeight / totalWeight) * batteryRatio;
                return Math.Clamp(chargePart + batteryPart, 0f, 1f);
            }

            return batteryRatio;
        }

        private float CalculateWeaponRatio(PlayerResourceSnapshot snapshot, PlayerNeedWeightEvaluationConfig config)
        {
            return Math.Clamp((float)snapshot.Weapons.OwnedWeaponCount / config.TargetWeapons, 0f, 1f);
        }

        private float CalculateUtilityRatio(PlayerResourceSnapshot snapshot, PlayerNeedWeightEvaluationConfig config)
        {
            return Math.Clamp((float)snapshot.Utility.UtilityItemCount / config.TargetUtilityItems, 0f, 1f);
        }

        private float CalculateRareRatio(PlayerResourceSnapshot snapshot, PlayerNeedWeightEvaluationConfig config)
        {
            return Math.Clamp((float)snapshot.Rare.RareItemCount / config.TargetRareItems, 0f, 1f);
        }
    }
}
