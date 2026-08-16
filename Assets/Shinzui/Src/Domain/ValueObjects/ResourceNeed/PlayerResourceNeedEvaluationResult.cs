using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Shinzui.Domain.ValueObjects.ResourceNeed
{
    /// <summary>
    /// リソース評価結果。各カテゴリのNeed Weight、充足率Ratio、デバッグ用元値を保持
    /// </summary>
    public record PlayerResourceNeedEvaluationResult
    {
        public PlayerResourceSnapshot Snapshot { get; init; }
        public ResourceRatioBreakdown Ratios { get; init; }
        public IReadOnlyDictionary<ResourceCategory, float> NeedWeights { get; init; }

        public PlayerResourceNeedEvaluationResult(
            PlayerResourceSnapshot snapshot,
            ResourceRatioBreakdown ratios,
            IDictionary<ResourceCategory, float> needWeights)
        {
            Snapshot = snapshot;
            Ratios = ratios;
            if (needWeights != null)
            {
                var dict = new Dictionary<ResourceCategory, float>(needWeights);
                NeedWeights = new ReadOnlyDictionary<ResourceCategory, float>(dict);
            }
            else
            {
                NeedWeights = new ReadOnlyDictionary<ResourceCategory, float>(new Dictionary<ResourceCategory, float>());
            }
        }

        public float GetNeedWeight(ResourceCategory category)
        {
            if (NeedWeights != null && NeedWeights.TryGetValue(category, out float weight))
            {
                return weight;
            }
            return 1.0f;
        }

        public float GetRatio(ResourceCategory category)
        {
            return Ratios?.GetRatio(category) ?? 1.0f;
        }

        public string GetDebugSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Player Resource Need Evaluation ===");
            sb.AppendLine($"Timestamp: {Snapshot?.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"[Healing] Ratio: {Ratios?.HealingRatio:F2} | Weight: {GetNeedWeight(ResourceCategory.Healing):F2} (HP Ratio: {Snapshot?.Hp?.HpRatio:F2}, HealItems: {Snapshot?.Healing?.HealingItemCount})");
            sb.AppendLine($"[Ammo]    Ratio: {Ratios?.AmmoRatio:F2} | Weight: {GetNeedWeight(ResourceCategory.Ammo):F2} (Weapons: {Snapshot?.Ammo?.WeaponAmmoList?.Count ?? 0}, LoadedAmmo: {Snapshot?.Ammo?.TotalLoadedAmmo ?? 0}, ReserveAmmo: {Snapshot?.Ammo?.TotalReserveAmmo ?? 0}, UnassignedAmmo: {Snapshot?.Ammo?.UnassignedAmmoCount ?? 0})");
            sb.AppendLine($"[Light]   Ratio: {Ratios?.LightRatio:F2} | Weight: {GetNeedWeight(ResourceCategory.LightResource):F2} (Strobe: {Snapshot?.Light?.StrobeCharge:F2}, Batteries: {Snapshot?.Light?.BatteryCount})");
            sb.AppendLine($"[Weapon]  Ratio: {Ratios?.WeaponRatio:F2} | Weight: {GetNeedWeight(ResourceCategory.Weapon):F2} (Owned: {Snapshot?.Weapons?.OwnedWeaponCount})");
            sb.AppendLine($"[Utility] Ratio: {Ratios?.UtilityRatio:F2} | Weight: {GetNeedWeight(ResourceCategory.Utility):F2} (Items: {Snapshot?.Utility?.UtilityItemCount})");
            sb.AppendLine($"[Rare]    Ratio: {Ratios?.RareRatio:F2} | Weight: {GetNeedWeight(ResourceCategory.Rare):F2} (Items: {Snapshot?.Rare?.RareItemCount})");
            return sb.ToString();
        }
    }
}
