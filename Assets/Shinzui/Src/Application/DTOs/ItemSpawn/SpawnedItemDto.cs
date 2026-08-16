using System;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;
using UnityEngine;

namespace Shinzui.Application.DTOs.ItemSpawn
{
    /// <summary>
    /// 生成されたアイテムの情報をレイヤー間で受け渡すためのDTO
    /// </summary>
    [Serializable]
    public sealed class SpawnedItemDto
    {
        public string ItemId { get; set; }
        public ResourceCategory Category { get; set; }
        public Vector3 WorldPosition { get; set; }
        public Quaternion WorldRotation { get; set; }
        public string SurfaceId { get; set; }
        public string SurfaceName { get; set; }
        public int TriangleIndex { get; set; }
        public int Seed { get; set; }
        public int Cost { get; set; }
        public int SpawnCost
        {
            get => Cost;
            set => Cost = value;
        }
        public float EffectiveWeight { get; set; }
        public float BaseWeight { get; set; }
        public float NeedWeight { get; set; }
        public float FinalItemWeight
        {
            get => EffectiveWeight;
            set => EffectiveWeight = value;
        }
        public float SelectedTriangleWeight { get; set; }
        public bool IsGuaranteed { get; set; }

        public float PositionX
        {
            get => WorldPosition.x;
            set => WorldPosition = new Vector3(value, WorldPosition.y, WorldPosition.z);
        }
        public float PositionY
        {
            get => WorldPosition.y;
            set => WorldPosition = new Vector3(WorldPosition.x, value, WorldPosition.z);
        }
        public float PositionZ
        {
            get => WorldPosition.z;
            set => WorldPosition = new Vector3(WorldPosition.x, WorldPosition.y, value);
        }

        public float NormalX { get; set; }
        public float NormalY { get; set; }
        public float NormalZ { get; set; }
        public float YawDegrees { get; set; }
        public ItemSpawnRotationPolicy RotationPolicy { get; set; }

        public static SpawnedItemDto FromDomain(SpawnedItemRecord record)
        {
            if (record == null) return null;
            Vector3 normal = new Vector3(record.Normal.X, record.Normal.Y, record.Normal.Z);
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;
            normal.Normalize();

            Quaternion rot = Quaternion.identity;
            switch (record.RotationPolicy)
            {
                case ItemSpawnRotationPolicy.AlignToSurfaceNormalWithRandomYaw:
                    rot = Quaternion.AngleAxis(record.YawDegrees, normal) * Quaternion.FromToRotation(Vector3.up, normal);
                    break;
                case ItemSpawnRotationPolicy.AlignToSurfaceNormal:
                    rot = Quaternion.FromToRotation(Vector3.up, normal);
                    break;
                case ItemSpawnRotationPolicy.WorldUpWithRandomYaw:
                    rot = Quaternion.Euler(0f, record.YawDegrees, 0f);
                    break;
                case ItemSpawnRotationPolicy.FullRandomRotation:
                    rot = Quaternion.Euler(record.YawDegrees, (record.YawDegrees * 1.618f) % 360f, (record.YawDegrees * 2.718f) % 360f);
                    break;
            }

            return new SpawnedItemDto
            {
                ItemId = record.ItemId,
                WorldPosition = new Vector3(record.Position.X, record.Position.Y, record.Position.Z),
                WorldRotation = rot,
                NormalX = record.Normal.X,
                NormalY = record.Normal.Y,
                NormalZ = record.Normal.Z,
                YawDegrees = record.YawDegrees,
                RotationPolicy = record.RotationPolicy,
                SurfaceId = record.SourceSurfaceId,
                SurfaceName = record.SourceSurfaceId,
                TriangleIndex = record.SourceTriangleIndex,
                Seed = record.Seed,
                Cost = record.SpawnCost,
                BaseWeight = record.BaseWeight,
                NeedWeight = record.NeedWeight,
                EffectiveWeight = record.FinalItemWeight,
                SelectedTriangleWeight = record.SelectedTriangleWeight
            };
        }
    }
}
