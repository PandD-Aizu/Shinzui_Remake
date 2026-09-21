using System;
using System.Collections.Generic;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Application.Interfaces.Tunnel;
using Shinzui.View;
using Shinzui.View.GenerateTunnel;
using UnityEngine;

namespace Shinzui.Presentation.GenerateTunnel
{
    /// <summary>
    /// トンネルランダム生成ユースケース（Application）とUnity描画ビュー（View）を仲介するPresenter。
    /// ドメイン層やインフラ層には一切依存せず、DTOとViewメソッドのみを通じてマップ生成とNavMeshベイクを完結させる。
    /// </summary>
    public sealed class GenerateTunnelPresenter : IDisposable
    {
        public TunnelMapDto CurrentMap { get; private set; }
        private readonly IGenerateTunnelUseCase _useCase;
        private readonly TunnelMapView _mapView;
        private readonly ITunnelNavigationBuilder _navigation;
        private readonly List<GeneratedWarpCorridorTrigger> _warpTriggers = new();
        private const float WarpCooldown = 0.35f;
        private float _lastWarpTime = float.NegativeInfinity;

        public GenerateTunnelPresenter(IGenerateTunnelUseCase useCase, TunnelMapView mapView,
            ITunnelNavigationBuilder navigation)
        {
            _useCase = useCase ?? throw new ArgumentNullException(nameof(useCase));
            _mapView = mapView != null ? mapView : throw new ArgumentNullException(nameof(mapView));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        }

        /// <summary>
        /// リクエストパラメータに基づき、一度だけトンネル生成・配置・NavMeshベイクを実行する。
        /// </summary>
        public bool ExecuteGeneration(TunnelGenerationRequestDto request, bool regenerate = false)
        {
            if (_useCase == null || _mapView == null)
            {
                Debug.LogError("[GenerateTunnelPresenter] Cannot execute generation: UseCase or MapView is null.");
                return false;
            }

            request ??= new TunnelGenerationRequestDto();

            // 見本モデルが存在する場合は寸法を実測値で上書きしてリクエストを補正
            float tLen = request.TunnelLength;
            float tWidth = request.TunnelWidth;
            float tHeight = request.TunnelHeight;
            float cLen = request.CorridorLength;
            float cWidth = request.CorridorWidth;

            _mapView.ResolveTemplateDimensions(ref tLen, ref tWidth, ref tHeight, ref cLen, ref cWidth);

            var adjustedRequest = new TunnelGenerationRequestDto
            {
                TunnelCount = request.TunnelCount,
                Seed = request.Seed,
                PlacementAttemptsPerTunnel = request.PlacementAttemptsPerTunnel,
                SmallRoomCount = request.SmallRoomCount,
                SmallRoomWidth = request.SmallRoomWidth,
                SmallRoomLength = request.SmallRoomLength,
                TunnelLength = tLen,
                ConnectionPointSpacing = _mapView.ResolveConnectionPointSpacing(request.ConnectionPointSpacing),
                TunnelWidth = tWidth,
                TunnelHeight = tHeight,
                CorridorLength = cLen,
                CorridorWidth = cWidth,
                PlacementMargin = request.PlacementMargin,
                TunnelOverlapSizeMultiplier = request.TunnelOverlapSizeMultiplier,
                CorridorOverlapSizeMultiplier = request.CorridorOverlapSizeMultiplier,
                SmallRoomOverlapSizeMultiplier = request.SmallRoomOverlapSizeMultiplier
            };

            TunnelMapDto mapDto;
            bool wasGenerated = regenerate
                ? _useCase.Regenerate(adjustedRequest, out mapDto)
                : _useCase.GenerateOnce(adjustedRequest, out mapDto);
            if (!wasGenerated || mapDto == null)
            {
                // 既に生成済みのためスキップ
                return false;
            }

            BuildSceneHierarchy(mapDto);
            if (!_navigation.BuildNavMesh()) return false;
            _mapView.NotifyGeometryReady();
            CurrentMap = mapDto;
            Debug.Log($"[GenerateTunnelPresenter] Generated {mapDto.Tunnels.Count} tunnels with navigation in '{_mapView.gameObject.scene.name}' (seed: {mapDto.Seed}).");
            return true;
        }

