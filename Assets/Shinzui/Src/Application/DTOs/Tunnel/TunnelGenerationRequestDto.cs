using Shinzui.Domain.ValueObjects.Tunnel;

namespace Shinzui.Application.DTOs.Tunnel
{
    /// <summary>
    /// トンネル生成リクエストDTO。
    /// 外部層（Presentation/Bootstrap）からユースケースへ渡される生成パラメータ。
    /// </summary>
    public sealed class TunnelGenerationRequestDto
    {
        public int SpiderWebCount { get; init; } = 3;
        public int CarCount { get; init; } = 4;
        public float CarPlacementWidth { get; init; }
        public float CarMinSpacing { get; init; } = 15f;
        public int HidingLockerCount { get; init; } = 4;
        public float HidingLockerMinSpacing { get; init; } = 25f;
        public int FireAlarmCount { get; init; } = 3;
        public float FireAlarmMinSpacing { get; init; } = 40.0f;
        public float FireAlarmEndMargin { get; init; } = 12.0f;
        public float FireAlarmMouthMargin { get; init; } = 4.0f;

        public int TunnelCount { get; init; } = 8;
        public int Seed { get; init; } = 2777;
        public int PlacementAttemptsPerTunnel { get; init; } = 80;
        public bool GenerateUpperFloor { get; init; }
        public float UpperFloorHeight { get; init; } = 12.0f;
        public bool GenerateLowerFloor { get; init; }
        public float LowerFloorDepth { get; init; } = 12.0f;
        public int DropOpeningCount { get; init; } = 2;
        public float DropOpeningSize { get; init; } = 3.0f;

        public int SmallRoomCount { get; init; } = 1;
        public float SmallRoomWidth { get; init; } = 8.0f;
        public float SmallRoomLength { get; init; } = 6.0f;

        public float TunnelLength { get; init; } = 153.12695f;
        public float MinTunnelLength { get; init; } = 153.12695f;
        public float MaxTunnelLength { get; init; } = 153.12695f;
        public float ConnectionPointSpacing { get; init; } = 51.042316f;
        public float TunnelWidth { get; init; } = 14.800003f;
        public float TunnelHeight { get; init; } = 5.0f;
        public float CorridorLength { get; init; } = 8.0f;
        public float CorridorWidth { get; init; } = 3.0f;
        public float PlacementMargin { get; init; } = 2.0f;

        public float TunnelOverlapSizeMultiplier { get; init; } = 1.0f;
        public float CorridorOverlapSizeMultiplier { get; init; } = 1.0f;
        public float SmallRoomOverlapSizeMultiplier { get; init; } = 1.0f;

        /// <summary>寸法とギミック配置設定をドメインへ渡す</summary>
        /// <returns>生成用の設定</returns>
        public TunnelGenerationConfig ToDomainConfig()
        {
            float minTunnelLength = MinTunnelLength;
            float maxTunnelLength = MaxTunnelLength;
            if (MinTunnelLength == 153.12695f && MaxTunnelLength == 153.12695f && TunnelLength != 153.12695f)
            {
                minTunnelLength = TunnelLength;
                maxTunnelLength = TunnelLength;
            }

            return new TunnelGenerationConfig(
                TunnelCount,
                Seed,
                PlacementAttemptsPerTunnel,
                SmallRoomCount,
                SmallRoomWidth,
                SmallRoomLength,
                TunnelLength,
                minTunnelLength,
                maxTunnelLength,
                ConnectionPointSpacing,
                TunnelWidth,
                TunnelHeight,
                CorridorLength,
                CorridorWidth,
                PlacementMargin,
                TunnelOverlapSizeMultiplier,
                CorridorOverlapSizeMultiplier,
                SmallRoomOverlapSizeMultiplier,
                GenerateUpperFloor,
                UpperFloorHeight,
                DropOpeningCount,
                DropOpeningSize,
                GenerateLowerFloor,
                LowerFloorDepth)
            {
                SpiderWebCount = SpiderWebCount,
                CarCount = CarCount,
                CarPlacementWidth = CarPlacementWidth,
                CarMinSpacing = CarMinSpacing,
                HidingLockerCount = HidingLockerCount,
                HidingLockerMinSpacing = HidingLockerMinSpacing,
                FireAlarmCount = FireAlarmCount,
                FireAlarmMinSpacing = FireAlarmMinSpacing,
                FireAlarmEndMargin = FireAlarmEndMargin,
                FireAlarmMouthMargin = FireAlarmMouthMargin,
            };
        }
    }
}
