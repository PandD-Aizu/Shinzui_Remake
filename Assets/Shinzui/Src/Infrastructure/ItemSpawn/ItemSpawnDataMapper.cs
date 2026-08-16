using System.Collections.Generic;
using Shinzui.Application.DTOs.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using UnityEngine;

namespace Shinzui.Infrastructure.ItemSpawn
{
    /// <summary>
    /// ScriptableObject設定アセットをApplication層のDTO群へ変換するマッパー
    /// </summary>
    public static class ItemSpawnDataMapper
    {
        public static ItemSpawnDefinitionDto ToDto(ItemSpawnDefinitionSO so)
        {
            if (so == null) return null;

            return new ItemSpawnDefinitionDto
            {
                Id = string.IsNullOrEmpty(so.ItemId) ? so.name : so.ItemId,
                BaseWeight = so.BaseWeight,
                Category = so.Category,
                SpawnCost = so.SpawnCost,
                MinCount = so.MinCount,
                MaxCount = so.MaxCount,
                CategoryMinCount = 0,
                CategoryMaxCount = so.CategoryMaxCount,
                MinDistance = so.MinDistance,
                IsNormalRandomCandidate = so.IsNormalRandomCandidate,
                BoundsSize = Vector3.one * (so.CollisionRadius * 2f),
                HeightOffset = so.SurfaceOffset,
                RotationMode = MapRotationPolicy(so.RotationPolicy),
                CollisionShape = PlacementCollisionShape.Sphere,
                Prefab = so.Prefab
            };
        }

        public static List<ItemSpawnDefinitionDto> ToDtoList(IEnumerable<ItemSpawnDefinitionSO> list)
        {
            var dtos = new List<ItemSpawnDefinitionDto>();
            if (list != null)
            {
                foreach (var so in list)
                {
                    var dto = ToDto(so);
                    if (dto != null) dtos.Add(dto);
                }
            }
            return dtos;
        }

        public static ItemSpawnSettingsDto ToSettingsDto(ItemSpawnSettingsSO so, Vector3? playerStartPosition = null)
        {
            if (so == null) return new ItemSpawnSettingsDto { PlayerStartPosition = playerStartPosition };

            var dto = new ItemSpawnSettingsDto
            {
                Budget = so.Budget,
                MinTotalCount = so.MinTotalCount,
                MaxTotalCount = so.MaxTotalCount,
                MaxPlacementAttempts = so.MaxPlacementAttempts,
                MinPlayerStartDistance = so.MinPlayerStartDistance,
                PlayerStartPosition = playerStartPosition,
                PhysicsLayerMask = so.ObstacleLayerMask.value,
                TriggerInteraction = so.QueryTriggerInteraction,
                CheckPhysicsCollision = so.CheckPhysicsCollision
            };

            if (so.ExplicitGuarantees != null)
            {
                foreach (var g in so.ExplicitGuarantees)
                {
                    if (g == null) continue;
                    dto.Guarantees.Add(new ItemSpawnGuaranteeDto
                    {
                        ItemId = g.UseCategory ? null : g.ItemId,
                        Category = g.UseCategory ? g.Category : null,
                        GuaranteedCount = g.GuaranteedCount
                    });
                }
            }

            return dto;
        }

        private static PlacementRotationMode MapRotationPolicy(ItemSpawnRotationPolicy policy)
        {
            return policy switch
            {
                ItemSpawnRotationPolicy.AlignToSurfaceNormalWithRandomYaw => PlacementRotationMode.AlignWithNormalAndRandomYaw,
                ItemSpawnRotationPolicy.AlignToSurfaceNormal => PlacementRotationMode.AlignWithNormal,
                ItemSpawnRotationPolicy.WorldUpWithRandomYaw => PlacementRotationMode.RandomYawOnly,
                ItemSpawnRotationPolicy.FullRandomRotation => PlacementRotationMode.FullRandomRotation,
                _ => PlacementRotationMode.AlignWithNormalAndRandomYaw
            };
        }
    }
}
