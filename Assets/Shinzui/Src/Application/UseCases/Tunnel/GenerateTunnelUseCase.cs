using System;
using System.Collections.Generic;
using System.Linq;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Application.Interfaces.Tunnel;
using Shinzui.Domain.DomainServices.Tunnel;
using Shinzui.Domain.ValueObjects.Tunnel;

namespace Shinzui.Application.UseCases.Tunnel
{
    /// <summary>
    /// トンネルランダム生成ユースケースの実装。
    /// ドメイン層のレイアウト生成サービスを呼び出し、結果をDTOに変換して返却する。
    /// 対象シーン開始時に一度限りの生成を保証し、二重生成を防ぐ。
    /// </summary>
    public sealed class GenerateTunnelUseCase : IGenerateTunnelUseCase
    {
        private readonly ITunnelLayoutGenerator _layoutGenerator;
        private readonly ITunnelGimmickPlanner _gimmickPlanner;
        private readonly object _lock = new();
        private bool _hasGenerated;
        private TunnelMapDto _cachedMapDto;

        public bool HasGenerated
        {
            get
            {
                lock (_lock)
                {
                    return _hasGenerated;
                }
            }
        }

        /// <summary>生成と配置計画のサービスを保持する</summary>
        /// <param name="layoutGenerator">レイアウト生成サービス</param>
        /// <param name="gimmickPlanner">ギミック配置サービス</param>
        public GenerateTunnelUseCase(ITunnelLayoutGenerator layoutGenerator, ITunnelGimmickPlanner gimmickPlanner = null)
        {
            _layoutGenerator = layoutGenerator ?? new TunnelLayoutGenerator();
            _gimmickPlanner = gimmickPlanner ?? new TunnelGimmickPlanner();
        }

        public bool GenerateOnce(TunnelGenerationRequestDto request, out TunnelMapDto mapDto)
        {
            return Generate(request, false, out mapDto);
        }

        public bool Regenerate(TunnelGenerationRequestDto request, out TunnelMapDto mapDto)
        {
            return Generate(request, true, out mapDto);
        }

        private bool Generate(TunnelGenerationRequestDto request, bool regenerate, out TunnelMapDto mapDto)
        {
            lock (_lock)
            {
                if (_hasGenerated && !regenerate)
                {
                    mapDto = _cachedMapDto;
                    return false;
                }

                request ??= new TunnelGenerationRequestDto();
                TunnelGenerationConfig config = request.ToDomainConfig();
                TunnelLayoutResult layoutResult = _layoutGenerator.GenerateLayout(config);

                mapDto = ConvertToDto(layoutResult, config);
                _cachedMapDto = mapDto;
                _hasGenerated = true;
                return true;
            }
        }

