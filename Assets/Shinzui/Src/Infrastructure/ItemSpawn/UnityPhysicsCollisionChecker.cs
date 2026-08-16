using Shinzui.Application.Interfaces.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using UnityEngine;

namespace Shinzui.Infrastructure.ItemSpawn
{
    /// <summary>
    /// Unityの物理エンジン（Physics.OverlapBox / OverlapSphere）を利用して
    /// アイテム配置予定位置での壁・障害物・トリガーとの衝突を検証するInfrastructure実装
    /// </summary>
    public sealed class UnityPhysicsCollisionChecker : IPhysicsCollisionChecker
    {
        private static readonly Collider[] HitCollidersBuffer = new Collider[16];

        public bool CheckOverlap(
            Vector3 position,
            Quaternion rotation,
            Vector3 extents,
            PlacementCollisionShape shape,
            int layerMask,
            QueryTriggerInteraction triggerInteraction)
        {
            if (shape == PlacementCollisionShape.Sphere)
            {
                float radius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)) * 0.5f;
                int count = Physics.OverlapSphereNonAlloc(position, radius, HitCollidersBuffer, layerMask, triggerInteraction);
                return count > 0;
            }
            else
            {
                Vector3 halfExtents = extents * 0.5f;
                int count = Physics.OverlapBoxNonAlloc(position, halfExtents, HitCollidersBuffer, rotation, layerMask, triggerInteraction);
                return count > 0;
            }
        }
    }
}
