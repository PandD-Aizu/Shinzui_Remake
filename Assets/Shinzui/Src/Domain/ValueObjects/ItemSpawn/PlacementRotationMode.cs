namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// アイテム配置時の回転姿勢決定方針
    /// </summary>
    public enum PlacementRotationMode
    {
        /// <summary>サーフェス法線を上方向とし、法線軸周りにランダムYaw回転を適用（既定）</summary>
        AlignWithNormalAndRandomYaw,

        /// <summary>サーフェス法線を上方向とし、追加Yaw回転なし（法線のみに自然に合わせる）</summary>
        AlignWithNormal,

        /// <summary>ワールド上方向（Y軸）を基準としてランダムYaw回転のみ適用（水平配置）</summary>
        RandomYawOnly,

        /// <summary>3軸完全ランダム回転</summary>
        FullRandomRotation,

        /// <summary>回転なし（Identity）</summary>
        None
    }
}
