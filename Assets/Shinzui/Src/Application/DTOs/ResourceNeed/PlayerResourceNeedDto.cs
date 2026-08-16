using System;

namespace Shinzui.Application.DTOs.ResourceNeed
{
    public readonly struct PlayerResourceNeedDto
    {
        public DateTime Timestamp { get; }
        
        public float HealingNeedWeight { get; }
        public float AmmoNeedWeight { get; }
        public float LightNeedWeight { get; }
        public float WeaponNeedWeight { get; }
        public float UtilityNeedWeight { get; }
        public float RareNeedWeight { get; }

        public float HealingRatio { get; }
        public float AmmoRatio { get; }
        public float LightRatio { get; }
        public float WeaponRatio { get; }
        public float UtilityRatio { get; }
        public float RareRatio { get; }

        public float HpRatio { get; }
        public int HealingItemCount { get; }
        public float StrobeCharge { get; }
        public int BatteryCount { get; }
        public int OwnedWeaponCount { get; }
        public int UtilityItemCount { get; }
        public int RareItemCount { get; }
        public int TotalLoadedAmmo { get; }
        public int TotalReserveAmmo { get; }

        public PlayerResourceNeedDto(
            DateTime timestamp,
            float healingNeedWeight,
            float ammoNeedWeight,
            float lightNeedWeight,
            float weaponNeedWeight,
            float utilityNeedWeight,
            float rareNeedWeight,
            float healingRatio,
            float ammoRatio,
            float lightRatio,
            float weaponRatio,
            float utilityRatio,
            float rareRatio,
            float hpRatio,
            int healingItemCount,
            float strobeCharge,
            int batteryCount,
            int ownedWeaponCount,
            int utilityItemCount,
            int rareItemCount,
            int totalLoadedAmmo = 0,
            int totalReserveAmmo = 0)
        {
            Timestamp = timestamp;
            HealingNeedWeight = healingNeedWeight;
            AmmoNeedWeight = ammoNeedWeight;
            LightNeedWeight = lightNeedWeight;
            WeaponNeedWeight = weaponNeedWeight;
            UtilityNeedWeight = utilityNeedWeight;
            RareNeedWeight = rareNeedWeight;
            HealingRatio = healingRatio;
            AmmoRatio = ammoRatio;
            LightRatio = lightRatio;
            WeaponRatio = weaponRatio;
            UtilityRatio = utilityRatio;
            RareRatio = rareRatio;
            HpRatio = hpRatio;
            HealingItemCount = healingItemCount;
            StrobeCharge = strobeCharge;
            BatteryCount = batteryCount;
            OwnedWeaponCount = ownedWeaponCount;
            UtilityItemCount = utilityItemCount;
            RareItemCount = rareItemCount;
            TotalLoadedAmmo = totalLoadedAmmo;
            TotalReserveAmmo = totalReserveAmmo;
        }
    }
}
