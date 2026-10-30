using System.Collections.Generic;

namespace Shinzui.Application.DTOs.Tunnel
{
    /// <summary>
    /// トンネル出入口候補マーカーDTO。
    /// </summary>
    public sealed class TunnelEntranceMarkerDto
    {
        public int Index { get; init; }
        public string Name { get; init; }
        public float LocalPosX { get; init; }
        public float LocalPosY { get; init; }
        public float LocalPosZ { get; init; }
        public bool IsActive { get; init; }
        public bool IsOpen { get; init; }
    }

    /// <summary>
    /// トンネルノードDTO。
    /// </summary>
    public sealed class TunnelNodeDto
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public float PositionX { get; init; }
        public float PositionY { get; init; }
        public float PositionZ { get; init; }
        public float DirX { get; init; }
        public float DirZ { get; init; }
        public float Length { get; init; }
        public int FloorIndex { get; init; }
        public float Slope { get; init; }
        public int SlopeExitSign { get; init; }
        public bool IsSpecial { get; init; }
        public IReadOnlyList<TunnelEntranceMarkerDto> EntranceMarkers { get; init; }
        public IReadOnlyList<bool> OpenEntrances { get; init; }
    }

    /// <summary>
    /// 通常通路DTO。
    /// </summary>
    public sealed class NormalCorridorDto
    {
        public int ConnectionIndex { get; init; }
        public float StartX { get; init; }
        public float StartY { get; init; }
        public float StartZ { get; init; }
        public float EndX { get; init; }
        public float EndY { get; init; }
        public float EndZ { get; init; }
        public float CenterX { get; init; }
        public float CenterY { get; init; }
        public float CenterZ { get; init; }
        public float DirX { get; init; }
        public float DirY { get; init; }
        public float DirZ { get; init; }
        public float Length { get; init; }
        public bool OpenEnd { get; init; } = true;
        public bool OpenLeft { get; init; }
        public bool OpenRight { get; init; }
    }

    /// <summary>
    /// 小部屋接続DTO。
    /// </summary>
    public sealed class SmallRoomDto
    {
        public int RoomNumber { get; init; }
        public int ConnectionIndex { get; init; }
        public float CenterX { get; init; }
        public float CenterY { get; init; }
        public float CenterZ { get; init; }
        public float DirX { get; init; }
        public float DirY { get; init; }
        public float DirZ { get; init; }
        public float TotalLength { get; init; }
        public float Width { get; init; }
        public float RoomLength { get; init; }
        public float PassageLength { get; init; }
    }

    /// <summary>
    /// ワープ通路DTO。
    /// </summary>
    public sealed class WarpCorridorDto
    {
        public int PairId { get; init; }
        public string SideName { get; init; }
        public int TunnelId { get; init; }
        public float StartX { get; init; }
        public float StartY { get; init; }
        public float StartZ { get; init; }
        public float EndX { get; init; }
        public float EndY { get; init; }
        public float EndZ { get; init; }
        public float CenterX { get; init; }
        public float CenterY { get; init; }
        public float CenterZ { get; init; }
        public float DirX { get; init; }
        public float DirY { get; init; }
        public float DirZ { get; init; }
        public float Length { get; init; }
        public int PairedIndex { get; init; } = -1;
    }

    /// <summary>
    /// トンネルや通路の基準寸法DTO。
    /// </summary>
    public sealed class TunnelDimensionsDto
    {
        public float TunnelLength { get; init; }
        public float TunnelWidth { get; init; }
        public float TunnelHeight { get; init; }
        public float CorridorLength { get; init; }
        public float CorridorWidth { get; init; }
        public float ShellThickness { get; init; } = 0.2f;
    }

    /// <summary>
    /// カメラフレーミング用バウンディングボックスDTO。
    /// </summary>
    public sealed class TunnelBoundsDto
    {
        public float CenterX { get; init; }
        public float CenterY { get; init; }
        public float CenterZ { get; init; }
        public float SizeX { get; init; }
        public float SizeY { get; init; }
        public float SizeZ { get; init; }
    }

    /// <summary>
    /// 生成されたトンネルマップ全体のデータDTO。
    /// アプリケーション層からプレゼンテーション層・ビュー層へ純粋なデータとして引き渡される。
    /// </summary>
    public sealed class TunnelMapDto
    {
        public IReadOnlyList<TunnelNodeDto> Tunnels { get; init; }
        public IReadOnlyList<NormalCorridorDto> NormalCorridors { get; init; }
        public IReadOnlyList<SmallRoomDto> SmallRooms { get; init; }
        public IReadOnlyList<WarpCorridorDto> WarpCorridors { get; init; }
        public int? GoalTunnelId { get; init; }
        public int GoalEndSign { get; init; }
        public IReadOnlyList<TunnelGimmickDto> Gimmicks { get; init; } = System.Array.Empty<TunnelGimmickDto>();
        public int? SpecialTunnelId { get; init; }
        public TunnelBoundsDto Bounds { get; init; }
        public TunnelDimensionsDto Dimensions { get; init; }
        public int Seed { get; init; }
        public IReadOnlyList<TunnelDropOpeningDto> DropOpenings { get; init; } = System.Array.Empty<TunnelDropOpeningDto>();
    }

    /// <summary>
    /// 上階の床と下階の天井に設ける落下口のDTO
    /// </summary>
    public sealed class TunnelDropOpeningDto
    {
        public int LowerTunnelId { get; init; }
        public int UpperTunnelId { get; init; }
        public float CenterX { get; init; }
        public float CenterZ { get; init; }
        public float Size { get; init; }
        public float LowerFloorY { get; init; }
        public float UpperFloorY { get; init; }
    }
    /// <summary>生成するギミックの種類</summary>
    public enum TunnelGimmickKindDto { SpiderWeb, FireAlarm, HidingLocker, Car }

    /// <summary>ホスト内のギミック配置情報</summary>
    public sealed class TunnelGimmickDto
    {
        public TunnelGimmickKindDto Kind { get; init; }
        /// <summary>蜘蛛の巣はNormalCorridorsのリスト番号、報知器はトンネル番号</summary>
        public int HostId { get; init; }
        public float LocalX { get; init; }
        public float Yaw { get; init; }
        public float LocalZ { get; init; }
        public int Side { get; init; }
        public int VisualSeed { get; init; }
    }
}
