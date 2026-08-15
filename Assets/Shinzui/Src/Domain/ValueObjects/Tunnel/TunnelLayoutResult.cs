using System.Collections.Generic;

namespace Shinzui.Domain.ValueObjects.Tunnel
{
    /// <summary>
    /// ドメイン層で決定論的に計算された各トンネルノードのレイアウト情報。
    /// </summary>
    public sealed class TunnelNodeLayout
    {
        public int Id { get; }
        public string Name { get; }
        public TunnelVector3 Position { get; }
        public bool IsSpecial { get; set; }
        public IReadOnlyList<TunnelEntranceMarkerLayout> EntranceMarkers { get; }
        public IReadOnlyList<bool> OpenEntrances { get; }

        public TunnelNodeLayout(
            int id,
            string name,
            TunnelVector3 position,
            bool isSpecial,
            IReadOnlyList<TunnelEntranceMarkerLayout> entranceMarkers,
            IReadOnlyList<bool> openEntrances = null)
        {
            Id = id;
            Name = name;
            Position = position;
            IsSpecial = isSpecial;
            EntranceMarkers = entranceMarkers;
            OpenEntrances = openEntrances;
        }
    }

    /// <summary>
    /// トンネルの出入口候補マーカーのレイアウト情報。
    /// </summary>
    public sealed class TunnelEntranceMarkerLayout
    {
        public int Index { get; }
        public string Name { get; }
        public TunnelVector3 LocalPosition { get; }
        public bool IsActive { get; set; }
        public bool IsOpen { get; set; }

        public TunnelEntranceMarkerLayout(int index, string name, TunnelVector3 localPosition, bool isActive = true, bool isOpen = false)
        {
            Index = index;
            Name = name;
            LocalPosition = localPosition;
            IsActive = isActive;
            IsOpen = isOpen;
        }
    }

    /// <summary>
    /// 通常通路のレイアウト情報。
    /// </summary>
    public sealed class NormalCorridorLayout
    {
        public int ConnectionIndex { get; }
        public TunnelVector3 StartPort { get; }
        public TunnelVector3 EndPort { get; }
        public TunnelVector3 Center { get; }
        public TunnelVector3 Direction { get; }
        public float Length { get; }

        public NormalCorridorLayout(
            int connectionIndex,
            TunnelVector3 startPort,
            TunnelVector3 endPort,
            TunnelVector3 center,
            TunnelVector3 direction,
            float length)
        {
            ConnectionIndex = connectionIndex;
            StartPort = startPort;
            EndPort = endPort;
            Center = center;
            Direction = direction;
            Length = length;
        }
    }

    /// <summary>
    /// 小部屋接続のレイアウト情報。
    /// </summary>
    public sealed class SmallRoomLayout
    {
        public int RoomNumber { get; }
        public int ConnectionIndex { get; }
        public TunnelVector3 Center { get; }
        public TunnelVector3 Direction { get; }
        public float TotalLength { get; }
        public float Width { get; }
        public float RoomLength { get; }
        public float PassageLength { get; }

        public SmallRoomLayout(
            int roomNumber,
            int connectionIndex,
            TunnelVector3 center,
            TunnelVector3 direction,
            float totalLength,
            float width,
            float roomLength,
            float passageLength)
        {
            RoomNumber = roomNumber;
            ConnectionIndex = connectionIndex;
            Center = center;
            Direction = direction;
            TotalLength = totalLength;
            Width = width;
            RoomLength = roomLength;
            PassageLength = passageLength;
        }
    }

    /// <summary>
    /// ワープ通路のレイアウト情報。
    /// </summary>
    public sealed class WarpCorridorLayout
    {
        public int PairId { get; }
        public string SideName { get; }
        public int TunnelId { get; }
        public TunnelVector3 StartPort { get; }
        public TunnelVector3 EndPort { get; }
        public TunnelVector3 Center { get; }
        public TunnelVector3 Direction { get; }
        public float Length { get; }
        public int PairedIndex { get; set; } = -1;

        public WarpCorridorLayout(
            int pairId,
            string sideName,
            int tunnelId,
            TunnelVector3 startPort,
            TunnelVector3 endPort,
            TunnelVector3 center,
            TunnelVector3 direction,
            float length)
        {
            PairId = pairId;
            SideName = sideName;
            TunnelId = tunnelId;
            StartPort = startPort;
            EndPort = endPort;
            Center = center;
            Direction = direction;
            Length = length;
        }
    }

    /// <summary>
    /// 生成トンネル群全体のBounding Box情報。カメラフレーミング計算に使用。
    /// </summary>
    public sealed class TunnelBoundsLayout
    {
        public TunnelVector3 Center { get; }
        public TunnelVector3 Size { get; }

        public TunnelBoundsLayout(TunnelVector3 center, TunnelVector3 size)
        {
            Center = center;
            Size = size;
        }
    }

    /// <summary>
    /// ドメイン層のレイアウト生成結果全体を格納するモデル（POCO）。
    /// </summary>
    public sealed class TunnelLayoutResult
    {
        public IReadOnlyList<TunnelNodeLayout> Tunnels { get; }
        public IReadOnlyList<NormalCorridorLayout> NormalCorridors { get; }
        public IReadOnlyList<SmallRoomLayout> SmallRooms { get; }
        public IReadOnlyList<WarpCorridorLayout> WarpCorridors { get; }
        public int? SpecialTunnelId { get; }
        public TunnelBoundsLayout Bounds { get; }
        public int Seed { get; }

        public TunnelLayoutResult(
            IReadOnlyList<TunnelNodeLayout> tunnels,
            IReadOnlyList<NormalCorridorLayout> normalCorridors,
            IReadOnlyList<SmallRoomLayout> smallRooms,
            IReadOnlyList<WarpCorridorLayout> warpCorridors,
            int? specialTunnelId,
            TunnelBoundsLayout bounds,
            int seed)
        {
            Tunnels = tunnels;
            NormalCorridors = normalCorridors;
            SmallRooms = smallRooms;
            WarpCorridors = warpCorridors;
            SpecialTunnelId = specialTunnelId;
            Bounds = bounds;
            Seed = seed;
        }
    }
}
