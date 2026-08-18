using System;

namespace Shinzui.Domain.ValueObjects.Tunnel
{
    /// <summary>
    /// トンネルランダム生成のための設定値オブジェクト（POCO）。
    /// Unityエンジン非依存で、ドメイン層の生成ロジックに渡される。
    /// </summary>
    public sealed class TunnelGenerationConfig
    {
        public int TunnelCount { get; init; } = 8;
        public int Seed { get; init; } = 2777;
        public int PlacementAttemptsPerTunnel { get; init; } = 80;

        public int SmallRoomCount { get; init; } = 1;
        public float SmallRoomWidth { get; init; } = 8.0f;
        public float SmallRoomLength { get; init; } = 6.0f;

        public float TunnelLength { get; init; } = 153.12695f;
        public float TunnelWidth { get; init; } = 14.800003f;
        public float TunnelHeight { get; init; } = 5.0f;
        public float CorridorLength { get; init; } = 8.0f;
        public float CorridorWidth { get; init; } = 3.0f;
        public float PlacementMargin { get; init; } = 2.0f;

        public float TunnelOverlapSizeMultiplier { get; init; } = 1.0f;
        public float CorridorOverlapSizeMultiplier { get; init; } = 1.0f;
        public float SmallRoomOverlapSizeMultiplier { get; init; } = 1.0f;

        public TunnelGenerationConfig() { }

        public TunnelGenerationConfig(
            int tunnelCount,
            int seed,
            int placementAttemptsPerTunnel,
            int smallRoomCount,
            float smallRoomWidth,
            float smallRoomLength,
            float tunnelLength,
            float tunnelWidth,
            float tunnelHeight,
            float corridorLength,
            float corridorWidth,
            float placementMargin,
            float tunnelOverlapSizeMultiplier,
            float corridorOverlapSizeMultiplier,
            float smallRoomOverlapSizeMultiplier)
        {
            TunnelCount = Math.Max(5, tunnelCount);
            Seed = seed;
            PlacementAttemptsPerTunnel = Math.Max(1, placementAttemptsPerTunnel);
            SmallRoomCount = Math.Max(0, smallRoomCount);
            SmallRoomWidth = Math.Max(1.0f, smallRoomWidth);
            SmallRoomLength = Math.Max(1.0f, smallRoomLength);
            TunnelLength = Math.Max(4.0f, tunnelLength);
            TunnelWidth = Math.Max(3.0f, tunnelWidth);
            TunnelHeight = Math.Max(2.0f, tunnelHeight);
            CorridorLength = Math.Max(1.0f, corridorLength);
            CorridorWidth = Math.Max(1.0f, corridorWidth);
            PlacementMargin = Math.Max(0.0f, placementMargin);
            TunnelOverlapSizeMultiplier = Math.Max(0.1f, tunnelOverlapSizeMultiplier);
            CorridorOverlapSizeMultiplier = Math.Max(0.1f, corridorOverlapSizeMultiplier);
            SmallRoomOverlapSizeMultiplier = Math.Max(0.1f, smallRoomOverlapSizeMultiplier);
        }
    }
}
