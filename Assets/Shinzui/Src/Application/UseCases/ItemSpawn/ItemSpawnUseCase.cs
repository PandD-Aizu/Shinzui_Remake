using System;
using System.Collections.Generic;
using Shinzui.Application.DTOs.ItemSpawn;
using Shinzui.Application.Interfaces.ItemSpawn;
using Shinzui.Application.Interfaces.ResourceNeed;
using Shinzui.Domain.DomainServices.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;
using UnityEngine;

namespace Shinzui.Application.UseCases.ItemSpawn
{
    /// <summary>
    /// アイテム生成フロー全体をオーケストレーションするユースケース。
    /// IPlayerResourceNeedUseCase（未注入時は中立重み1.0fフォールバック）を統合し、
    /// ドメイン層の純粋な抽選コアとバリデータを呼び出す。
    /// </summary>
    public sealed class ItemSpawnUseCase : IItemSpawnUseCase
    {
        private readonly IPlayerResourceNeedUseCase _resourceNeedUseCase;
        private readonly IPhysicsCollisionChecker _physicsCollisionChecker;
        private readonly IItemSpawnLotteryCore _lotteryCore;
        private readonly IItemSpawnPlanner _planner;

        public ItemSpawnUseCase()
            : this(null, null, null, null)
        {
        }

        public ItemSpawnUseCase(IPlayerResourceNeedUseCase resourceNeedUseCase)
            : this(resourceNeedUseCase, null, null, null)
        {
        }

        public ItemSpawnUseCase(IPhysicsCollisionChecker physicsCollisionChecker)
            : this(null, physicsCollisionChecker, null, null)
        {
        }

        public ItemSpawnUseCase(
            IPlayerResourceNeedUseCase resourceNeedUseCase,
            IPhysicsCollisionChecker physicsCollisionChecker)
            : this(resourceNeedUseCase, physicsCollisionChecker, null, null)
        {
        }

        public ItemSpawnUseCase(
            IPlayerResourceNeedUseCase resourceNeedUseCase,
            IPhysicsCollisionChecker physicsCollisionChecker,
            IItemSpawnLotteryCore lotteryCore,
            IItemSpawnPlanner planner)
        {
            _resourceNeedUseCase = resourceNeedUseCase;
            _physicsCollisionChecker = physicsCollisionChecker;
            _lotteryCore = lotteryCore ?? new ItemSpawnLotteryCore();
            _planner = planner ?? new ItemSpawnPlanner();
        }

        public ItemSpawnResultDto ExecuteSpawnPlanning(
            ItemSpawnConfigDto configDto,
            IReadOnlyList<ItemSpawnTargetDto> targetsDto,
            IReadOnlyList<SpawnSurfaceDataDto> surfacesDto,
            INeedWeightProvider needWeightProvider = null,
            IExternalPlacementValidator externalValidator = null)
        {
            configDto ??= new ItemSpawnConfigDto();
            ItemSpawnRunConfig config = configDto.ToDomain();

            var targets = new List<ItemSpawnTarget>();
            if (targetsDto != null)
            {
                for (int i = 0; i < targetsDto.Count; i++)
                {
                    if (targetsDto[i] != null && !string.IsNullOrEmpty(targetsDto[i].Id))
                    {
                        targets.Add(targetsDto[i].ToDomain());
                    }
                }
            }

            var surfaces = new List<SpawnSurfaceData>();
            if (surfacesDto != null)
            {
                for (int i = 0; i < surfacesDto.Count; i++)
                {
                    if (surfacesDto[i] != null)
                    {
                        surfaces.Add(surfacesDto[i].ToDomain());
                    }
                }
            }

            Func<ResourceCategory, float> needFunc = null;
            if (needWeightProvider != null)
            {
                needFunc = cat => needWeightProvider.GetNeedWeight(cat);
            }
            else if (_resourceNeedUseCase != null)
            {
                needFunc = cat =>
                {
                    try { return Math.Max(0f, _resourceNeedUseCase.GetNeedWeight(cat)); }
                    catch { return 1.0f; }
                };
            }

            ISpawnPlacementValidator compositeValidator = new AppPlacementValidatorAdapter(externalValidator);

            ItemSpawnRunResult domainResult = _planner.PlanSpawns(
                config,
                targets,
                surfaces,
                needFunc,
                compositeValidator);

            return ItemSpawnResultDto.FromDomain(domainResult);
        }

        private sealed class AppPlacementValidatorAdapter : DomainDistancePlacementValidator
        {
            private readonly IExternalPlacementValidator _externalValidator;

