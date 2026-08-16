using Shinzui.Domain.ValueObjects.ItemSpawn;
using UnityEngine;

namespace Shinzui.Application.Interfaces.ItemSpawn
{
    /// <summary>
    /// アイテム配置予定位置における物理的干渉（壁・障害物との衝突）を判定するインターフェース
    /// </summary>
    public interface IPhysicsCollisionChecker
    {
        bool CheckOverlap(
            Vector3 position,
            Quaternion rotation,
            Vector3 extents,
            PlacementCollisionShape shape,
            int layerMask,
            QueryTriggerInteraction triggerInteraction);
    }
}
