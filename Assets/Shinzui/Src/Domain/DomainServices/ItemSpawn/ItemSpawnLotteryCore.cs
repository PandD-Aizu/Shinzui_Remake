using System;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;
using UnityEngine;

namespace Shinzui.Domain.DomainServices.ItemSpawn
{
    /// <summary>
    /// アイテム生成における抽選計算、サーフェスサンプリング、最低保証処理、予算制約、
    /// 配置バリデーションを一貫して実行するドメインサービス実装（POCO）。
    /// </summary>
    public sealed class ItemSpawnLotteryCore : IItemSpawnLotteryCore
    {
        public ItemSpawnExecutionReport Execute(
            ItemSpawnConfig config,
            IReadOnlyList<ItemSpawnDefinition> itemDefinitions,
            IReadOnlyList<SpawnSurfaceTriangle> triangles,
            IReadOnlyDictionary<ResourceCategory, float> categoryNeedWeights,
            IRandomNumberGenerator rng,
            ISpawnPositionValidator validator)
        {
            config ??= new ItemSpawnConfig();
            itemDefinitions ??= Array.Empty<ItemSpawnDefinition>();
            triangles ??= Array.Empty<SpawnSurfaceTriangle>();
            rng ??= new ItemSpawnPRNG(12345);

            int remainingBudget = config.Budget;
            int totalCostUsed = 0;
            int failedPlacementAttempts = 0;
            var spawnedItems = new List<SpawnedItemData>();
            var skipReasons = new List<string>();
            var itemCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var categoryCounts = new Dictionary<ResourceCategory, int>();
            var finalItemWeights = new Dictionary<string, float>(StringComparer.Ordinal);

            // 1. 各カテゴリのNeedWeight評価と各アイテムのEffectiveWeight（BaseWeight * NeedWeight）計算
            var definitionMap = new Dictionary<string, ItemSpawnDefinition>(StringComparer.Ordinal);
            var evaluations = new List<SpawnCandidateEvaluation>(itemDefinitions.Count);

            foreach (var def in itemDefinitions)
            {
                if (def == null) continue;
                definitionMap[def.Id] = def;
                itemCounts[def.Id] = 0;

                float needWeight = 1.0f;
                if (categoryNeedWeights != null && categoryNeedWeights.TryGetValue(def.Category, out float nw))
                {
                    needWeight = Math.Max(0f, nw);
                }

                var eval = new SpawnCandidateEvaluation(def, needWeight);
                evaluations.Add(eval);
                finalItemWeights[def.Id] = eval.EffectiveWeight;
            }

            // カテゴリカウンタ初期化
            foreach (ResourceCategory cat in Enum.GetValues(typeof(ResourceCategory)))
            {
                categoryCounts[cat] = 0;
            }

            // 2. 有効なサーフェス三角形の抽出と累積重みテーブル構築
            var validTriangles = new List<SpawnSurfaceTriangle>();
            float totalTriangleWeight = 0f;

            for (int i = 0; i < triangles.Count; i++)
            {
                var tri = triangles[i];
                if (tri.IsValid)
                {
                    validTriangles.Add(tri);
                    totalTriangleWeight += tri.EffectiveWeight;
                }
            }

            if (validTriangles.Count == 0 || totalTriangleWeight <= 0f)
            {
                skipReasons.Add("No valid spawn surface triangles with positive area and weights were found.");
                return new ItemSpawnExecutionReport(
                    spawnedItems,
                    totalCostUsed,
                    remainingBudget,
                    failedPlacementAttempts,
                    skipReasons,
                    finalItemWeights,
                    categoryNeedWeights != null ? new Dictionary<ResourceCategory, float>(categoryNeedWeights) : new Dictionary<ResourceCategory, float>(),
                    rng.Seed);
            }

            // 3. 最低保証処理（通常抽選より前に実行）
            ProcessMinimumGuarantees(
                config,
                itemDefinitions,
                definitionMap,
                validTriangles,
                totalTriangleWeight,
                validator,
                rng,
                ref remainingBudget,
                ref totalCostUsed,
                ref failedPlacementAttempts,
                spawnedItems,
                itemCounts,
                categoryCounts,
                finalItemWeights,
                skipReasons);

            // 4. 通常ランダム抽選フェーズ
            ProcessNormalLottery(
                config,
                evaluations,
                validTriangles,
                totalTriangleWeight,
                validator,
                rng,
                ref remainingBudget,
                ref totalCostUsed,
                ref failedPlacementAttempts,
                spawnedItems,
                itemCounts,
                categoryCounts,
                finalItemWeights,
                skipReasons);

            return new ItemSpawnExecutionReport(
                spawnedItems,
                totalCostUsed,
                remainingBudget,
                failedPlacementAttempts,
                skipReasons,
                finalItemWeights,
                categoryNeedWeights != null ? new Dictionary<ResourceCategory, float>(categoryNeedWeights) : new Dictionary<ResourceCategory, float>(),
                rng.Seed);
        }

