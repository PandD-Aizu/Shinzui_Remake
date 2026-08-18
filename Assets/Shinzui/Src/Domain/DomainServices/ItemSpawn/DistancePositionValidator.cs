using System;
using UnityEngine;

namespace Shinzui.Domain.DomainServices.ItemSpawn
{
    /// <summary>
    /// プレイヤースタート地点からの最低距離、および既存生成アイテム同士の最低距離を検証するバリデータ
    /// </summary>
    public sealed class DistancePositionValidator : ISpawnPositionValidator
    {
        public bool ValidatePosition(in SpawnValidationContext context, out string failReason)
        {
            Vector3 proposedPos = context.ProposedPosition;

            // 1. プレイヤースタート地点からの距離検証
            if (context.Config.PlayerStartPosition.HasValue && context.Config.MinPlayerStartDistance > 0f)
            {
                float distToPlayer = Vector3.Distance(proposedPos, context.Config.PlayerStartPosition.Value);
                if (distToPlayer < context.Config.MinPlayerStartDistance)
                {
                    failReason = $"Too close to player start: distance {distToPlayer:F2} < min {context.Config.MinPlayerStartDistance:F2}";
                    return false;
                }
            }

            // 2. 既存生成アイテムとの相互距離検証
            float candidateMinDist = context.Candidate.MinDistance;
            if (context.AlreadySpawnedItems != null)
            {
                for (int i = 0; i < context.AlreadySpawnedItems.Count; i++)
                {
                    var existing = context.AlreadySpawnedItems[i];
                    float requiredDist = Math.Max(candidateMinDist, existing.Definition.MinDistance);
                    if (requiredDist <= 0f) continue;

                    float distToExisting = Vector3.Distance(proposedPos, existing.WorldPosition);
                    if (distToExisting < requiredDist)
                    {
                        failReason = $"Too close to existing item '{existing.ItemId}': distance {distToExisting:F2} < required {requiredDist:F2}";
                        return false;
                    }
                }
            }

            failReason = string.Empty;
            return true;
        }
    }
}
