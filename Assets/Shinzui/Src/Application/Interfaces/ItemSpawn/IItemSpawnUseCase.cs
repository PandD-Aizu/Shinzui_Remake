using System.Collections.Generic;
using Shinzui.Application.DTOs.ItemSpawn;
using Shinzui.Domain.DomainServices.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;

namespace Shinzui.Application.Interfaces.ItemSpawn
{
    /// <summary>
    /// アイテムスポーンユースケースのインターフェース
    /// </summary>
    public interface IItemSpawnUseCase
    {
        ItemSpawnResultDto ExecuteSpawnPlanning(
            ItemSpawnConfigDto configDto,
            IReadOnlyList<ItemSpawnTargetDto> targetsDto,
            IReadOnlyList<SpawnSurfaceDataDto> surfacesDto,
            INeedWeightProvider needWeightProvider = null,
            IExternalPlacementValidator externalValidator = null);

        ItemSpawnResultDto ExecuteSpawn(
            ItemSpawnSettingsDto settingsDto,
            IReadOnlyList<ItemSpawnDefinitionDto> itemDefinitionsDto,
            IReadOnlyList<SpawnSurfaceTriangleDto> trianglesDto,
            int seed,
            ISpawnPositionValidator customValidator = null);

        ItemSpawnResultDto ExecuteSpawn(
            ItemSpawnSettingsDto settingsDto,
            IReadOnlyList<ItemSpawnDefinitionDto> itemDefinitionsDto,
            IReadOnlyList<SpawnSurfaceTriangle> domainTriangles,
            int seed,
            ISpawnPositionValidator customValidator = null);

        ItemSpawnResultDto ExecuteSpawn(
            ItemSpawnSettingsDto settingsDto,
            IReadOnlyList<ItemSpawnDefinitionDto> itemDefinitionsDto,
            ISpawnSurfaceDataProvider surfaceDataProvider,
            int seed,
            ISpawnPositionValidator customValidator = null);
    }
}