        private void ProcessMinimumGuarantees(
            ItemSpawnConfig config,
            IReadOnlyList<ItemSpawnDefinition> itemDefinitions,
            Dictionary<string, ItemSpawnDefinition> definitionMap,
            List<SpawnSurfaceTriangle> validTriangles,
            float totalTriangleWeight,
            ISpawnPositionValidator validator,
            IRandomNumberGenerator rng,
            ref int remainingBudget,
            ref int totalCostUsed,
            ref int failedPlacementAttempts,
            List<SpawnedItemData> spawnedItems,
            Dictionary<string, int> itemCounts,
            Dictionary<ResourceCategory, int> categoryCounts,
            Dictionary<string, float> finalItemWeights,
            List<string> skipReasons)
        {
            // A. Item-level MinCount 保証
            foreach (var def in itemDefinitions)
            {
                if (def == null || def.MinCount <= 0) continue;

                int needed = def.MinCount - itemCounts[def.Id];
                for (int g = 0; g < needed; g++)
                {
                    if (spawnedItems.Count >= config.MaxTotalCount)
                    {
                        skipReasons.Add($"[Guarantee Skip] Item '{def.Id}' min count skipped because max total spawn limit ({config.MaxTotalCount}) was reached.");
                        break;
                    }

                    if (remainingBudget < def.SpawnCost)
                    {
                        skipReasons.Add($"[Guarantee Skip] Item '{def.Id}' requires cost {def.SpawnCost}, but remaining budget is {remainingBudget}.");
                        break;
                    }

                    if (itemCounts[def.Id] >= def.MaxCount || categoryCounts[def.Category] >= def.CategoryMaxCount)
                    {
                        skipReasons.Add($"[Guarantee Skip] Item '{def.Id}' reached max limit.");
                        break;
                    }

                    bool spawned = TryPlaceItem(
                        def,
                        config,
                        validTriangles,
                        totalTriangleWeight,
                        validator,
                        rng,
                        spawnedItems,
                        finalItemWeights.TryGetValue(def.Id, out float w) ? w : def.BaseWeight,
                        isGuaranteed: true,
                        out SpawnedItemData itemData,
                        out int attemptsUsed,
                        out string failReason);

                    failedPlacementAttempts += attemptsUsed;

                    if (spawned)
                    {
                        spawnedItems.Add(itemData);
                        remainingBudget -= def.SpawnCost;
                        totalCostUsed += def.SpawnCost;
                        itemCounts[def.Id]++;
                        categoryCounts[def.Category]++;
                    }
                    else
                    {
                        skipReasons.Add($"[Guarantee Failed] Item '{def.Id}' failed placement after {attemptsUsed} attempts. Last reason: {failReason}");
                    }
                }
            }

            // B. Explicit Config Guarantees 保証
            if (config.Guarantees != null)
            {
                foreach (var guarantee in config.Guarantees)
                {
                    if (guarantee == null) continue;

                    for (int g = 0; g < guarantee.GuaranteedCount; g++)
                    {
                        if (spawnedItems.Count >= config.MaxTotalCount || remainingBudget <= 0) break;

                        ItemSpawnDefinition targetDef = null;

                        if (!string.IsNullOrEmpty(guarantee.ItemId))
                        {
                            definitionMap.TryGetValue(guarantee.ItemId, out targetDef);
                        }
                        else if (guarantee.Category.HasValue)
                        {
                            // カテゴリに属する候補から抽選（または最初の適合定義）
                            targetDef = FindCandidateForCategory(itemDefinitions, guarantee.Category.Value, remainingBudget, itemCounts, categoryCounts);
                        }

                        if (targetDef == null)
                        {
                            skipReasons.Add($"[Guarantee Skip] Could not find eligible definition for guarantee (ItemId: '{guarantee.ItemId}', Category: '{guarantee.Category}').");
                            continue;
                        }

                        if (remainingBudget < targetDef.SpawnCost)
                        {
                            skipReasons.Add($"[Guarantee Skip] Guaranteed item '{targetDef.Id}' cost {targetDef.SpawnCost} exceeds budget {remainingBudget}.");
                            continue;
                        }

                        if (itemCounts[targetDef.Id] >= targetDef.MaxCount || categoryCounts[targetDef.Category] >= targetDef.CategoryMaxCount)
                        {
                            skipReasons.Add($"[Guarantee Skip] Guaranteed item '{targetDef.Id}' reached max limits.");
                            continue;
                        }

                        bool spawned = TryPlaceItem(
                            targetDef,
                            config,
                            validTriangles,
                            totalTriangleWeight,
                            validator,
                            rng,
                            spawnedItems,
                            finalItemWeights.TryGetValue(targetDef.Id, out float w) ? w : targetDef.BaseWeight,
                            isGuaranteed: true,
                            out SpawnedItemData itemData,
                            out int attemptsUsed,
                            out string failReason);

                        failedPlacementAttempts += attemptsUsed;

                        if (spawned)
                        {
                            spawnedItems.Add(itemData);
                            remainingBudget -= targetDef.SpawnCost;
                            totalCostUsed += targetDef.SpawnCost;
                            itemCounts[targetDef.Id]++;
                            categoryCounts[targetDef.Category]++;
                        }
                        else
                        {
                            skipReasons.Add($"[Guarantee Failed] Guaranteed item '{targetDef.Id}' placement failed. Last reason: {failReason}");
                        }
                    }
                }
            }
        }

