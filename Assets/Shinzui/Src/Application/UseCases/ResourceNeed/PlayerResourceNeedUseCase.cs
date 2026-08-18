using Shinzui.Application.DTOs.ResourceNeed;
using Shinzui.Application.Interfaces.ResourceNeed;
using Shinzui.Domain.DomainServices.ResourceNeed;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Application.UseCases.ResourceNeed
{
    public class PlayerResourceNeedUseCase : IPlayerResourceNeedUseCase
    {
        private readonly IPlayerResourceSnapshotProvider _snapshotProvider;
        private readonly PlayerResourceNeedEvaluator _evaluator;
        private readonly PlayerNeedWeightEvaluationConfig _config;

        public PlayerResourceNeedUseCase(
            IPlayerResourceSnapshotProvider snapshotProvider,
            PlayerResourceNeedEvaluator evaluator,
            PlayerNeedWeightEvaluationConfig config = null)
        {
            _snapshotProvider = snapshotProvider;
            _evaluator = evaluator ?? new PlayerResourceNeedEvaluator();
            _config = config ?? PlayerNeedWeightEvaluationConfig.CreateDefault();
        }

        public PlayerResourceSnapshot CaptureSnapshot()
        {
            return _snapshotProvider.CaptureSnapshot();
        }

        public PlayerResourceNeedEvaluationResult Evaluate()
        {
            var snapshot = CaptureSnapshot();
            return Evaluate(snapshot);
        }

        public PlayerResourceNeedEvaluationResult Evaluate(PlayerResourceSnapshot snapshot)
        {
            return _evaluator.Evaluate(snapshot, _config);
        }

        public float GetNeedWeight(ResourceCategory category)
        {
            var result = Evaluate();
            return result.GetNeedWeight(category);
        }

        public float GetNeedWeight(ResourceCategory category, PlayerResourceSnapshot snapshot)
        {
            var result = Evaluate(snapshot);
            return result.GetNeedWeight(category);
        }

        public ResourceRatioBreakdown GetRatios()
        {
            var result = Evaluate();
            return result.Ratios;
        }

        public ResourceRatioBreakdown GetRatios(PlayerResourceSnapshot snapshot)
        {
            var result = Evaluate(snapshot);
            return result.Ratios;
        }

        public PlayerResourceNeedDto GetNeedDto()
        {
            var result = Evaluate();
            return ToDto(result);
        }

        public PlayerResourceNeedDto GetNeedDto(PlayerResourceSnapshot snapshot)
        {
            var result = Evaluate(snapshot);
            return ToDto(result);
        }

        private static PlayerResourceNeedDto ToDto(PlayerResourceNeedEvaluationResult result)
        {
            var s = result.Snapshot;
            var r = result.Ratios;
            return new PlayerResourceNeedDto(
                s?.Timestamp ?? System.DateTime.UtcNow,
                result.GetNeedWeight(ResourceCategory.Healing),
                result.GetNeedWeight(ResourceCategory.Ammo),
                result.GetNeedWeight(ResourceCategory.LightResource),
                result.GetNeedWeight(ResourceCategory.Weapon),
                result.GetNeedWeight(ResourceCategory.Utility),
                result.GetNeedWeight(ResourceCategory.Rare),
                r?.HealingRatio ?? 1f,
                r?.AmmoRatio ?? 1f,
                r?.LightRatio ?? 1f,
                r?.WeaponRatio ?? 1f,
                r?.UtilityRatio ?? 1f,
                r?.RareRatio ?? 1f,
                s?.Hp?.HpRatio ?? 1f,
                s?.Healing?.HealingItemCount ?? 0,
                s?.Light?.StrobeCharge ?? 0f,
                s?.Light?.BatteryCount ?? 0,
                s?.Weapons?.OwnedWeaponCount ?? 0,
                s?.Utility?.UtilityItemCount ?? 0,
                s?.Rare?.RareItemCount ?? 0,
                s?.Ammo?.TotalLoadedAmmo ?? 0,
                s?.Ammo?.TotalReserveAmmo ?? 0
            );
        }
    }
}
