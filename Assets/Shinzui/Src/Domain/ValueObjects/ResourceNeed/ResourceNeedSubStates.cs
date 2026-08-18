using System;
using System.Collections.Generic;

namespace Shinzui.Domain.ValueObjects.ResourceNeed
{
    /// <summary>
    /// プレイヤーのHP状態（未実装システムでも安全に扱える構造）
    /// </summary>
    public record PlayerHpState
    {
        public bool HasHp { get; init; }
        public float CurrentHp { get; init; }
        public float MaxHp { get; init; }

        public float HpRatio => HasHp && MaxHp > 0f
            ? Math.Clamp(CurrentHp / MaxHp, 0f, 1f)
            : 1.0f;

        public PlayerHpState()
        {
            HasHp = false;
            CurrentHp = 100f;
            MaxHp = 100f;
        }

        public PlayerHpState(float currentHp, float maxHp)
        {
            HasHp = true;
            CurrentHp = Math.Max(0f, currentHp);
            MaxHp = Math.Max(1f, maxHp);
        }

        public static PlayerHpState Unavailable => new() { HasHp = false, CurrentHp = 100f, MaxHp = 100f };
    }

    /// <summary>
    /// 回復アイテムの所持状態
    /// </summary>
    public record HealingResourceState
    {
        public int HealingItemCount { get; init; }
        public float EstimatedHealPoints { get; init; }

        public HealingResourceState(int healingItemCount = 0, float estimatedHealPoints = 0f)
        {
            HealingItemCount = Math.Max(0, healingItemCount);
            EstimatedHealPoints = Math.Max(0f, estimatedHealPoints);
        }

        public static HealingResourceState Empty => new(0, 0f);
    }

    /// <summary>
    /// 懐中電灯・バッテリーの所持・チャージ状態
    /// </summary>
    public record LightResourceState
    {
        public bool HasFlashlight { get; init; }
        public float StrobeCharge { get; init; } // 0.0 .. 1.0
        public int BatteryCount { get; init; }

        public LightResourceState(bool hasFlashlight = true, float strobeCharge = 0f, int batteryCount = 0)
        {
            HasFlashlight = hasFlashlight;
            StrobeCharge = Math.Clamp(strobeCharge, 0f, 1f);
            BatteryCount = Math.Max(0, batteryCount);
        }

        public static LightResourceState Empty => new(true, 0f, 0);
    }

    /// <summary>
    /// 各武器の装填弾・予備弾薬情報
    /// </summary>
    public record WeaponAmmoInfo
    {
        public string WeaponId { get; init; }
        public int LoadedAmmo { get; init; }
        public int MaxLoadedAmmo { get; init; }
        public int ReserveAmmo { get; init; }
        public int TargetReserveAmmo { get; init; }

        public float LoadedRatio => MaxLoadedAmmo > 0
            ? Math.Clamp((float)LoadedAmmo / MaxLoadedAmmo, 0f, 1f)
            : 1.0f;

        public float ReserveRatio => TargetReserveAmmo > 0
            ? Math.Clamp((float)ReserveAmmo / TargetReserveAmmo, 0f, 1f)
            : 1.0f;

        public WeaponAmmoInfo(string weaponId, int loadedAmmo, int maxLoadedAmmo, int reserveAmmo, int targetReserveAmmo = 30)
        {
            WeaponId = weaponId ?? string.Empty;
            LoadedAmmo = Math.Max(0, loadedAmmo);
            MaxLoadedAmmo = Math.Max(0, maxLoadedAmmo);
            ReserveAmmo = Math.Max(0, reserveAmmo);
            TargetReserveAmmo = Math.Max(0, targetReserveAmmo);
        }
    }

    /// <summary>
    /// 弾薬リソース全般の所持状態
    /// </summary>
    public record AmmoResourceState
    {
        public IReadOnlyList<WeaponAmmoInfo> WeaponAmmoList { get; init; }
        public int UnassignedAmmoCount { get; init; }
        public int TotalLoadedAmmo { get; }
        public int TotalReserveAmmo { get; }

        public bool HasAnyWeapon => WeaponAmmoList != null && WeaponAmmoList.Count > 0;

        public AmmoResourceState(IEnumerable<WeaponAmmoInfo> weaponAmmoList = null, int unassignedAmmoCount = 0)
        {
            if (weaponAmmoList == null)
            {
                WeaponAmmoList = Array.Empty<WeaponAmmoInfo>();
            }
            else
            {
                // 外部からのリスト変更を防ぐためのディフェンシブコピー
                var list = new List<WeaponAmmoInfo>(weaponAmmoList);
                WeaponAmmoList = list.AsReadOnly();
            }
            UnassignedAmmoCount = Math.Max(0, unassignedAmmoCount);

            int loaded = 0;
            int reserve = UnassignedAmmoCount;
            for (int i = 0; i < WeaponAmmoList.Count; i++)
            {
                loaded += WeaponAmmoList[i].LoadedAmmo;
                reserve += WeaponAmmoList[i].ReserveAmmo;
            }
            TotalLoadedAmmo = loaded;
            TotalReserveAmmo = reserve;
        }

        public static AmmoResourceState Empty => new(Array.Empty<WeaponAmmoInfo>(), 0);
    }

    /// <summary>
    /// 武器本体の所持状態
    /// </summary>
    public record WeaponResourceState
    {
        public int OwnedWeaponCount { get; init; }
        public IReadOnlyList<string> OwnedWeaponIds { get; init; }

        public WeaponResourceState(int ownedWeaponCount = 0, IEnumerable<string> ownedWeaponIds = null)
        {
            OwnedWeaponCount = Math.Max(0, ownedWeaponCount);
            if (ownedWeaponIds == null)
            {
                OwnedWeaponIds = Array.Empty<string>();
            }
            else
            {
                // 外部からのリスト変更を防ぐためのディフェンシブコピー
                var list = new List<string>(ownedWeaponIds);
                OwnedWeaponIds = list.AsReadOnly();
            }
        }

        public static WeaponResourceState Empty => new(0, Array.Empty<string>());
    }

    /// <summary>
    /// ユーティリティアイテム（スタミナ剤、石、道具など）の所持状態
    /// </summary>
    public record UtilityResourceState
    {
        public int UtilityItemCount { get; init; }

        public UtilityResourceState(int utilityItemCount = 0)
        {
            UtilityItemCount = Math.Max(0, utilityItemCount);
        }

        public static UtilityResourceState Empty => new(0);
    }

    /// <summary>
    /// 希少・特殊アイテムの所持状態
    /// </summary>
    public record RareResourceState
    {
        public int RareItemCount { get; init; }

        public RareResourceState(int rareItemCount = 0)
        {
            RareItemCount = Math.Max(0, rareItemCount);
        }

        public static RareResourceState Empty => new(0);
    }
}
