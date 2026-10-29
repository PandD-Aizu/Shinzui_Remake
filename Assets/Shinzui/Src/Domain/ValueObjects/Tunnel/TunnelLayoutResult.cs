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
        public TunnelVector2 Forward { get; }
        public float Length { get; }
        public int FloorIndex { get; }
        public float Slope { get; }
        public int SlopeExitSign { get; }
        public bool IsSpecial { get; set; }
        public IReadOnlyList<TunnelEntranceMarkerLayout> EntranceMarkers { get; }
        public IReadOnlyList<bool> OpenEntrances { get; }

        /// <summary>
        /// 基本トンネルの配置と接続口と階層を保持する
        /// </summary>
        /// <param name="id">トンネル番号</param>
        /// <param name="name">表示名</param>
        /// <param name="position">中心位置</param>
        /// <param name="forward">水平の長さ方向</param>
        /// <param name="length">トンネル長</param>
        /// <param name="isSpecial">特殊トンネルか</param>
        /// <param name="entranceMarkers">側面口の配置</param>
        /// <param name="openEntrances">使用する側面口</param>
        /// <param name="floorIndex">所属階層</param>
        /// <param name="slope">水平距離あたりの高さの変化</param>
        /// <param name="slopeExitSign">別階層につながる長さ方向の端を示す符号</param>
        public TunnelNodeLayout(
            int id,
            string name,
            TunnelVector3 position,
            TunnelVector2 forward,
            float length,
            bool isSpecial,
            IReadOnlyList<TunnelEntranceMarkerLayout> entranceMarkers,
            IReadOnlyList<bool> openEntrances = null,
            int floorIndex = 0,
            float slope = 0.0f,
            int slopeExitSign = 0)
        {
            Id = id;
            Name = name;
            Position = position;
            Forward = forward;
            Length = length;
            IsSpecial = isSpecial;
            EntranceMarkers = entranceMarkers;
            OpenEntrances = openEntrances;
            FloorIndex = floorIndex;
            Slope = slope;
            SlopeExitSign = slopeExitSign;
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
        public bool OpenEnd { get; }
        public bool OpenLeft { get; }
        public bool OpenRight { get; }

        /// <summary>
        /// 通路区間の寸法と接続端と開口部を保持する
        /// </summary>
        /// <param name="connectionIndex">接続の識別番号</param>
        /// <param name="startPort">入口の位置</param>
        /// <param name="endPort">出口の位置</param>
        /// <param name="center">区間の中心</param>
        /// <param name="direction">区間の前方向</param>
        /// <param name="length">区間の長さ</param>
        /// <param name="openEnd">前方を開けるか</param>
        /// <param name="openLeft">左側を開けるか</param>
        /// <param name="openRight">右側を開けるか</param>
        public NormalCorridorLayout(
            int connectionIndex,
            TunnelVector3 startPort,
            TunnelVector3 endPort,
            TunnelVector3 center,
            TunnelVector3 direction,
            float length,
            bool openEnd = true,
            bool openLeft = false,
            bool openRight = false)
        {
            ConnectionIndex = connectionIndex;
            StartPort = startPort;
            EndPort = endPort;
            Center = center;
            Direction = direction;
            Length = length;
            OpenEnd = openEnd;
            OpenLeft = openLeft;
            OpenRight = openRight;
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
        public IReadOnlyList<TunnelDropOpeningLayout> DropOpenings { get; }

        /// <summary>
        /// 全階層のトンネルと通路と落下口を保持する
        /// </summary>
        /// <param name="tunnels">基本トンネル</param>
        /// <param name="normalCorridors">通常通路</param>
        /// <param name="smallRooms">小部屋</param>
        /// <param name="warpCorridors">ワープ通路</param>
        /// <param name="specialTunnelId">特殊トンネルの番号</param>
        /// <param name="bounds">マップ全体の範囲</param>
        /// <param name="seed">生成シード</param>
        /// <param name="dropOpenings">階層間の落下口</param>
        public TunnelLayoutResult(
            IReadOnlyList<TunnelNodeLayout> tunnels,
            IReadOnlyList<NormalCorridorLayout> normalCorridors,
            IReadOnlyList<SmallRoomLayout> smallRooms,
            IReadOnlyList<WarpCorridorLayout> warpCorridors,
            int? specialTunnelId,
            TunnelBoundsLayout bounds,
            int seed,
            IReadOnlyList<TunnelDropOpeningLayout> dropOpenings = null)
        {
            Tunnels = tunnels;
            NormalCorridors = normalCorridors;
            SmallRooms = smallRooms;
            WarpCorridors = warpCorridors;
            SpecialTunnelId = specialTunnelId;
            Bounds = bounds;
            Seed = seed;
            DropOpenings = dropOpenings ?? System.Array.Empty<TunnelDropOpeningLayout>();
        }
    }

    /// <summary>
    /// 上階の床と下階の天井をつなぐ落下口の配置
    /// </summary>
    public sealed class TunnelDropOpeningLayout
    {
        public int LowerTunnelId { get; }
        public int UpperTunnelId { get; }
        public TunnelVector3 Center { get; }
        public float Size { get; }
        public float LowerFloorY { get; }
        public float UpperFloorY { get; }

        /// <summary>
        /// 落下口と着地先のトンネルを保持する
        /// </summary>
        /// <param name="lowerTunnelId">着地先のトンネル番号</param>
        /// <param name="upperTunnelId">落下元のトンネル番号</param>
        /// <param name="center">落下口の水平中心</param>
        /// <param name="size">正方形の開口幅</param>
        /// <param name="lowerFloorY">着地面の高さ</param>
        /// <param name="upperFloorY">落下元の床の高さ</param>
        public TunnelDropOpeningLayout(int lowerTunnelId, int upperTunnelId, TunnelVector3 center,
            float size, float lowerFloorY, float upperFloorY)
        {
            LowerTunnelId = lowerTunnelId;
            UpperTunnelId = upperTunnelId;
            Center = center;
            Size = size;
            LowerFloorY = lowerFloorY;
            UpperFloorY = upperFloorY;
        }
    }
}
