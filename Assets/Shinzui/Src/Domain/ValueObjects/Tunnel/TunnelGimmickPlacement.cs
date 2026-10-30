namespace Shinzui.Domain.ValueObjects.Tunnel
{
    /// <summary>自動配置するギミックの種類</summary>
    public enum TunnelGimmickKind { SpiderWeb, FireAlarm, HidingLocker, Car }

    /// <summary>ホストの中心を原点とする配置スロット</summary>
    public sealed class TunnelGimmickPlacement
    {
        public TunnelGimmickKind Kind { get; }
        public int HostId { get; }
        public float LocalX { get; }
        public float Yaw { get; }
        public float LocalZ { get; }
        public int Side { get; }
        public int VisualSeed { get; }

        /// <summary>配置と外観シードを保持する</summary>
        /// <param name="kind">ギミックの種類</param>
        /// <param name="hostId">通常通路リストの区間番号またはトンネル番号</param>
        /// <param name="localZ">ホスト内の長さ方向の位置</param>
        /// <param name="side">側壁の符号</param>
        /// <param name="visualSeed">外観のシード</param>
        /// <param name="localX">幅方向の位置</param>
        /// <param name="yaw">水平回転角</param>
        public TunnelGimmickPlacement(TunnelGimmickKind kind, int hostId, float localZ, int side, int visualSeed,
            float localX = 0f, float yaw = 0f)
        {
            LocalX = localX;
            Yaw = yaw;
            Kind = kind;
            HostId = hostId;
            LocalZ = localZ;
            Side = side;
            VisualSeed = visualSeed;
        }
    }
}
