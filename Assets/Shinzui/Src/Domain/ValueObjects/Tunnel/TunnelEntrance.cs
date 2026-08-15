using System;

namespace Shinzui.Domain.ValueObjects.Tunnel
{
    /// <summary>
    /// トンネルの出入口候補定義（名前、正規化ローカル位置、接続向き）。
    /// </summary>
    public readonly struct TunnelEntrance : IEquatable<TunnelEntrance>
    {
        public string Name { get; }
        public TunnelVector2 NormalizedPosition { get; }
        public TunnelVector2 Direction { get; }

        public TunnelEntrance(string name, TunnelVector2 normalizedPosition, TunnelVector2 direction)
        {
            Name = name ?? string.Empty;
            NormalizedPosition = normalizedPosition;
            Direction = direction;
        }

        public bool Equals(TunnelEntrance other) =>
            Name == other.Name &&
            NormalizedPosition.Equals(other.NormalizedPosition) &&
            Direction.Equals(other.Direction);

        public override bool Equals(object obj) => obj is TunnelEntrance other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Name, NormalizedPosition, Direction);
    }
}
