using System;
using System.Collections.Generic;
using Shinzui.Application.DTOs.ItemSpawn;
using Shinzui.Application.Interfaces.ItemSpawn;
using Shinzui.View.ItemSpawn;
using UnityEngine;

namespace Shinzui.Presentation.ItemSpawn
{
    /// <summary>
    /// アイテムスポーンユースケース（Application）とUnity描画ビュー（View）を仲介するPresenter。
    /// サーフェスからの三角形データの抽出、ユースケース呼び出し、生成された座標・回転へのPrefab配置を実行する。
    /// ドメイン層へは直接依存せず、DTOとインターフェースのみで完結する。
    /// </summary>
    public sealed class ItemSpawnPresenter
    {
        private readonly IItemSpawnUseCase _useCase;
        private readonly ItemSpawnContainerView _containerView;
        private readonly INeedWeightProvider _needWeightProvider;
        private readonly IExternalPlacementValidator _externalValidator;

        public ItemSpawnResultDto LastRunResult { get; private set; }

        public ItemSpawnPresenter(
            IItemSpawnUseCase useCase,
            ItemSpawnContainerView containerView,
            INeedWeightProvider needWeightProvider = null,
            IExternalPlacementValidator externalValidator = null)
        {
            _useCase = useCase;
            _containerView = containerView;
            _needWeightProvider = needWeightProvider;
            _externalValidator = externalValidator;
        }

        /// <summary>
        /// 指定された設定・対象定義・サーフェスに基づいてアイテムスポーンを実行し、シーン上に実体を生成する。
        /// </summary>
        public ItemSpawnResultDto ExecuteSpawn(
            ItemSpawnConfigDto configDto,
            IReadOnlyList<ItemSpawnTargetDto> targetsDto,
            IReadOnlyDictionary<string, GameObject> prefabMap)
        {
            if (_useCase == null)
            {
                Debug.LogError("[ItemSpawnPresenter] Cannot execute spawn: UseCase is null.");
                return null;
            }

            if (_containerView == null)
            {
                Debug.LogError("[ItemSpawnPresenter] Cannot execute spawn: ContainerView is null.");
                return null;
            }

            // 既存の生成アイテムをクリア
            _containerView.ClearAllSpawned();

            // Viewからサーフェスデータを抽出してDTOへ変換
            var surfaceDataDtos = CollectSurfaceDtos(_containerView.GetSurfaces());

            // ユースケースによる配置計画の算出
            LastRunResult = _useCase.ExecuteSpawnPlanning(
                configDto,
                targetsDto,
                surfaceDataDtos,
                _needWeightProvider,
                _externalValidator);

            if (LastRunResult == null || LastRunResult.SpawnedItems == null)
            {
                Debug.LogWarning("[ItemSpawnPresenter] Item spawn planning returned null or empty result.");
                return LastRunResult;
            }

            // Prefabの配置実体化
            for (int i = 0; i < LastRunResult.SpawnedItems.Count; i++)
            {
                SpawnedItemDto item = LastRunResult.SpawnedItems[i];
                if (item == null) continue;

                if (prefabMap == null || !prefabMap.TryGetValue(item.ItemId, out GameObject prefab) || prefab == null)
                {
                    Debug.LogWarning($"[ItemSpawnPresenter] Prefab for item '{item.ItemId}' not found in prefab map.");
                    continue;
                }

                Vector3 position = item.WorldPosition;
                Quaternion rotation = item.WorldRotation;

                _containerView.SpawnItem(prefab, position, rotation);
            }

            return LastRunResult;
        }

        public void ClearSpawned()
        {
            if (_containerView != null)
            {
                _containerView.ClearAllSpawned();
            }
            LastRunResult = null;
        }

        private static List<SpawnSurfaceDataDto> CollectSurfaceDtos(IReadOnlyList<WeightedSpawnSurface> surfaces)
        {
            var dtos = new List<SpawnSurfaceDataDto>();
            if (surfaces == null) return dtos;

            for (int s = 0; s < surfaces.Count; s++)
            {
                WeightedSpawnSurface surface = surfaces[s];
                if (surface == null || !surface.IsDataValid) continue;

                Matrix4x4 w2l = surface.transform.worldToLocalMatrix;
                var surfaceDto = new SpawnSurfaceDataDto
                {
                    SurfaceId = surface.gameObject.name,
                    RegionWeight = surface.RegionWeight,
                    IsValid = surface.IsDataValid,
                    MapResolution = surface.MapResolution,
                    WeightMap = surface.CopyWeightMap(),
                    BoundsMin = surface.LocalBounds.min,
                    BoundsSize = surface.LocalBounds.size,
                    ProjectionPlane = surface.ProjectionPlane switch
                    {
                        SpawnSurfaceProjection.XY => SpawnSurfaceProjectionDto.XY,
                        SpawnSurfaceProjection.YZ => SpawnSurfaceProjectionDto.YZ,
                        _ => SpawnSurfaceProjectionDto.XZ
                    },
                    WorldToLocalMatrix = new float[]
                    {
                        w2l.m00, w2l.m01, w2l.m02, w2l.m03,
                        w2l.m10, w2l.m11, w2l.m12, w2l.m13,
                        w2l.m20, w2l.m21, w2l.m22, w2l.m23,
                        w2l.m30, w2l.m31, w2l.m32, w2l.m33
                    },
                    TransformPosition = surface.transform.position,
                    TransformScale = surface.transform.lossyScale,
                    Triangles = new List<SpawnSurfaceTriangleDto>()
                };

                bool hasWeightMap = surfaceDto.WeightMap != null && surfaceDto.WeightMap.Length > 0;
                int triCount = surface.TriangleCount;
                for (int t = 0; t < triCount; t++)
                {
                    if (surface.TryGetTriangleData(t, out Vector3 a, out Vector3 b, out Vector3 c, out float weight))
                    {
                        // 2D Weight Mapが存在する場合は幾何候補として必ず有効化(PaintWeight=1f)し、Rejection Samplingで判定
                        // 旧方式のみの場合は weight > 0f のみを追加
                        if (hasWeightMap)
                        {
                            surfaceDto.Triangles.Add(new SpawnSurfaceTriangleDto
                            {
                                VertexA = a,
                                VertexB = b,
                                VertexC = c,
                                PaintWeight = 1.0f,
                                RegionWeight = surface.RegionWeight,
                                SurfaceName = surfaceDto.SurfaceId,
                                TriangleIndex = t
                            });
                        }
                        else if (weight > 0f)
                        {
                            surfaceDto.Triangles.Add(new SpawnSurfaceTriangleDto
                            {
                                VertexA = a,
                                VertexB = b,
                                VertexC = c,
                                PaintWeight = weight,
                                RegionWeight = surface.RegionWeight,
                                SurfaceName = surfaceDto.SurfaceId,
                                TriangleIndex = t
                            });
                        }
                    }
                }

                if (surfaceDto.Triangles.Count > 0)
                {
                    dtos.Add(surfaceDto);
                }
            }

            return dtos;
        }
    }
}
