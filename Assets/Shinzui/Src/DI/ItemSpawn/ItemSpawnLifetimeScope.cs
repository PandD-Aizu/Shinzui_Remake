using System;
using System.Collections.Generic;
using Shinzui.Application.DTOs.ItemSpawn;
using Shinzui.Application.Interfaces.ItemSpawn;
using Shinzui.Application.Interfaces.ResourceNeed;
using Shinzui.Application.UseCases.ItemSpawn;
using Shinzui.Domain.DomainServices.ItemSpawn;
using Shinzui.Infrastructure.ItemSpawn;
using Shinzui.Presentation.ItemSpawn;
using Shinzui.View.ItemSpawn;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Shinzui.DI.ItemSpawn
{
    /// <summary>
    /// アイテムスポーンシステム関連の依存注入を管理するLifetimeScope。
    /// ドメイン・アプリケーション・インフラ・プレゼンテーション・ビューの全層をClean Architectureに沿って結線する。
    /// インフラ層のScriptableObject設定アセットをApplication層DTOへ変換し、Presentation層（ItemSpawnManager）へ渡す。
    /// </summary>
    public sealed class ItemSpawnLifetimeScope : LifetimeScope
    {
        [Header("Configurations (Infrastructure SO Assets)")]
        [SerializeField] private ItemSpawnConfigSO spawnConfig;
        [SerializeField] private List<ItemSpawnDefinitionSO> itemDefinitions = new();

        [Header("Views")]
        [SerializeField] private ItemSpawnContainerView containerView;
        [SerializeField] private ItemSpawnManager manager;

        protected override void Configure(IContainerBuilder builder)
        {
            // 1. ドメイン層サービスの登録
            builder.Register<ItemSpawnLotteryService>(Lifetime.Singleton).As<IItemSpawnLotteryService>();
            builder.Register<SurfaceSamplerService>(Lifetime.Singleton).As<ISurfaceSamplerService>();
            builder.Register<DomainDistancePlacementValidator>(Lifetime.Singleton).As<ISpawnPlacementValidator>();
            builder.Register<ItemSpawnPlanner>(Lifetime.Singleton).As<IItemSpawnPlanner>();

            // 2. アプリケーション層の登録（明示的factoryにより、未登録のIPlayerResourceNeedUseCaseを要求せずparameterlessのneutral fallbackで生成）
            builder.Register<IItemSpawnUseCase>(_ => new ItemSpawnUseCase(), Lifetime.Singleton);

            // 3. インフラ層アダプターの登録
            builder.Register<INeedWeightProvider>(resolver =>
            {
                resolver.TryResolve(out IPlayerResourceNeedUseCase needUseCase);
                return new PlayerResourceNeedWeightAdapter(needUseCase);
            }, Lifetime.Singleton);

            LayerMask obstacleMask = spawnConfig != null ? spawnConfig.ObstacleMask : default;
            QueryTriggerInteraction triggerInteraction = spawnConfig != null
                ? spawnConfig.TriggerInteraction
                : QueryTriggerInteraction.Ignore;

            builder.RegisterInstance(new UnityPhysicsPlacementValidator(
                obstacleMask,
                triggerInteraction)).As<IExternalPlacementValidator>();

            // 4. ビュー層コンポーネントの登録
            var resolvedContainer = containerView;
            if (resolvedContainer == null)
            {
                resolvedContainer = FindFirstObjectByType<ItemSpawnContainerView>();
            }
            if (resolvedContainer != null)
            {
                builder.RegisterComponent(resolvedContainer);
            }

            // 5. プレゼンテーション層の登録
            builder.Register<ItemSpawnPresenter>(Lifetime.Singleton);

            var resolvedManager = manager;
            if (resolvedManager == null)
            {
                resolvedManager = FindFirstObjectByType<ItemSpawnManager>();
            }
            if (resolvedManager != null)
            {
                // インフラ層SOからDTOへのマッピングを行ってマネージャーを設定
                if (spawnConfig != null)
                {
                    ItemSpawnConfigDto configDto = spawnConfig.ToDto(Vector3.zero);
                    var targetsDto = new List<ItemSpawnTargetDto>();
                    var prefabMap = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

                    for (int i = 0; i < itemDefinitions.Count; i++)
                    {
                        ItemSpawnDefinitionSO def = itemDefinitions[i];
                        if (def == null) continue;

                        ItemSpawnTargetDto dto = def.ToDto();
                        targetsDto.Add(dto);

                        if (def.Prefab != null && !prefabMap.ContainsKey(dto.Id))
                        {
                            prefabMap.Add(dto.Id, def.Prefab);
                        }
                    }

                    resolvedManager.Configure(configDto, targetsDto, prefabMap);
                }

                builder.RegisterComponent(resolvedManager);
            }
        }
    }
}
