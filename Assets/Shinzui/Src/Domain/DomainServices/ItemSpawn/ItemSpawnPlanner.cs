using System;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Domain.DomainServices.ItemSpawn
{
    /// <summary>
    /// アイテムスポーン生成計画を決定論的に実行するドメインプランナー実装。
    /// 最低保証を先行処理し、残予算と制限の範囲内で通常抽選・位置サンプリング・配置バリデーションを行う。
    /// </summary>
    public sealed class ItemSpawnPlanner : IItemSpawnPlanner
    {
        private readonly IItemSpawnLotteryService _lotteryService;
        private readonly ISurfaceSamplerService _surfaceSampler;
        private readonly ISpawnPlacementValidator _defaultValidator;

        public ItemSpawnPlanner(
            IItemSpawnLotteryService lotteryService = null,
            ISurfaceSamplerService surfaceSampler = null,
            ISpawnPlacementValidator defaultValidator = null)
        {
            _lotteryService = lotteryService ?? new ItemSpawnLotteryService();
            _surfaceSampler = surfaceSampler ?? new SurfaceSamplerService();
            _defaultValidator = defaultValidator ?? new DomainDistancePlacementValidator();
        }

        public ItemSpawnRunResult PlanSpawns(
            ItemSpawnRunConfig config,
            IReadOnlyList<ItemSpawnTarget> targets,
            IReadOnlyList<SpawnSurfaceData> surfaces,
            Func<ResourceCategory, float> needWeightProvider = null,
            ISpawnPlacementValidator customValidator = null)
        {
            config ??= new ItemSpawnRunConfig(0);
            targets ??= Array.Empty<ItemSpawnTarget>();
            surfaces ??= Array.Empty<SpawnSurfaceData>();

            var skipReasons = new List<string>();
            var spawnedItems = new List<SpawnedItemRecord>();
            var itemCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var categoryCounts = new Dictionary<ResourceCategory, int>();

            var prng = new DeterministicPrng(config.Seed);

            // 1. サーフェス三角形の収集と面積計算
            var validTriangles = _surfaceSampler.CollectValidTriangles(
                surfaces,
                out int totalTrianglesCount,
                out float totalValidArea);

            if (validTriangles.Count == 0)
            {
                skipReasons.Add("No valid spawn surface triangles available.");
                return new ItemSpawnRunResult(
                    spawnedItems,
                    0,
                    config.Budget,
                    0,
                    skipReasons,
                    new Dictionary<string, float>(),
                    totalTrianglesCount,
                    0,
                    0f,
                    false);
            }

            // 2. アイテム候補マップの構築とスナップショット記録
            var targetMap = new Dictionary<string, ItemSpawnTarget>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null && !string.IsNullOrEmpty(targets[i].Id))
                {
                    targetMap[targets[i].Id] = targets[i];
                }
            }

            var candidateWeightsSnapshot = _lotteryService.CalculateCandidateWeights(targets, needWeightProvider);

            int remainingBudget = config.Budget;
            int totalCostSpent = 0;
            int failedPlacementAttempts = 0;
            int totalPlacementAttempts = 0;

            ISpawnPlacementValidator validator = customValidator ?? _defaultValidator;

            // 3. 最低保証（Guaranteed Rules）の先行処理
            if (config.GuaranteedRules != null && config.GuaranteedRules.Count > 0)
            {
                for (int r = 0; r < config.GuaranteedRules.Count; r++)
                {
                    GuaranteedSpawnRule rule = config.GuaranteedRules[r];
                    if (rule == null || string.IsNullOrEmpty(rule.ItemId)) continue;

                    if (!targetMap.TryGetValue(rule.ItemId, out ItemSpawnTarget target))
                    {
                        skipReasons.Add($"Guaranteed rule skipped: Target item '{rule.ItemId}' not defined.");
                        continue;
                    }

                    for (int count = 0; count < rule.Count; count++)
                    {
                        if (spawnedItems.Count >= config.MaxTotalSpawns)
                        {
                            skipReasons.Add($"Guaranteed item '{target.Id}' stopped: MaxTotalSpawns ({config.MaxTotalSpawns}) reached.");
                            break;
                        }

                        if (target.SpawnCost > remainingBudget)
                        {
                            skipReasons.Add($"Guaranteed item '{target.Id}' skipped: Cost ({target.SpawnCost}) exceeds remaining budget ({remainingBudget}).");
                            break;
                        }

                        itemCounts.TryGetValue(target.Id, out int curItemCount);
                        if (curItemCount >= target.MaxCount)
                        {
                            skipReasons.Add($"Guaranteed item '{target.Id}' skipped: Item MaxCount ({target.MaxCount}) reached.");
                            break;
                        }

                        categoryCounts.TryGetValue(target.Category, out int curCatCount);
                        if (curCatCount >= target.CategoryMaxCount)
                        {
                            skipReasons.Add($"Guaranteed item '{target.Id}' skipped: Category MaxCount ({target.CategoryMaxCount}) reached.");
                            break;
                        }

                        // 配置試行
                        bool placed = TryPlaceItem(
                            target,
                            surfaces,
                            validTriangles,
                            prng,
                            config,
                            validator,
                            spawnedItems,
                            needWeightProvider,
                            out SpawnedItemRecord record,
                            ref failedPlacementAttempts,
                            ref totalPlacementAttempts);

                        if (placed)
                        {
                            spawnedItems.Add(record);
                            remainingBudget -= target.SpawnCost;
                            totalCostSpent += target.SpawnCost;
                            itemCounts[target.Id] = curItemCount + 1;
                            categoryCounts[target.Category] = curCatCount + 1;
                        }
                        else
                        {
                            skipReasons.Add($"Guaranteed item '{target.Id}' skipped: Placement validation failed after {config.MaxPlacementAttemptsPerItem} attempts.");
                        }
                    }
                }
            }

            // 4. 通常ランダム抽選ループ（通常対象外フラグ・予算・上限・ウェイト0を除外）
            while (spawnedItems.Count < config.MaxTotalSpawns &&
                   remainingBudget > 0 &&
                   totalPlacementAttempts < config.MaxTotalPlacementAttempts)
            {
                bool hasCandidate = _lotteryService.TrySelectRandomCandidate(
                    targets,
                    itemCounts,
                    categoryCounts,
                    remainingBudget,
                    needWeightProvider,
                    prng,
                    out ItemSpawnTarget selectedTarget,
                    out float baseWeight,
                    out float needWeight,
                    out float finalWeight,
                    out string selectFailReason);

                if (!hasCandidate)
                {
                    if (!string.IsNullOrEmpty(selectFailReason))
                    {
                        skipReasons.Add($"Random lottery terminated: {selectFailReason}");
                    }
                    break;
                }

                // 配置試行
                itemCounts.TryGetValue(selectedTarget.Id, out int currentItemCount);
                categoryCounts.TryGetValue(selectedTarget.Category, out int currentCatCount);

                bool placed = TryPlaceItem(
                    selectedTarget,
                    surfaces,
                    validTriangles,
                    prng,
                    config,
                    validator,
                    spawnedItems,
                    needWeightProvider,
                    out SpawnedItemRecord spawnedRecord,
                    ref failedPlacementAttempts,
                    ref totalPlacementAttempts);

                if (placed)
                {
                    spawnedItems.Add(spawnedRecord);
                    remainingBudget -= selectedTarget.SpawnCost;
                    totalCostSpent += selectedTarget.SpawnCost;
                    itemCounts[selectedTarget.Id] = currentItemCount + 1;
                    categoryCounts[selectedTarget.Category] = currentCatCount + 1;
                }
                else
                {
                    skipReasons.Add($"Item '{selectedTarget.Id}' placement skipped: Failed after {config.MaxPlacementAttemptsPerItem} attempts.");
                    if (totalPlacementAttempts >= config.MaxTotalPlacementAttempts)
                    {
                        skipReasons.Add($"Lottery stopped: Max total placement attempts ({config.MaxTotalPlacementAttempts}) reached.");
                        break;
                    }
                }
            }

            bool hasReachedMax = spawnedItems.Count >= config.MaxTotalSpawns;

            return new ItemSpawnRunResult(
                spawnedItems,
                totalCostSpent,
                remainingBudget,
                failedPlacementAttempts,
                skipReasons,
                candidateWeightsSnapshot,
                totalTrianglesCount,
                validTriangles.Count,
                totalValidArea,
                hasReachedMax);
        }

        private bool TryPlaceItem(
            ItemSpawnTarget target,
            IReadOnlyList<SpawnSurfaceData> surfaces,
            IReadOnlyList<SpawnSurfaceTriangle> validTriangles,
            ISpawnPrng prng,
            ItemSpawnRunConfig config,
            ISpawnPlacementValidator validator,
            IReadOnlyList<SpawnedItemRecord> alreadySpawned,
            Func<ResourceCategory, float> needWeightProvider,
            out SpawnedItemRecord record,
            ref int failedPlacementAttempts,
            ref int totalPlacementAttempts)
        {
            record = null;

            int attempts = 0;
            while (attempts < config.MaxPlacementAttemptsPerItem && totalPlacementAttempts < config.MaxTotalPlacementAttempts)
            {
                attempts++;
                totalPlacementAttempts++;

                if (!_surfaceSampler.TrySampleCandidatePoint(
                    surfaces,
                    validTriangles,
                    prng,
                    out SpawnVector3 surfacePoint,
                    out SpawnVector3 normal,
                    out SpawnSurfaceTriangle triangle,
                    out float sampledWeight))
                {
                    failedPlacementAttempts++;
                    continue;
                }

                SpawnVector3 spawnPosition = surfacePoint + (normal * target.SurfaceOffset);

                if (validator != null && !validator.Validate(spawnPosition, normal, target, alreadySpawned, config, out _))
                {
                    failedPlacementAttempts++;
                    continue;
                }

                // 配置成功：回転（Yaw角）決定
                float yawDegrees = prng.NextFloat(0f, 360f);
                float needWeight = 1.0f;
                if (needWeightProvider != null)
                {
                    needWeight = Math.Max(0f, needWeightProvider(target.Category));
                }
                float finalItemWeight = target.BaseWeight * needWeight;

                record = new SpawnedItemRecord(
                    target.Id,
                    spawnPosition,
                    normal,
                    yawDegrees,
                    target.RotationPolicy,
                    triangle.SurfaceId,
                    triangle.TriangleIndex,
                    config.Seed,
                    target.SpawnCost,
                    target.BaseWeight,
                    needWeight,
                    finalItemWeight,
                    sampledWeight);

                return true;
            }

            return false;
        }
    }
}
