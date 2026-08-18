using System;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using UnityEngine;

namespace Shinzui.Application.DTOs.ItemSpawn
{
    /// <summary>
    /// 2D Weight Mapの投影平面（Application DTO定義）。
    /// </summary>
    public enum SpawnSurfaceProjectionDto
    {
        XZ = 0,
        XY = 1,
        YZ = 2
    }

    [Serializable]
    public class SpawnSurfaceDataDto
    {
        public string SurfaceId;
        public float RegionWeight;
        public List<SpawnSurfaceTriangleDto> Triangles = new();
        public bool IsValid = true;

        public int MapResolution;
        public float[] WeightMap;
        public Vector3 BoundsMin;
        public Vector3 BoundsSize;
        public SpawnSurfaceProjectionDto ProjectionPlane = SpawnSurfaceProjectionDto.XZ;
        public float[] WorldToLocalMatrix;

        // 後方互換用
        public Vector3 TransformPosition;
        public Vector3 TransformScale = Vector3.one;

        public SpawnSurfaceData ToDomain()
        {
            var domainTriangles = new List<SpawnSurfaceTriangle>();
            if (Triangles != null)
            {
                foreach (var t in Triangles)
                {
                    if (t != null)
                    {
                        domainTriangles.Add(t.ToDomain());
                    }
                }
            }

            SpawnSurfaceWeightMap domainWeightMap = null;
            if (WeightMap != null && WeightMap.Length > 0 && MapResolution > 0)
            {
                SpawnMatrix4x4 w2l = WorldToLocalMatrix != null && WorldToLocalMatrix.Length == 16
                    ? SpawnMatrix4x4.FromFloatArray(WorldToLocalMatrix)
                    : SpawnMatrix4x4.CreateInverseTranslationScale(TransformPosition, TransformScale);

                var domainProjectionPlane = ProjectionPlane switch
                {
                    SpawnSurfaceProjectionDto.XY => SpawnSurfaceProjectionPlane.XY,
                    SpawnSurfaceProjectionDto.YZ => SpawnSurfaceProjectionPlane.YZ,
                    _ => SpawnSurfaceProjectionPlane.XZ
                };

                domainWeightMap = new SpawnSurfaceWeightMap(
                    MapResolution,
                    WeightMap,
                    BoundsMin,
                    BoundsSize,
                    w2l,
                    domainProjectionPlane);
            }

            return new SpawnSurfaceData(SurfaceId, RegionWeight, domainTriangles, domainWeightMap, IsValid);
        }
    }
}
