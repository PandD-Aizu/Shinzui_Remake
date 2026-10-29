using UnityEngine;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>敵が聞き取る音の種類</summary>
    public enum NoiseKind { Footstep, Impact, Alarm, Door, Strobe, Breath }

    /// <summary>音の発生位置と聞こえる範囲</summary>
    public readonly struct NoiseEvent
    {
        public NoiseKind Kind { get; }
        public Vector3 Position { get; }
        public float Radius { get; }
        public Vector3 Direction { get; }

        /// <summary>音の刺激を作成</summary>
        /// <param name="kind">種類</param>
        /// <param name="position">発生位置</param>
        /// <param name="radius">最大可聴距離</param>
        /// <param name="direction">飛来方向</param>
        public NoiseEvent(NoiseKind kind, Vector3 position, float radius, Vector3 direction = default)
        {
            Kind = kind;
            Position = position;
            Radius = radius;
            Direction = direction;
        }
    }
}