        private void ProcessNormalLottery(
            ItemSpawnConfig config,
            List<SpawnCandidateEvaluation> evaluations,
            List<SpawnSurfaceTriangle> validTriangles,
            float totalTriangleWeight,
            ISpawnPositionValidator validator,
            IRandomNumberGenerator rng,
            ref int remainingBudget,
            ref int totalCostUsed,
            ref int failedPlacementAttempts,
            List<SpawnedItemData> spawnedItems,
            Dictionary<string, int> itemCounts,
            Dictionary<ResourceCategory, int> categoryCounts,
            Dictionary<string, float> finalItemWeights,
            List<string> skipReasons)
        {
            var eligiblePool = new List<SpawnCandidateEvaluation>();

            while (spawnedItems.Count < config.MaxTotalCount && remainingBudget > 0)
            {
                eligiblePool.Clear();
                float totalPoolWeight = 0f;

                for (int i = 0; i < evaluations.Count; i++)
                {
                    var eval = evaluations[i];
                    var def = eval.Definition;

                    // 通常ランダム対象フラグ、予算制約、最大数制約、有効重みチェック
                    if (!def.IsNormalRandomCandidate) continue;
                    if (def.SpawnCost > remainingBudget) continue;
                    if (itemCounts[def.Id] >= def.MaxCount) continue;
                    if (categoryCounts[def.Category] >= def.CategoryMaxCount) continue;
                    if (eval.EffectiveWeight <= 0f) continue;

                    eligiblePool.Add(eval);
                    totalPoolWeight += eval.EffectiveWeight;
                }

                if (eligiblePool.Count == 0 || totalPoolWeight <= 0f)
                {
                    // 抽選可能な候補が存在しないため終了
                    break;
                }

                // ルーレット選択
                float roll = rng.NextFloat(0f, totalPoolWeight);
                SpawnCandidateEvaluation selected = eligiblePool[0];
                float cumulative = 0f;

                for (int i = 0; i < eligiblePool.Count; i++)
                {
                    cumulative += eligiblePool[i].EffectiveWeight;
                    if (roll < cumulative || i == eligiblePool.Count - 1)
                    {
                        selected = eligiblePool[i];
                        break;
                    }
                }

                var selectedDef = selected.Definition;

                bool spawned = TryPlaceItem(
                    selectedDef,
                    config,
                    validTriangles,
                    totalTriangleWeight,
                    validator,
                    rng,
                    spawnedItems,
                    selected.EffectiveWeight,
                    isGuaranteed: false,
                    out SpawnedItemData itemData,
                    out int attemptsUsed,
                    out string failReason);

                failedPlacementAttempts += attemptsUsed;

                if (spawned)
                {
                    spawnedItems.Add(itemData);
                    remainingBudget -= selectedDef.SpawnCost;
                    totalCostUsed += selectedDef.SpawnCost;
                    itemCounts[selectedDef.Id]++;
                    categoryCounts[selectedDef.Category]++;
                }
                else
                {
                    skipReasons.Add($"[Lottery Placement Failed] Item '{selectedDef.Id}' could not be placed after {attemptsUsed} attempts. Last reason: {failReason}");
                    // 配置失敗時は無限ループ防止のため、候補プールの条件再評価を行う（他の候補があれば次イテレーションで試行）
                }
            }
        }

