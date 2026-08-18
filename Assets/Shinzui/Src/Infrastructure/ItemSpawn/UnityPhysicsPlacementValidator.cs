using System;
using Shinzui.Application.Interfaces.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using UnityEngine;

namespace Shinzui.Infrastructure.ItemSpawn
{
    /// <summary>
    /// Unityの物理エンジン（Physics.OverlapSphere）を利用して、
    /// 壁や障害物との衝突・めり込みを検出する配置バリデータ実装。
    /// </summary>
    public sealed class UnityPhysicsPlacementValidator : IExternalPlacementValidator
    {
        private readonly LayerMask _obstacleMask;
        private readonly QueryTriggerInteraction _triggerInteraction;
        private readonly Collider[] _overlapResults = new Collider[8];

        public UnityPhysicsPlacementValidator(
            LayerMask obstacleMask,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore)
        {
            _obstacleMask = obstacleMask;
            _triggerInteraction = triggerInteraction;
        }

        public bool ValidateLocation(
            SpawnVector3 position,
            SpawnVector3 normal,
            ItemSpawnTarget target,
            out string failureReason)
        {
            failureReason = null;

            if (_obstacleMask.value == 0)
            {
                return true; // マスク未設定の場合はスキップ
            }

            float radius = target?.CollisionRadius ?? 0.3f;
            Vector3 worldPos = new Vector3(position.X, position.Y, position.Z);
            Vector3 worldNorm = new Vector3(normal.X, normal.Y, normal.Z);

            // アイテムのコライダー中心（法線方向に半径分浮かせた位置）
            Vector3 checkCenter = worldPos + (worldNorm * radius);

            int hitCount = Physics.OverlapSphereNonAlloc(
                checkCenter,
                radius,
                _overlapResults,
                _obstacleMask,
                _triggerInteraction);

            if (hitCount > 0)
            {
                Collider hit = _overlapResults[0];
                failureReason = $"Physics collision detected with '{(hit != null ? hit.name : "Obstacle")}' at ({checkCenter.x:F2}, {checkCenter.y:F2}, {checkCenter.z:F2})";
                Array.Clear(_overlapResults, 0, hitCount);
                return false;
            }

            return true;
        }
    }
}
