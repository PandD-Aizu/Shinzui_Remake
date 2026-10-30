using Shinzui.Application.DTOs.Tunnel;
using Shinzui.View.GenerateTunnel;

namespace Shinzui.DI.GenerateTunnel
{
    internal static class TunnelGenerationRequestFactory
    {
        /// <summary>Inspectorの設定から生成リクエストを作る</summary>
        /// <param name="tunnelGenerator">シーンの生成設定</param>
        /// <param name="seed">再生成時に上書きするシード</param>
        /// <returns>配置数と寸法を含む生成リクエスト</returns>
        public static TunnelGenerationRequestDto Create(TunnelGenerator tunnelGenerator, int? seed = null)
        {
            if (tunnelGenerator != null)
            {
                return new TunnelGenerationRequestDto
                {
                    SpiderWebCount = tunnelGenerator.SpiderWebCount,
                    CarCount = tunnelGenerator.CarCount,
                    CarMinSpacing = tunnelGenerator.CarMinSpacing,
                    HidingLockerCount = tunnelGenerator.HidingLockerCount,
                    HidingLockerMinSpacing = tunnelGenerator.HidingLockerMinSpacing,
                    FireAlarmCount = tunnelGenerator.FireAlarmCount,
                    FireAlarmMinSpacing = tunnelGenerator.FireAlarmMinSpacing,
                    FireAlarmEndMargin = tunnelGenerator.FireAlarmEndMargin,
                    FireAlarmMouthMargin = tunnelGenerator.FireAlarmMouthMargin,
                    TunnelCount = tunnelGenerator.TunnelCount,
                    Seed = seed ?? tunnelGenerator.Seed,
                    PlacementAttemptsPerTunnel = tunnelGenerator.PlacementAttemptsPerTunnel,
                    GenerateUpperFloor = tunnelGenerator.GenerateUpperFloor,
                    UpperFloorHeight = tunnelGenerator.UpperFloorHeight,
                    GenerateLowerFloor = tunnelGenerator.GenerateLowerFloor,
                    LowerFloorDepth = tunnelGenerator.LowerFloorDepth,
                    DropOpeningCount = tunnelGenerator.DropOpeningCount,
                    DropOpeningSize = tunnelGenerator.DropOpeningSize,
                    SmallRoomCount = tunnelGenerator.SmallRoomCount,
                    SmallRoomWidth = tunnelGenerator.SmallRoomWidth,
                    SmallRoomLength = tunnelGenerator.SmallRoomLength,
                    TunnelLength = tunnelGenerator.TunnelLength,
                    MinTunnelLength = tunnelGenerator.MinTunnelLength,
                    MaxTunnelLength = tunnelGenerator.MaxTunnelLength,
                    ConnectionPointSpacing = tunnelGenerator.ConnectionPointSpacing,
                    TunnelWidth = tunnelGenerator.TunnelWidth,
                    TunnelHeight = tunnelGenerator.TunnelHeight,
                    CorridorLength = tunnelGenerator.CorridorLength,
                    CorridorWidth = tunnelGenerator.CorridorWidth,
                    PlacementMargin = tunnelGenerator.PlacementMargin,
                    TunnelOverlapSizeMultiplier = tunnelGenerator.TunnelOverlapSizeMultiplier,
                    CorridorOverlapSizeMultiplier = tunnelGenerator.CorridorOverlapSizeMultiplier,
                    SmallRoomOverlapSizeMultiplier = tunnelGenerator.SmallRoomOverlapSizeMultiplier
                };
            }

            return new TunnelGenerationRequestDto { Seed = seed ?? 2777 };
        }

    }
}