        private bool TryPlaceItem(
            ItemSpawnDefinition candidate,
            ItemSpawnConfig config,
            List<SpawnSurfaceTriangle> validTriangles,
            float totalTriangleWeight,
            ISpawnPositionValidator validator,
            IRandomNumberGenerator rng,
            IReadOnlyList<SpawnedItemData> alreadySpawnedItems,
            float effectiveWeight,
            bool isGuaranteed,
            out SpawnedItemData spawnedItem,
            out int attemptsUsed,
            out string lastFailReason)
        {
            spawnedItem = null;
            lastFailReason = "No attempts made";
            attemptsUsed = 0;

            int maxAttempts = config.MaxPlacementAttempts;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                attemptsUsed++;

                // 1. サーフェス三角形の面積・重み比例ルーレット選択
                if (!TrySampleTriangle(validTriangles, totalTriangleWeight, rng, out SpawnSurfaceTriangle tri))
                {
                    lastFailReason = "Failed to sample a valid triangle.";
                    continue;
                }

                if (tri.PaintWeight <= 0f)
                {
                    lastFailReason = "Sampled triangle weight is zero.";
                    continue;
                }

                // 2. 三角形内部の一様ランダムサンプリング
                float u1 = rng.NextFloat();
                float u2 = rng.NextFloat();
                Vector3 samplePoint = tri.SampleUniformPoint(u1, u2);

                // 3. 回転姿勢および法線オフセット計算
                Vector3 normal = tri.Normal;
                Quaternion rotation = CalculateRotation(candidate.PlacementPolicy.RotationMode, normal, rng);
                Vector3 finalPosition = samplePoint + (normal * candidate.PlacementPolicy.HeightOffset);

                // 4. バリデーション実行（距離・プレイヤー開始地点・障害物等）
                var context = new SpawnValidationContext(
                    candidate,
                    finalPosition,
                    rotation,
                    tri,
                    alreadySpawnedItems,
                    config,
                    attempt);

                if (validator != null && !validator.ValidatePosition(in context, out string failReason))
                {
                    lastFailReason = failReason;
                    continue;
                }

                // 成功
                int surfaceIdInt = ParseOrHashSurfaceId(tri.SurfaceId);
                spawnedItem = new SpawnedItemData(
                    candidate.Id,
                    candidate,
                    finalPosition,
                    rotation,
                    surfaceIdInt,
                    tri.SurfaceName,
                    tri.TriangleIndex,
                    rng.Seed,
                    candidate.SpawnCost,
                    effectiveWeight,
                    isGuaranteed);

                return true;
            }

