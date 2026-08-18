using System;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ItemSpawn;

namespace Shinzui.Domain.DomainServices.ItemSpawn
{
    /// <summary>
    /// アイテム配置位置のバリデーション（プレイヤー開始地点距離、既存アイテム間距離、障害物判定など）を行うインターフェース。
    /// </summary>
    public interface ISpawnPlacementValidator
    {
        bool Validate(
            SpawnVector3 position,
            SpawnVector3 normal,
            ItemSpawnTarget target,
            IReadOnlyList<SpawnedItemRecord> alreadySpawned,
            ItemSpawnRunConfig config,
            out string failureReason);
    }

    /// <summary>
    /// ドメイン層の幾何的バリデーション（プレイヤー距離・アイテム間距離）を行う標準バリデータ。
    /// </summary>
    public class DomainDistancePlacementValidator : ISpawnPlacementValidator
    {
        public virtual bool Validate(
            SpawnVector3 position,
            SpawnVector3 normal,
            ItemSpawnTarget target,
            IReadOnlyList<SpawnedItemRecord> alreadySpawned,
            ItemSpawnRunConfig config,
            out string failureReason)
        {
            failureReason = null;

            // 1. プレイヤー開始位置との距離チェック
            if (config.PlayerStartMinDistance > 0f)
            {
                float distToPlayer = SpawnVector3.Distance(position, config.PlayerStartPosition);
                if (distToPlayer < config.PlayerStartMinDistance)
                {
                    failureReason = $"Too close to player start position (Distance: {distToPlayer:F2} < Min: {config.PlayerStartMinDistance:F2})";
                    return false;
                }
            }

            // 2. 既に配置されたアイテムとの最小距離チェック
            if (alreadySpawned != null && alreadySpawned.Count > 0)
            {
                float requiredDist = target?.MinDistance ?? 0f;
                for (int i = 0; i < alreadySpawned.Count; i++)
                {
                    SpawnedItemRecord existing = alreadySpawned[i];
                    float dist = SpawnVector3.Distance(position, existing.Position);
                    if (dist < requiredDist)
                    {
                        failureReason = $"Too close to existing item '{existing.ItemId}' (Distance: {dist:F2} < Min: {requiredDist:F2})";
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
