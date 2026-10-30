using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <param name="request">生成設定</param>
        /// <param name="regenerate">既存の生成物を置き換えるか</param>
        /// <returns>生成とNavMesh構築に成功したか</returns>
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
            float minTunnelLength = Mathf.Approximately(request.MinTunnelLength, request.TunnelLength) ? tLen : request.MinTunnelLength;
            float maxTunnelLength = Mathf.Approximately(request.MaxTunnelLength, request.TunnelLength) ? tLen : request.MaxTunnelLength;

            var adjustedRequest = new TunnelGenerationRequestDto
            {
                SpiderWebCount = request.SpiderWebCount,
                CarCount = request.CarCount,
                CarPlacementWidth = _mapView.ResolveCarPlacementWidth(tWidth),
                CarMinSpacing = request.CarMinSpacing,
                HidingLockerCount = request.HidingLockerCount,
                HidingLockerMinSpacing = request.HidingLockerMinSpacing,
                FireAlarmCount = request.FireAlarmCount,
                FireAlarmMinSpacing = request.FireAlarmMinSpacing,
                FireAlarmEndMargin = request.FireAlarmEndMargin,
                FireAlarmMouthMargin = request.FireAlarmMouthMargin,
                TunnelCount = request.TunnelCount,
                Seed = request.Seed,
                PlacementAttemptsPerTunnel = request.PlacementAttemptsPerTunnel,
                GenerateUpperFloor = request.GenerateUpperFloor,
                UpperFloorHeight = request.UpperFloorHeight,
                GenerateLowerFloor = request.GenerateLowerFloor,
                LowerFloorDepth = request.LowerFloorDepth,
                DropOpeningCount = request.DropOpeningCount,
                DropOpeningSize = request.DropOpeningSize,
                SmallRoomCount = request.SmallRoomCount,
                SmallRoomWidth = request.SmallRoomWidth,
                SmallRoomLength = request.SmallRoomLength,
                TunnelLength = tLen,
                MinTunnelLength = minTunnelLength,
                MaxTunnelLength = maxTunnelLength,
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

        /// <summary>
        /// 全階層のモデルを生成し傾斜と落下口をつなぐ
        /// </summary>
        /// <param name="mapDto">生成済みの配置情報</param>
        private void BuildSceneHierarchy(TunnelMapDto mapDto)
        {
            Dispose();
            _mapView.PrepareBuild();
            var tunnelRoots = new Dictionary<int, Transform>();
            var corridorRoots = new Dictionary<int, Transform>();
            var roomRoots = new Dictionary<int, Transform>();

            // 1. トンネルノードと出入口候補マーカーの生成
            foreach (TunnelNodeDto tunnel in mapDto.Tunnels.OrderBy(t => t.Slope != 0.0f))
            {
                Vector3 pos = new Vector3(tunnel.PositionX, tunnel.PositionY, tunnel.PositionZ);
                Vector3 forward = new Vector3(tunnel.DirX, 0.0f, tunnel.DirZ);
                float slopeEntranceZ = 0.0f;
                Transform upperLanding = null;
                int upperEntranceIndex = 1;
                if (tunnel.Slope != 0.0f && tunnel.EntranceMarkers != null)
                {
                    foreach (var marker in tunnel.EntranceMarkers)
                        if (marker.IsOpen) slopeEntranceZ = marker.LocalPosZ;

                    // 接続先の階層の実モデルを先に作り、傾斜の出口を取得する
                    float halfLength = tunnel.Length / Mathf.Sqrt(1.0f + tunnel.Slope * tunnel.Slope) * 0.5f;
                    int exitSign = tunnel.SlopeExitSign != 0 ? tunnel.SlopeExitSign : (int)Mathf.Sign(tunnel.Slope);
                    Vector3 high = pos + (forward + Vector3.up * tunnel.Slope) * (halfLength * exitSign);
                    foreach (var upper in mapDto.Tunnels.Where(t => t.FloorIndex != tunnel.FloorIndex && t.Slope == 0.0f))
                    {
                        Transform upperRoot = tunnelRoots[upper.Id];
                        foreach (var marker in upper.EntranceMarkers)
                        {
                            Vector3 port = upperRoot.localPosition + upperRoot.localRotation
                                * new Vector3(marker.LocalPosX, 0, marker.LocalPosZ);
                            if (!marker.IsOpen || (port - high).sqrMagnitude > 0.0001f) continue;
                            upperLanding = upperRoot;
                            upperEntranceIndex = marker.Index;
                        }
                    }
                }
                Transform tunnelRoot = _mapView.CreateTunnelNode(
                    pos,
                    forward,
                    tunnel.Name,
                    tunnel.IsSpecial,
                    mapDto.Dimensions.TunnelWidth,
                    mapDto.Dimensions.TunnelHeight,
                    tunnel.Length,
                    mapDto.Dimensions.TunnelLength,
                    tunnel.OpenEntrances,
                    tunnel.Slope,
                    slopeEntranceZ,
                    mapDto.Dimensions.CorridorWidth,
                    upperLanding,
                    upperEntranceIndex,
                    tunnel.SlopeExitSign);
                tunnelRoots.Add(tunnel.Id, tunnelRoot);

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
            float passageHeight = _mapView.ResolvePassageHeight(mapDto.Dimensions.TunnelHeight);
            foreach (NormalCorridorDto corridor in mapDto.NormalCorridors)
            {
                Vector3 center = new Vector3(corridor.CenterX, corridor.CenterY, corridor.CenterZ);
                Vector3 dir = new Vector3(corridor.DirX, corridor.DirY, corridor.DirZ);

                var corridorInfo = _mapView.CreateNormalCorridor(
                    center,
                    dir,
                    corridor.Length,
                    corridor.ConnectionIndex,
                    mapDto.Dimensions.CorridorWidth,
                    passageHeight,
                    mapDto.Dimensions.CorridorLength,
                    corridor.OpenEnd,
                    corridor.OpenLeft,
                    corridor.OpenRight);
                // 曲がり通路は接続番号を共有するため、区間のリスト番号で識別する
                corridorRoots.Add(corridorRoots.Count, corridorInfo.transform);
            }

            // 3. 小部屋接続の生成
            foreach (SmallRoomDto room in mapDto.SmallRooms)
            {
                Vector3 center = new Vector3(room.CenterX, room.CenterY, room.CenterZ);
                Vector3 dir = new Vector3(room.DirX, room.DirY, room.DirZ);

                var roomInfo = _mapView.CreateSmallRoomConnection(
                    center,
                    dir,
                    room.RoomNumber,
                    room.Width,
                    room.RoomLength,
                    room.PassageLength,
                    mapDto.Dimensions.CorridorWidth,
                    passageHeight,
                    mapDto.Dimensions.CorridorLength);
                roomRoots.Add(room.RoomNumber, roomInfo.transform);
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
                    passageHeight,
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

            // 5. 上階の床と下階の天井を貫く落下口を生成する
            foreach (var opening in mapDto.DropOpenings)
            {
                _mapView.CreateDropOpening(tunnelRoots[opening.LowerTunnelId], tunnelRoots[opening.UpperTunnelId],
                    new Vector3(opening.CenterX, 0, opening.CenterZ), opening.Size,
                    opening.LowerFloorY, opening.UpperFloorY, mapDto.Dimensions.TunnelHeight);
            }

            // 6. 配置計画と共通のゴール位置を使う
            if (mapDto.GoalTunnelId.HasValue)
            {
                TunnelNodeDto goalTunnel = mapDto.Tunnels.Single(t => t.Id == mapDto.GoalTunnelId.Value);
                _mapView.CreateTunnelGoal(tunnelRoots[goalTunnel.Id], goalTunnel.Length,
                    mapDto.Dimensions.TunnelWidth, mapDto.Dimensions.TunnelHeight, mapDto.GoalEndSign);
            }

            // 7. 同フレームの床と壁を物理へ反映してからギミックを配置する
            Physics.SyncTransforms();
            foreach (var gimmick in mapDto.Gimmicks)
            {
                if (gimmick.Kind == TunnelGimmickKindDto.SpiderWeb)
                {
                    _mapView.CreateSpiderWeb(corridorRoots[gimmick.HostId], gimmick.LocalZ,
                        mapDto.Dimensions.CorridorWidth - 0.2f, passageHeight - 0.2f, gimmick.VisualSeed);
                }
                else if (gimmick.Kind == TunnelGimmickKindDto.FireAlarm)
                {
                    _mapView.CreateFireAlarm(tunnelRoots[gimmick.HostId], gimmick.LocalZ,
                        gimmick.Side, _mapView.FireAlarmHeight, mapDto.Dimensions.TunnelWidth);
                }
                else if (gimmick.Kind == TunnelGimmickKindDto.Car)
                {
                    _mapView.CreateCar(tunnelRoots[gimmick.HostId], gimmick.LocalX, gimmick.LocalZ,
                        gimmick.Yaw, gimmick.VisualSeed);
                }
                else if (gimmick.Kind == TunnelGimmickKindDto.HidingLocker)
                {
                    // 負のホストIDは小部屋番号を符号化したもの
                    bool room = gimmick.HostId < 0;
                    var host = room ? roomRoots[-gimmick.HostId - 1] : tunnelRoots[gimmick.HostId];
                    float width = room ? mapDto.SmallRooms.First(r => r.RoomNumber == -gimmick.HostId - 1).Width : mapDto.Dimensions.TunnelWidth;
                    _mapView.CreateHidingLocker(host, gimmick.LocalZ, gimmick.Side, width);
                }
            }

            // 家具の衝突を反映し、小部屋の空いた側壁へラックを配置する
            Physics.SyncTransforms();
            foreach (var room in mapDto.SmallRooms)
            {
                _mapView.CreateSmallRoomRacks(roomRoots[room.RoomNumber], room.Width, room.RoomLength,
                    mapDto.Dimensions.CorridorWidth, unchecked(mapDto.Seed ^ (room.RoomNumber * 397)));
            }

            // 大きいトンネル本体だけにごみ袋の群を配置する
            foreach (var tunnel in mapDto.Tunnels)
            {
                _mapView.CreateGarbageBagGroups(tunnelRoots[tunnel.Id],
                    _mapView.ResolveCarPlacementWidth(mapDto.Dimensions.TunnelWidth), tunnel.Length,
                    mapDto.Dimensions.CorridorWidth, unchecked(mapDto.Seed ^ (tunnel.Id * 397)));
            }

            // 接続口と中央の通行帯を空けて大小のトンネルへドラム缶を配置する
            foreach (var tunnel in mapDto.Tunnels)
            {
                var openings = new List<float>();
                if (tunnel.EntranceMarkers != null)
                    foreach (var marker in tunnel.EntranceMarkers)
                        if (marker.IsActive) openings.Add(marker.LocalPosZ);
                // 大型看板は大きいトンネル本体だけに配置する
                _mapView.CreateRoadSigns(tunnelRoots[tunnel.Id],
                    _mapView.ResolveCarPlacementWidth(mapDto.Dimensions.TunnelWidth), tunnel.Length,
                    mapDto.Dimensions.CorridorWidth, unchecked(mapDto.Seed ^ (tunnel.Id * 397)),
                    openings.ToArray(), mapDto.Dimensions.CorridorWidth);
                _mapView.CreateDrums(tunnelRoots[tunnel.Id],
                    _mapView.ResolveCarPlacementWidth(mapDto.Dimensions.TunnelWidth), tunnel.Length,
                    mapDto.Dimensions.CorridorWidth, unchecked(mapDto.Seed ^ (tunnel.Id * 397)),
                    openings.ToArray(), mapDto.Dimensions.CorridorWidth);
            }
            for (int i = 0; i < mapDto.NormalCorridors.Count; i++)
            {
                _mapView.CreateDrums(corridorRoots[i], mapDto.Dimensions.CorridorWidth,
                    mapDto.NormalCorridors[i].Length, 1.5f, unchecked(mapDto.Seed ^ (i * 613)),
                    null, mapDto.Dimensions.CorridorWidth);
            }
            foreach (var room in mapDto.SmallRooms)
            {
                foreach (string name in new[] { "Corridor Before Room", "Corridor After Room" })
                    _mapView.CreateDrums(roomRoots[room.RoomNumber].Find(name), mapDto.Dimensions.CorridorWidth,
                        room.PassageLength, 1.5f, unchecked(mapDto.Seed ^ (room.RoomNumber * 877)),
                        null, mapDto.Dimensions.CorridorWidth);
            }
            for (int i = 0; i < mapDto.WarpCorridors.Count; i++)
                _mapView.CreateDrums(createdWarpInfos[i].transform, mapDto.Dimensions.CorridorWidth,
                    mapDto.WarpCorridors[i].Length, 1.5f, unchecked(mapDto.Seed ^ (i * 991)),
                    null, mapDto.Dimensions.CorridorWidth);

            // 8. カメラフレーミング
            if (mapDto.Bounds != null)
            {
                Vector3 boundsCenter = new Vector3(mapDto.Bounds.CenterX, mapDto.Bounds.CenterY, mapDto.Bounds.CenterZ);
                Vector3 boundsSize = new Vector3(mapDto.Bounds.SizeX, mapDto.Bounds.SizeY, mapDto.Bounds.SizeZ);
                _mapView.FrameSceneCamera(boundsCenter, boundsSize);
            }

        }

        /// <summary>
        /// ペア先の通路内へ、床からの相対的な高さを保って移動する
        /// </summary>
        /// <param name="source">進入元の通路</param>
        /// <param name="player">移動するプレイヤー</param>
        private void OnWarpRequested(GeneratedCorridorInfo source, PlayerView player)
        {
            if (source == null || source.PairedCorridor == null || player == null ||
                player.gameObject.scene != _mapView.gameObject.scene || Time.time - _lastWarpTime < WarpCooldown)
                return;

            Transform target = source.PairedCorridor.transform;
            float height = source.transform.InverseTransformPoint(player.transform.position).y;
            Vector3 destination = target.TransformPoint(new Vector3(0, height, -1.25f));

            // Warpは移動先座標ではなく現在位置からの移動量を受け取る
            player.Warp(destination - player.transform.position, -target.forward);
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