            return false;
        }

        private static bool TrySampleTriangle(
            List<SpawnSurfaceTriangle> triangles,
            float totalWeight,
            IRandomNumberGenerator rng,
            out SpawnSurfaceTriangle selectedTriangle)
        {
            selectedTriangle = default;
            if (triangles == null || triangles.Count == 0 || totalWeight <= 0f) return false;

            float roll = rng.NextFloat(0f, totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < triangles.Count; i++)
            {
                cumulative += triangles[i].EffectiveWeight;
                if (roll < cumulative || i == triangles.Count - 1)
                {
                    selectedTriangle = triangles[i];
                    return true;
                }
            }

            selectedTriangle = triangles[0];
            return true;
        }

        private static Quaternion CalculateRotation(
            PlacementRotationMode mode,
            Vector3 normal,
            IRandomNumberGenerator rng)
        {
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;

            switch (mode)
            {
                case PlacementRotationMode.AlignWithNormalAndRandomYaw:
                {
                    Quaternion align = Quaternion.FromToRotation(Vector3.up, normal);
                    float yaw = rng.NextFloat(0f, 360f);
                    return Quaternion.AngleAxis(yaw, normal) * align;
                }

                case PlacementRotationMode.AlignWithNormal:
                {
                    return Quaternion.FromToRotation(Vector3.up, normal);
                }

                case PlacementRotationMode.RandomYawOnly:
                {
                    float yaw = rng.NextFloat(0f, 360f);
                    return Quaternion.Euler(0f, yaw, 0f);
                }

                case PlacementRotationMode.FullRandomRotation:
                {
                    return Quaternion.Euler(
                        rng.NextFloat(0f, 360f),
                        rng.NextFloat(0f, 360f),
                        rng.NextFloat(0f, 360f));
                }

                case PlacementRotationMode.None:
                default:
                    return Quaternion.identity;
            }
        }

        private static ItemSpawnDefinition FindCandidateForCategory(
            IReadOnlyList<ItemSpawnDefinition> definitions,
            ResourceCategory category,
            int budget,
            Dictionary<string, int> itemCounts,
            Dictionary<ResourceCategory, int> categoryCounts)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i];
                if (def.Category == category &&
                    def.SpawnCost <= budget &&
                    itemCounts[def.Id] < def.MaxCount &&
                    categoryCounts[def.Category] < def.CategoryMaxCount)
                {
                    return def;
                }
            }
            return null;
        }

        /// <summary>
        /// サーフェス識別文字列を決定論的な整数IDに変換する。
        /// 数値文字列の場合はint.TryParseを優先し、非数値文字列の場合はプロセス・環境非依存のFNV-1aハッシュを計算する。
        /// 空またはnullの場合は0を返す。
        /// </summary>
        private static int ParseOrHashSurfaceId(string surfaceId)
        {
            if (string.IsNullOrEmpty(surfaceId))
            {
                return 0;
            }

            if (int.TryParse(surfaceId, out int sid))
            {
                return sid;
            }

            // 32-bit FNV-1a 決定論的ハッシュ計算 (UTF-16文字単位、ランタイム/プロセス非依存)
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < surfaceId.Length; i++)
                {
                    hash ^= surfaceId[i];
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }
    }
}
