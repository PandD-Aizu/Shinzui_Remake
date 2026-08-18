using System;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Domain.DomainServices.ItemSpawn
{
    /// <summary>
    /// アイテム候補の抽選を行うドメインサービス実装。
    /// weight = BaseWeight * NeedWeight に基づき、残予算超過・最大数到達・通常対象外を除外した上で決定論的に抽選する。
    /// </summary>
    public sealed class ItemSpawnLotteryService : IItemSpawnLotteryService
    {
        public float CalculateItemWeight(
            ItemSpawnTarget target,
            Func<ResourceCategory, float> needWeightProvider)
        {
            if (target == null || target.BaseWeight <= 0f)
            {
                return 0f;
            }

            float needWeight = 1.0f;
            if (needWeightProvider != null)
            {
                float retrieved = needWeightProvider(target.Category);
                needWeight = Math.Max(0f, retrieved);
            }

            return target.BaseWeight * needWeight;
        }

        public IReadOnlyDictionary<string, float> CalculateCandidateWeights(
            IReadOnlyList<ItemSpawnTarget> targets,
            Func<ResourceCategory, float> needWeightProvider)
        {
            var dict = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (targets == null) return dict;

            foreach (var target in targets)
            {
                if (target == null) continue;
                float weight = CalculateItemWeight(target, needWeightProvider);
                dict[target.Id] = weight;
            }

            return dict;
        }

        public bool TrySelectRandomCandidate(
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
            out string failureReason)
        {
            selectedTarget = null;
            baseWeight = 0f;
            needWeight = 1.0f;
            finalWeight = 0f;
            failureReason = null;

            if (allTargets == null || allTargets.Count == 0)
            {
                failureReason = "No item spawn targets available.";
                return false;
            }

            if (remainingBudget <= 0)
            {
                failureReason = "Remaining budget is exhausted.";
                return false;
            }

            // 有効な候補を収集
            var eligibleTargets = new List<ItemSpawnTarget>(allTargets.Count);
            var eligibleFinalWeights = new List<float>(allTargets.Count);
            var eligibleNeedWeights = new List<float>(allTargets.Count);
            float totalWeight = 0f;

            for (int i = 0; i < allTargets.Count; i++)
            {
                ItemSpawnTarget target = allTargets[i];
                if (target == null) continue;

                // 通常ランダム抽選フラグの確認（キーアイテム等は除外）
                if (!target.IsNormalRandomCandidate) continue;

                // 予算超過の除外
                if (target.SpawnCost > remainingBudget) continue;

                // アイテム最大数超過の除外
                if (currentItemCounts != null &&
                    currentItemCounts.TryGetValue(target.Id, out int count) &&
                    count >= target.MaxCount)
                {
                    continue;
                }

                // カテゴリ最大数超過の除外
                if (currentCategoryCounts != null &&
                    currentCategoryCounts.TryGetValue(target.Category, out int catCount) &&
                    catCount >= target.CategoryMaxCount)
                {
                    continue;
                }

                float targetNeedWeight = 1.0f;
                if (needWeightProvider != null)
                {
                    float retrieved = needWeightProvider(target.Category);
                    targetNeedWeight = Math.Max(0f, retrieved);
                }

                float calculatedWeight = target.BaseWeight * targetNeedWeight;
                if (calculatedWeight <= 1e-7f)
                {
                    continue; // ウェイト0は除外
                }

                eligibleTargets.Add(target);
                eligibleFinalWeights.Add(calculatedWeight);
                eligibleNeedWeights.Add(targetNeedWeight);
                totalWeight += calculatedWeight;
            }

            if (eligibleTargets.Count == 0 || totalWeight <= 1e-7f)
            {
                failureReason = "No eligible candidates meet budget, count, and weight criteria.";
                return false;
            }

            // 累積重みによるルーレット抽選
            double randomVal = prng.NextDouble() * totalWeight;
            double cumulative = 0.0;
            for (int i = 0; i < eligibleTargets.Count; i++)
            {
                cumulative += eligibleFinalWeights[i];
                if (randomVal <= cumulative || i == eligibleTargets.Count - 1)
                {
                    selectedTarget = eligibleTargets[i];
                    baseWeight = selectedTarget.BaseWeight;
                    needWeight = eligibleNeedWeights[i];
                    finalWeight = eligibleFinalWeights[i];
                    return true;
                }
            }

            // フォールバック
            selectedTarget = eligibleTargets[eligibleTargets.Count - 1];
            baseWeight = selectedTarget.BaseWeight;
            needWeight = eligibleNeedWeights[eligibleTargets.Count - 1];
            finalWeight = eligibleFinalWeights[eligibleTargets.Count - 1];
            return true;
        }
    }
}
