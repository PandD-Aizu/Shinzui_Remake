using System;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using UnityEngine;

namespace Shinzui.Application.DTOs.ItemSpawn
{
    /// <summary>
    /// サーフェスの三角形ポリゴン情報をView/PresentationからApplication層へ受け渡すDTO
    /// </summary>
    [Serializable]
    public sealed class SpawnSurfaceTriangleDto
    {
        public Vector3 VertexA { get; set; }
        public Vector3 VertexB { get; set; }
        public Vector3 VertexC { get; set; }
        public float PaintWeight { get; set; }
        public float RegionWeight { get; set; }
        public int SurfaceId { get; set; }
        public string SurfaceName { get; set; }
        public int TriangleIndex { get; set; }

        public float Ax
        {
            get => VertexA.x;
            set => VertexA = new Vector3(value, VertexA.y, VertexA.z);
        }
        public float Ay
        {
            get => VertexA.y;
            set => VertexA = new Vector3(VertexA.x, value, VertexA.z);
        }
        public float Az
        {
            get => VertexA.z;
            set => VertexA = new Vector3(VertexA.x, VertexA.y, value);
        }

        public float Bx
        {
            get => VertexB.x;
            set => VertexB = new Vector3(value, VertexB.y, VertexB.z);
        }
        public float By
        {
            get => VertexB.y;
            set => VertexB = new Vector3(VertexB.x, value, VertexB.z);
        }
        public float Bz
        {
            get => VertexB.z;
            set => VertexB = new Vector3(VertexB.x, VertexB.y, value);
        }

        public float Cx
        {
            get => VertexC.x;
            set => VertexC = new Vector3(value, VertexC.y, VertexC.z);
        }
        public float Cy
        {
            get => VertexC.y;
            set => VertexC = new Vector3(VertexC.x, value, VertexC.z);
        }
        public float Cz
        {
            get => VertexC.z;
            set => VertexC = new Vector3(VertexC.x, VertexC.y, value);
        }

        public SpawnSurfaceTriangle ToDomain()
        {
            return new SpawnSurfaceTriangle(
                VertexA,
                VertexB,
                VertexC,
                PaintWeight,
                RegionWeight,
                SurfaceId,
                SurfaceName ?? SurfaceId.ToString(),
                TriangleIndex);
        }
    }
}