            public AppPlacementValidatorAdapter(IExternalPlacementValidator externalValidator)
            {
                _externalValidator = externalValidator;
            }

            public override bool Validate(
                SpawnVector3 position,
                SpawnVector3 normal,
                ItemSpawnTarget target,
                IReadOnlyList<SpawnedItemRecord> alreadySpawned,
                ItemSpawnRunConfig config,
                out string failureReason)
            {
                if (!base.Validate(position, normal, target, alreadySpawned, config, out failureReason))
                {
                    return false;
                }

                if (_externalValidator != null)
                {
                    if (!_externalValidator.ValidateLocation(position, normal, target, out failureReason))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public ItemSpawnResultDto ExecuteSpawn(
            ItemSpawnSettingsDto settingsDto,
            IReadOnlyList<ItemSpawnDefinitionDto> itemDefinitionsDto,
            IReadOnlyList<SpawnSurfaceTriangleDto> trianglesDto,
            int seed,
            ISpawnPositionValidator customValidator = null)
        {
            var domainTriangles = new List<SpawnSurfaceTriangle>();
            if (trianglesDto != null)
            {
                for (int i = 0; i < trianglesDto.Count; i++)
                {
                    var dto = trianglesDto[i];
                    if (dto == null) continue;

                    var tri = new SpawnSurfaceTriangle(
                        dto.VertexA,
                        dto.VertexB,
                        dto.VertexC,
                        dto.PaintWeight,
                        dto.RegionWeight,
                        dto.SurfaceId,
                        dto.SurfaceName,
                        dto.TriangleIndex);

                    domainTriangles.Add(tri);
                }
            }

            return ExecuteSpawn(settingsDto, itemDefinitionsDto, domainTriangles, seed, customValidator);
        }

        public ItemSpawnResultDto ExecuteSpawn(
            ItemSpawnSettingsDto settingsDto,
            IReadOnlyList<ItemSpawnDefinitionDto> itemDefinitionsDto,
            ISpawnSurfaceDataProvider surfaceDataProvider,
            int seed,
            ISpawnPositionValidator customValidator = null)
        {
            IReadOnlyList<SpawnSurfaceTriangle> triangles = surfaceDataProvider != null
                ? surfaceDataProvider.ExtractTriangles()
                : Array.Empty<SpawnSurfaceTriangle>();

            return ExecuteSpawn(settingsDto, itemDefinitionsDto, triangles, seed, customValidator);
        }

        public ItemSpawnResultDto ExecuteSpawn(
            ItemSpawnSettingsDto settingsDto,
            IReadOnlyList<ItemSpawnDefinitionDto> itemDefinitionsDto,
            IReadOnlyList<SpawnSurfaceTriangle> domainTriangles,
            int seed,
            ISpawnPositionValidator customValidator = null)
        {
            settingsDto ??= new ItemSpawnSettingsDto();
            itemDefinitionsDto ??= Array.Empty<ItemSpawnDefinitionDto>();
            domainTriangles ??= Array.Empty<SpawnSurfaceTriangle>();

            // 1. 各カテゴリのNeedWeight評価（IPlayerResourceNeedUseCaseが未設定の場合は1.0fの中立重みフォールバック）
            var categoryNeedWeights = new Dictionary<ResourceCategory, float>();
            foreach (ResourceCategory category in Enum.GetValues(typeof(ResourceCategory)))
            {
                float weight = 1.0f;
                if (_resourceNeedUseCase != null)
                {
                    try
                    {
                        weight = Math.Max(0f, _resourceNeedUseCase.GetNeedWeight(category));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ItemSpawnUseCase] Failed to get need weight for category {category}: {ex.Message}. Falling back to 1.0f.");
                        weight = 1.0f;
                    }
                }
                categoryNeedWeights[category] = weight;
            }

            // 2. DTOからドメインモデル（ItemSpawnDefinition / ItemSpawnConfig）への変換
            var domainDefinitions = new List<ItemSpawnDefinition>(itemDefinitionsDto.Count);
            foreach (var dto in itemDefinitionsDto)
            {
                if (dto == null) continue;
                var policy = new ItemPlacementPolicy(
                    dto.BoundsSize,
                    dto.HeightOffset,
                    dto.RotationMode,
                    dto.CollisionShape);

                var def = new ItemSpawnDefinition(
                    dto.Id,
                    dto.BaseWeight,
                    dto.Category,
                    dto.SpawnCost,
                    dto.MinCount,
                    dto.MaxCount,
                    dto.CategoryMinCount,
                    dto.CategoryMaxCount,
                    dto.MinDistance,
                    dto.IsNormalRandomCandidate,
                    policy);

                domainDefinitions.Add(def);
            }

            var domainGuarantees = new List<ItemSpawnGuarantee>();
            if (settingsDto.Guarantees != null)
            {
                foreach (var gDto in settingsDto.Guarantees)
                {
                    if (gDto == null) continue;
                    if (!string.IsNullOrEmpty(gDto.ItemId))
                    {
                        domainGuarantees.Add(new ItemSpawnGuarantee(gDto.ItemId, gDto.GuaranteedCount));
                    }
                    else if (gDto.Category.HasValue)
                    {
                        domainGuarantees.Add(new ItemSpawnGuarantee(gDto.Category.Value, gDto.GuaranteedCount));
                    }
                }
            }

            var domainConfig = new ItemSpawnConfig(
                settingsDto.Budget,
                settingsDto.MinTotalCount,
                settingsDto.MaxTotalCount,
                settingsDto.MaxPlacementAttempts,
                settingsDto.MinPlayerStartDistance,
                settingsDto.PlayerStartPosition,
                settingsDto.PhysicsLayerMask,
                settingsDto.TriggerInteraction,
                settingsDto.CheckPhysicsCollision,
                domainGuarantees);

            // 3. バリデータの構築（距離バリデータ + 物理衝突バリデータ + 任意カスタムバリデータ）
            var compositeValidator = new CompositePositionValidator();
            compositeValidator.Add(new DistancePositionValidator());

            if (_physicsCollisionChecker != null && settingsDto.CheckPhysicsCollision)
            {
                compositeValidator.Add(new PhysicsPositionValidatorAdapter(_physicsCollisionChecker));
            }

            if (customValidator != null)
            {
                compositeValidator.Add(customValidator);
            }

            // 4. 独立PRNGの初期化
            var rng = new ItemSpawnPRNG(seed);

            // 5. ドメイン抽選コアの実行
            ItemSpawnExecutionReport report = _lotteryCore.Execute(
                domainConfig,
                domainDefinitions,
                domainTriangles,
                categoryNeedWeights,
                rng,
                compositeValidator);

            // 6. ドメインレポートから結果DTOへの変換
            var resultDto = new ItemSpawnResultDto
            {
                TotalCostUsed = report.TotalCostUsed,
                RemainingBudget = report.RemainingBudget,
                FailedPlacementAttempts = report.FailedPlacementAttempts,
                Seed = report.Seed,
                SkipReasons = new List<string>(report.SkipReasons),
                FinalItemWeights = new Dictionary<string, float>(report.FinalItemWeights),
                CategoryNeedWeights = new Dictionary<ResourceCategory, float>(report.CategoryNeedWeights)
            };

            foreach (var item in report.SpawnedItems)
            {
                resultDto.SpawnedItems.Add(new SpawnedItemDto
                {
                    ItemId = item.ItemId,
                    Category = item.Definition.Category,
                    WorldPosition = item.WorldPosition,
                    WorldRotation = item.WorldRotation,
                    SurfaceId = item.SurfaceId.ToString(),
                    SurfaceName = item.SurfaceName,
                    TriangleIndex = item.TriangleIndex,
                    Seed = item.Seed,
                    Cost = item.Cost,
                    EffectiveWeight = item.EffectiveWeight,
                    IsGuaranteed = item.IsGuaranteed
                });
            }

            return resultDto;
        }

        /// <summary>
        /// IPhysicsCollisionCheckerをISpawnPositionValidatorとして動作させるアダプター
        /// </summary>
        private sealed class PhysicsPositionValidatorAdapter : ISpawnPositionValidator
        {
            private readonly IPhysicsCollisionChecker _checker;

            public PhysicsPositionValidatorAdapter(IPhysicsCollisionChecker checker)
            {
                _checker = checker;
            }

            public bool ValidatePosition(in SpawnValidationContext context, out string failReason)
            {
                if (_checker == null || !context.Config.CheckPhysicsCollision)
                {
                    failReason = string.Empty;
                    return true;
                }

                bool isOverlapping = _checker.CheckOverlap(
                    context.ProposedPosition,
                    context.ProposedRotation,
                    context.Candidate.PlacementPolicy.BoundsSize,
                    context.Candidate.PlacementPolicy.CollisionShape,
                    context.Config.PhysicsLayerMask,
                    context.Config.TriggerInteraction);

                if (isOverlapping)
                {
                    failReason = $"Physics overlap detected at {context.ProposedPosition}";
                    return false;
                }

                failReason = string.Empty;
                return true;
            }
        }
    }
}
