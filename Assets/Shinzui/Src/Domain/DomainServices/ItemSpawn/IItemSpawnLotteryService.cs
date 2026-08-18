using System;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Domain.DomainServices.ItemSpawn
{
    /// <summary>
    /// アイテム候補のウェイト計算（BaseWeight * NeedWeight）および抽選を行うドメインサービス。
    /// </summary>
    public interface IItemSpawnLotteryService
    {
        float CalculateItemWeight(
            ItemSpawnTarget target,
            Func<ResourceCategory, float> needWeightProvider);

        IReadOnlyDictionary<string, float> CalculateCandidateWeights(
            IReadOnlyList<ItemSpawnTarget> targets,
            Func<ResourceCategory, float> needWeightProvider);

        bool TrySelectRandomCandidate(
            IReadOnlyList<ItemSpawnTarget> allTargets,
            IReadOnlyDictionary<string, int> currentItemCounts,
            IReadOnlyDictionary<ResourceCategory, int> currentCategoryCounts,
            int remainingBudget,
            Func<ResourceCategory, float> needWeightProvider,
            ISpawnPrng prng,
            out ItemSpawnTarget selectedTarget,
            out float baseWeight,
            out float needWeight,
            out float finalWeight,
            out string failureReason);
    }
}