        /// <summary>
        /// 全階層の配置と落下口を描画用のDTOへ変換する
        /// </summary>
        /// <param name="layoutResult">ドメイン層の生成結果</param>
        /// <param name="config">寸法設定</param>
        /// <returns>描画と音響で共用する配置情報</returns>
        private TunnelMapDto ConvertToDto(TunnelLayoutResult layoutResult, TunnelGenerationConfig config)
        {
            var tunnelDtos = new List<TunnelNodeDto>(layoutResult.Tunnels.Count);
            foreach (var tunnel in layoutResult.Tunnels)
            {
                var markerDtos = new List<TunnelEntranceMarkerDto>(tunnel.EntranceMarkers.Count);
                foreach (var marker in tunnel.EntranceMarkers)
                {
                    markerDtos.Add(new TunnelEntranceMarkerDto
                    {
                        Index = marker.Index,
                        Name = marker.Name,
                        LocalPosX = marker.LocalPosition.X,
                        LocalPosY = marker.LocalPosition.Y,
                        LocalPosZ = marker.LocalPosition.Z,
                        IsActive = marker.IsActive,
                        IsOpen = marker.IsOpen
                    });
                }

                tunnelDtos.Add(new TunnelNodeDto
                {
                    Id = tunnel.Id,
                    Name = tunnel.Name,
                    PositionX = tunnel.Position.X,
                    PositionY = tunnel.Position.Y,
                    PositionZ = tunnel.Position.Z,
                    DirX = tunnel.Forward.X,
                    DirZ = tunnel.Forward.Y,
                    Length = tunnel.Length,
                    FloorIndex = tunnel.FloorIndex,
                    Slope = tunnel.Slope,
                    SlopeExitSign = tunnel.SlopeExitSign,
                    IsSpecial = tunnel.IsSpecial,
                    EntranceMarkers = markerDtos,
                    OpenEntrances = tunnel.OpenEntrances
                });
            }

            var corridorDtos = new List<NormalCorridorDto>(layoutResult.NormalCorridors.Count);
            foreach (var corridor in layoutResult.NormalCorridors)
            {
                corridorDtos.Add(new NormalCorridorDto
                {
                    ConnectionIndex = corridor.ConnectionIndex,
                    StartX = corridor.StartPort.X,
                    StartY = corridor.StartPort.Y,
                    StartZ = corridor.StartPort.Z,
                    EndX = corridor.EndPort.X,
                    EndY = corridor.EndPort.Y,
                    EndZ = corridor.EndPort.Z,
                    CenterX = corridor.Center.X,
                    CenterY = corridor.Center.Y,
                    CenterZ = corridor.Center.Z,
                    DirX = corridor.Direction.X,
                    DirY = corridor.Direction.Y,
                    DirZ = corridor.Direction.Z,
                    Length = corridor.Length,
                    OpenEnd = corridor.OpenEnd,
                    OpenLeft = corridor.OpenLeft,
                    OpenRight = corridor.OpenRight
                });
            }

            var smallRoomDtos = new List<SmallRoomDto>(layoutResult.SmallRooms.Count);
            foreach (var room in layoutResult.SmallRooms)
            {
                smallRoomDtos.Add(new SmallRoomDto
                {
                    RoomNumber = room.RoomNumber,
                    ConnectionIndex = room.ConnectionIndex,
                    CenterX = room.Center.X,
                    CenterY = room.Center.Y,
                    CenterZ = room.Center.Z,
                    DirX = room.Direction.X,
                    DirY = room.Direction.Y,
                    DirZ = room.Direction.Z,
                    TotalLength = room.TotalLength,
                    Width = room.Width,
                    RoomLength = room.RoomLength,
                    PassageLength = room.PassageLength
                });
            }

            var warpDtos = new List<WarpCorridorDto>(layoutResult.WarpCorridors.Count);
            foreach (var warp in layoutResult.WarpCorridors)
            {
                warpDtos.Add(new WarpCorridorDto
                {
                    PairId = warp.PairId,
                    SideName = warp.SideName,
                    TunnelId = warp.TunnelId,
                    StartX = warp.StartPort.X,
                    StartY = warp.StartPort.Y,
                    StartZ = warp.StartPort.Z,
                    EndX = warp.EndPort.X,
                    EndY = warp.EndPort.Y,
                    EndZ = warp.EndPort.Z,
                    CenterX = warp.Center.X,
                    CenterY = warp.Center.Y,
                    CenterZ = warp.Center.Z,
                    DirX = warp.Direction.X,
                    DirY = warp.Direction.Y,
                    DirZ = warp.Direction.Z,
                    Length = warp.Length,
                    PairedIndex = warp.PairedIndex
                });
            }

            var boundsDto = new TunnelBoundsDto
            {
                CenterX = layoutResult.Bounds.Center.X,
                CenterY = layoutResult.Bounds.Center.Y,
                CenterZ = layoutResult.Bounds.Center.Z,
                SizeX = layoutResult.Bounds.Size.X,
                SizeY = layoutResult.Bounds.Size.Y,
                SizeZ = layoutResult.Bounds.Size.Z
            };

            var dimensionsDto = new TunnelDimensionsDto
            {
                TunnelLength = config.TunnelLength,
                TunnelWidth = config.TunnelWidth,
                TunnelHeight = config.TunnelHeight,
                CorridorLength = config.CorridorLength,
                CorridorWidth = config.CorridorWidth,
                ShellThickness = 0.2f
            };

            // 既存の候補順と乱数呼び出し順を保ち、配置計画とゴール位置を共有する
            var goalCandidates = layoutResult.Tunnels.Where(t => t.Slope == 0 && !t.IsSpecial && t.Id != 0).ToArray();
            int? goalTunnelId = null;
            int goalEndSign = 0;
            if (goalCandidates.Length > 0)
            {
                var random = new System.Random(unchecked(layoutResult.Seed ^ 0x47504c));
                goalTunnelId = goalCandidates[random.Next(goalCandidates.Length)].Id;
                goalEndSign = random.Next(2) == 0 ? -1 : 1;
            }
            var gimmicks = _gimmickPlanner.Plan(layoutResult, config, goalTunnelId, goalEndSign);

            return new TunnelMapDto
            {
                GoalTunnelId = goalTunnelId,
                GoalEndSign = goalEndSign,
                Gimmicks = gimmicks.Select(g => new TunnelGimmickDto
                {
                    Kind = (TunnelGimmickKindDto)g.Kind, HostId = g.HostId,
                    LocalZ = g.LocalZ, Side = g.Side, VisualSeed = g.VisualSeed, LocalX = g.LocalX, Yaw = g.Yaw
                }).ToArray(),
                Tunnels = tunnelDtos,
                NormalCorridors = corridorDtos,
                SmallRooms = smallRoomDtos,
                WarpCorridors = warpDtos,
                SpecialTunnelId = layoutResult.SpecialTunnelId,
                Bounds = boundsDto,
                Dimensions = dimensionsDto,
                Seed = layoutResult.Seed,
                DropOpenings = layoutResult.DropOpenings.Select(
                    opening => new TunnelDropOpeningDto
                    {
                        LowerTunnelId = opening.LowerTunnelId,
                        UpperTunnelId = opening.UpperTunnelId,
                        CenterX = opening.Center.X,
                        CenterZ = opening.Center.Z,
                        Size = opening.Size,
                        LowerFloorY = opening.LowerFloorY,
                        UpperFloorY = opening.UpperFloorY
                    }).ToArray()
            };
        }
    }
}
