namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// スポーン時のアイテム回転・姿勢方針
    /// </summary>
    public enum ItemSpawnRotationPolicy
    {
        /// <summary>
        /// サーフェス法線にUpベクトルを合わせ、法線周りにランダムYaw回転を付与
        /// </summary>
        AlignToSurfaceNormalWithRandomYaw,

        /// <summary>
        /// サーフェス法線にUpベクトルを合わせ、Yaw回転は固定（0度）
        /// </summary>
        AlignToSurfaceNormal,

        /// <summary>
        /// ワールドUp方向を維持し、ランダムYaw回転
        /// </summary>
        WorldUpWithRandomYaw,

        /// <summary>
        /// 完全ランダムな3軸回転（地面に転がるような小物向け）
        /// </summary>
        FullRandomRotation
    }
}
