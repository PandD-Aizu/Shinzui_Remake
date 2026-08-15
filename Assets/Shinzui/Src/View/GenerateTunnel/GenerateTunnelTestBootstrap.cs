using UnityEngine;

namespace Shinzui.View.GenerateTunnel
{
    /// <summary>
    /// シーンおよびインスペクター上でトンネル生成パラメータを保持・設定するためのView設定コンポーネント。
    /// 既存のシリアライズ値やシーン参照との互換性を完全に維持する。
    /// ロジックやドメインへの依存を持たず、純粋なView層の設定保持のみを担う。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class GenerateTunnelTestBootstrap : MonoBehaviour
    {
        [Header("Generation")]
        [Min(5)] [SerializeField] private int tunnelCount = 8;
        [SerializeField] private int seed = 2777;
        [Min(1)] [SerializeField] private int placementAttemptsPerTunnel = 80;

        [Header("Small rooms")]
        [Min(0)] [SerializeField] private int smallRoomCount = 1;
        [Min(1.0f)] [SerializeField] private float smallRoomWidth = 8.0f;
        [Min(1.0f)] [SerializeField] private float smallRoomLength = 6.0f;

        [Header("Fixed tunnel size")]
        [Min(4.0f)] [SerializeField] private float tunnelLength = 153.12695f;
        [Min(3.0f)] [SerializeField] private float tunnelWidth = 14.800003f;
        [Min(2.0f)] [SerializeField] private float tunnelHeight = 5.0f;
        [Min(1.0f)] [SerializeField] private float corridorLength = 8.0f;
        [Min(1.0f)] [SerializeField] private float corridorWidth = 3.0f;
        [Min(0.0f)] [SerializeField] private float placementMargin = 2.0f;

        [Header("Overlap prevention")]
        [Min(0.1f)] [SerializeField] private float tunnelOverlapSizeMultiplier = 1.0f;
        [Min(0.1f)] [SerializeField] private float corridorOverlapSizeMultiplier = 1.0f;
        [Min(0.1f)] [SerializeField] private float smallRoomOverlapSizeMultiplier = 1.0f;

        public int TunnelCount => tunnelCount;
        public int Seed => seed;
        public int PlacementAttemptsPerTunnel => placementAttemptsPerTunnel;
        public int SmallRoomCount => smallRoomCount;
        public float SmallRoomWidth => smallRoomWidth;
        public float SmallRoomLength => smallRoomLength;
        public float TunnelLength => tunnelLength;
        public float TunnelWidth => tunnelWidth;
        public float TunnelHeight => tunnelHeight;
        public float CorridorLength => corridorLength;
        public float CorridorWidth => corridorWidth;
        public float PlacementMargin => placementMargin;
        public float TunnelOverlapSizeMultiplier => tunnelOverlapSizeMultiplier;
        public float CorridorOverlapSizeMultiplier => corridorOverlapSizeMultiplier;
        public float SmallRoomOverlapSizeMultiplier => smallRoomOverlapSizeMultiplier;
    }
}
