using System;

namespace Shinzui.Domain.ValueObjects.Tunnel
{
    /// <summary>
    /// トンネルや通路がXZ平面上で占有する矩形領域を表現する値オブジェクト。
    /// トンネル同士や通路同士の重なり（コリジョン）判定に使用される。
    /// </summary>
    public readonly struct TunnelOccupiedArea : IEquatable<TunnelOccupiedArea>
    {
        public TunnelVector2 Center { get; }
        public TunnelVector2 Size { get; }
        public int? OwnerTunnelId { get; }

        public TunnelOccupiedArea(TunnelVector2 center, TunnelVector2 size, int? ownerTunnelId = null)
        {
            Center = center;
            Size = size;
            OwnerTunnelId = ownerTunnelId;
        }

        public bool Overlaps(TunnelOccupiedArea other)
        {
            TunnelVector2 distance = Center - other.Center;
            TunnelVector2 minimumDistance = (Size + other.Size) * 0.5f;
            return Math.Abs(distance.X) < minimumDistance.X &&
                   Math.Abs(distance.Y) < minimumDistance.Y;
        }

        public bool Equals(TunnelOccupiedArea other) =>
            Center.Equals(other.Center) &&
            Size.Equals(other.Size) &&
            OwnerTunnelId == other.OwnerTunnelId;

        public override bool Equals(object obj) => obj is TunnelOccupiedArea other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Center, Size, OwnerTunnelId);
    }
}