        private void BuildSceneHierarchy(TunnelMapDto mapDto)
        {
            Dispose();
            _mapView.PrepareBuild();

            // 1. トンネルノードと出入口候補マーカーの生成
            foreach (TunnelNodeDto tunnel in mapDto.Tunnels)
            {
                Vector3 pos = new Vector3(tunnel.PositionX, tunnel.PositionY, tunnel.PositionZ);
                Transform tunnelRoot = _mapView.CreateTunnelNode(
                    pos,
                    tunnel.Name,
                    tunnel.IsSpecial,
                    mapDto.Dimensions.TunnelWidth,
                    mapDto.Dimensions.TunnelHeight,
                    mapDto.Dimensions.TunnelLength,
                    tunnel.OpenEntrances);

                if (tunnel.EntranceMarkers != null)
                {
                    foreach (TunnelEntranceMarkerDto marker in tunnel.EntranceMarkers)
                    {
                        if (marker.IsActive)
                        {
                            Vector3 localPos = new Vector3(marker.LocalPosX, marker.LocalPosY, marker.LocalPosZ);
                            _mapView.CreateEntranceMarker(tunnelRoot, marker.Name, localPos);
                        }
                    }
                }
            }

            // 2. 通常通路の生成
            foreach (NormalCorridorDto corridor in mapDto.NormalCorridors)
            {
                Vector3 center = new Vector3(corridor.CenterX, corridor.CenterY, corridor.CenterZ);
                Vector3 dir = new Vector3(corridor.DirX, corridor.DirY, corridor.DirZ);

                _mapView.CreateNormalCorridor(
                    center,
                    dir,
                    corridor.Length,
                    corridor.ConnectionIndex,
                    mapDto.Dimensions.CorridorWidth,
                    mapDto.Dimensions.TunnelHeight,
                    mapDto.Dimensions.CorridorLength);
            }

            // 3. 小部屋接続の生成
            foreach (SmallRoomDto room in mapDto.SmallRooms)
            {
                Vector3 center = new Vector3(room.CenterX, room.CenterY, room.CenterZ);
                Vector3 dir = new Vector3(room.DirX, room.DirY, room.DirZ);

                _mapView.CreateSmallRoomConnection(
                    center,
                    dir,
                    room.RoomNumber,
                    room.Width,
                    room.RoomLength,
                    room.PassageLength,
                    mapDto.Dimensions.CorridorWidth,
                    mapDto.Dimensions.TunnelHeight,
                    mapDto.Dimensions.CorridorLength);
            }

            // 4. ワープ通路の生成とペアリング
            var createdWarpInfos = new List<GeneratedCorridorInfo>(mapDto.WarpCorridors.Count);
            foreach (WarpCorridorDto warp in mapDto.WarpCorridors)
            {
                Vector3 center = new Vector3(warp.CenterX, warp.CenterY, warp.CenterZ);
                Vector3 dir = new Vector3(warp.DirX, warp.DirY, warp.DirZ);

                GeneratedCorridorInfo info = _mapView.CreateWarpCorridor(
                    center,
                    dir,
                    warp.Length,
                    warp.PairId,
                    warp.SideName,
                    mapDto.Dimensions.CorridorWidth,
                    mapDto.Dimensions.TunnelHeight,
                    mapDto.Dimensions.CorridorLength);

                createdWarpInfos.Add(info);
            }

            for (int i = 0; i < mapDto.WarpCorridors.Count; i++)
            {
                int pairedIndex = mapDto.WarpCorridors[i].PairedIndex;
                if (pairedIndex >= 0 && pairedIndex < createdWarpInfos.Count)
                {
                    createdWarpInfos[i].SetPair(createdWarpInfos[pairedIndex]);
                }
                var trigger = createdWarpInfos[i].GetComponentInChildren<GeneratedWarpCorridorTrigger>();
                if (trigger != null)
                {
                    trigger.PlayerEntered += OnWarpRequested;
                    _warpTriggers.Add(trigger);
                }
            }

            // 5. カメラフレーミング
            if (mapDto.Bounds != null)
            {
                Vector3 boundsCenter = new Vector3(mapDto.Bounds.CenterX, mapDto.Bounds.CenterY, mapDto.Bounds.CenterZ);
                Vector3 boundsSize = new Vector3(mapDto.Bounds.SizeX, mapDto.Bounds.SizeY, mapDto.Bounds.SizeZ);
                _mapView.FrameSceneCamera(boundsCenter, boundsSize);
            }

        }

        private void OnWarpRequested(GeneratedCorridorInfo source, PlayerView player)
        {
            if (source == null || source.PairedCorridor == null || player == null ||
                player.gameObject.scene != _mapView.gameObject.scene || Time.time - _lastWarpTime < WarpCooldown)
                return;

            Transform target = source.PairedCorridor.transform;
            player.Warp(target.position - target.forward * 1.25f - source.transform.position);
            _lastWarpTime = Time.time;
        }

        public void Dispose()
        {
            foreach (var trigger in _warpTriggers)
                if (trigger != null) trigger.PlayerEntered -= OnWarpRequested;
            _warpTriggers.Clear();
        }
    }
}
