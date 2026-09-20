using Shinzui.Application.DTOs.Tunnel;
using Shinzui.View.GenerateTunnel;

namespace Shinzui.DI.GenerateTunnel
{
    internal static class TunnelGenerationRequestFactory
    {
        public static TunnelGenerationRequestDto Create(TunnelGenerator tunnelGenerator, int? seed = null)
        {
            if (tunnelGenerator != null)
            {
                return new TunnelGenerationRequestDto
                {
                    TunnelCount = tunnelGenerator.TunnelCount,
                    Seed = seed ?? tunnelGenerator.Seed,
                    PlacementAttemptsPerTunnel = tunnelGenerator.PlacementAttemptsPerTunnel,
                    SmallRoomCount = tunnelGenerator.SmallRoomCount,
                    SmallRoomWidth = tunnelGenerator.SmallRoomWidth,
                    SmallRoomLength = tunnelGenerator.SmallRoomLength,
                    TunnelLength = tunnelGenerator.TunnelLength,
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
