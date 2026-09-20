using System;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.Tunnel;

namespace Shinzui.Domain.DomainServices.Tunnel
{
    /// <summary>
    /// 設定情報とシード値に基づき、連結トンネル・通常通路・小部屋・ワープ通路の
    /// 幾何レイアウトを決定論的に計算するドメインサービス実装。
    /// 完全なPOCOであり、UnityEngine非依存。
    /// </summary>
    public sealed class TunnelLayoutGenerator : ITunnelLayoutGenerator
    {
        private static readonly TunnelEntrance[] Entrances =
        {
            new("Left +Z", new TunnelVector2(-0.5f, 1.0f / 3.0f), TunnelVector2.Left),
            new("Left Center", new TunnelVector2(-0.5f, 0.0f), TunnelVector2.Left),
            new("Left -Z", new TunnelVector2(-0.5f, -1.0f / 3.0f), TunnelVector2.Left),
            new("Right +Z", new TunnelVector2(0.5f, 1.0f / 3.0f), TunnelVector2.Right),
            new("Right Center", new TunnelVector2(0.5f, 0.0f), TunnelVector2.Right),
            new("Right -Z", new TunnelVector2(0.5f, -1.0f / 3.0f), TunnelVector2.Right)
        };

        private sealed class InternalTunnelNode
        {
            public int Id { get; }
            public TunnelVector3 Position { get; }
            public string Name { get; set; }
            public bool IsSpecial { get; set; }
            public int ConnectionCount { get; set; }
            public HashSet<InternalTunnelNode> Neighbors { get; } = new();
            public bool[] ActiveEntranceMarkers { get; } = new bool[Entrances.Length];
            public bool[] OpenEntrances { get; } = new bool[Entrances.Length];

            public InternalTunnelNode(int id, TunnelVector3 position, string name, bool isSpecial)
            {
                Id = id;
                Position = position;
                Name = name;
                IsSpecial = isSpecial;
                for (int i = 0; i < ActiveEntranceMarkers.Length; i++)
                {
                    ActiveEntranceMarkers[i] = true;
                }
            }
        }

        private readonly struct InternalOpenEntrance
        {
            public readonly InternalTunnelNode Tunnel;
            public readonly int EntranceIndex;

            public InternalOpenEntrance(InternalTunnelNode tunnel, int entranceIndex)
            {
                Tunnel = tunnel;
                EntranceIndex = entranceIndex;
            }
        }

        private readonly struct InternalNormalCorridor
        {
            public TunnelVector3 StartPort { get; }
            public TunnelVector3 EndPort { get; }
            public TunnelVector3 Center { get; }
            public TunnelVector3 Direction { get; }
            public float Length { get; }
            public int ConnectionIndex { get; }

            public InternalNormalCorridor(
                TunnelVector3 startPort,
                TunnelVector3 endPort,
                TunnelVector3 center,
                TunnelVector3 direction,
                float length,
                int connectionIndex)
            {
                StartPort = startPort;
                EndPort = endPort;
                Center = center;
                Direction = direction;
                Length = length;
                ConnectionIndex = connectionIndex;
            }
        }

        public TunnelLayoutResult GenerateLayout(TunnelGenerationConfig config)
        {
            config ??= new TunnelGenerationConfig();

            var random = new Random(config.Seed);
            var tunnels = new List<InternalTunnelNode>();
            var openEntrances = new List<InternalOpenEntrance>();
            var normalCorridors = new List<InternalNormalCorridor>();
            var smallRoomConnectionIndices = new HashSet<int>();
            var occupiedAreas = new List<TunnelOccupiedArea>();
            var warpCorridors = new List<WarpCorridorLayout>();

            // 原点は Player の開始地点。Special はこれとは別のトンネルとして生成される。
            AddTunnel(tunnels, openEntrances, occupiedAreas, TunnelVector3.Zero, false, "Player Start Tunnel 00", config);

            int requestedTunnelCount = Math.Max(5, config.TunnelCount);
            SelectSmallRoomConnections(smallRoomConnectionIndices, requestedTunnelCount - 1, config.SmallRoomCount, random);

            for (int i = 1; i < requestedTunnelCount; i++)
            {
                // 最後の1本は既存の端へ接続し、Special候補となる「通常接続2本」のトンネルが少なくとも一つ存在するようにする。
                InternalTunnelNode requiredParent = (i == requestedTunnelCount - 1)
                    ? GetRandomNonStartEndTunnel(tunnels, openEntrances, random)
                    : null;

                if (!TryAddConnectedTunnel(tunnels, openEntrances, normalCorridors, smallRoomConnectionIndices, occupiedAreas, i, requiredParent, config, random))
                {
                    break;
                }
            }

            InternalTunnelNode specialTunnel = AssignRandomSpecialTunnel(tunnels, openEntrances, random);

            // 小部屋レイアウトの構築
            var smallRooms = new List<SmallRoomLayout>();
            var filteredNormalCorridors = new List<NormalCorridorLayout>();
            int roomNumber = 0;

            foreach (var corridor in normalCorridors)
            {
                if (smallRoomConnectionIndices.Contains(corridor.ConnectionIndex))
                {
                    roomNumber++;
                    float width = Math.Max(config.CorridorWidth, config.SmallRoomWidth);
                    float roomLength = Math.Max(1.0f, config.SmallRoomLength);
                    float passageLength = config.CorridorLength;

                    smallRooms.Add(new SmallRoomLayout(
                        roomNumber,
                        corridor.ConnectionIndex,
                        corridor.Center,
                        corridor.Direction,
                        corridor.Length,
                        width,
                        roomLength,
                        passageLength));
                }
                else
                {
                    filteredNormalCorridors.Add(new NormalCorridorLayout(
                        corridor.ConnectionIndex,
                        corridor.StartPort,
                        corridor.EndPort,
                        corridor.Center,
                        corridor.Direction,
                        corridor.Length));
                }
            }

            // Special Tunnel ワープ通路の作成
            CreateSpecialWarpCorridors(tunnels, openEntrances, occupiedAreas, warpCorridors, specialTunnel, config, random);

            // 端トンネル同士のワープ通路の作成
            CreateWarpCorridorsBetweenEnds(tunnels, openEntrances, occupiedAreas, warpCorridors, config, random);

            // 最終ノードリストの変換
            var tunnelNodeLayouts = new List<TunnelNodeLayout>(tunnels.Count);
            foreach (var tunnel in tunnels)
            {
                var markerLayouts = new List<TunnelEntranceMarkerLayout>(Entrances.Length);
                for (int m = 0; m < Entrances.Length; m++)
                {
                    TunnelVector3 localPos = GetPortOffset(Entrances[m], config);
                    localPos = new TunnelVector3(localPos.X, 0.16f, localPos.Z);
                    markerLayouts.Add(new TunnelEntranceMarkerLayout(
                        m,
                        $"Entrance Candidate {m + 1}: {Entrances[m].Name}",
                        localPos,
                        tunnel.ActiveEntranceMarkers[m],
                        tunnel.OpenEntrances[m]));
                }

                tunnelNodeLayouts.Add(new TunnelNodeLayout(
                    tunnel.Id,
                    tunnel.Name,
                    tunnel.Position,
                    tunnel.IsSpecial,
                    markerLayouts,
                    (bool[])tunnel.OpenEntrances.Clone()));
            }

            // カメラフレーミング用バウンディングボックスの計算
            TunnelBoundsLayout bounds = CalculateBounds(tunnels, config);

            return new TunnelLayoutResult(
                tunnelNodeLayouts,
                filteredNormalCorridors,
                smallRooms,
                warpCorridors,
                specialTunnel?.Id,
                bounds,
                config.Seed);
        }

        private static InternalTunnelNode AddTunnel(
            List<InternalTunnelNode> tunnels,
            List<InternalOpenEntrance> openEntrances,
            List<TunnelOccupiedArea> occupiedAreas,
            TunnelVector3 position,
            bool isSpecial,
            string objectName,
            TunnelGenerationConfig config)
        {
            int id = tunnels.Count;
            var node = new InternalTunnelNode(id, position, objectName, isSpecial);
            tunnels.Add(node);
            occupiedAreas.Add(CreateTunnelArea(position, id, config));

            for (int i = 0; i < Entrances.Length; i++)
            {
                openEntrances.Add(new InternalOpenEntrance(node, i));
            }

            return node;
        }

        private static bool TryAddConnectedTunnel(
            List<InternalTunnelNode> tunnels,
            List<InternalOpenEntrance> openEntrances,
            List<InternalNormalCorridor> normalCorridors,
            HashSet<int> smallRoomConnectionIndices,
            List<TunnelOccupiedArea> occupiedAreas,
            int index,
            InternalTunnelNode requiredParent,
            TunnelGenerationConfig config,
            Random random)
        {
            for (int attempt = 0; attempt < config.PlacementAttemptsPerTunnel && openEntrances.Count > 0; attempt++)
            {
                int parentOpenIndex = (requiredParent != null)
                    ? GetRandomOpenEntranceIndex(openEntrances, requiredParent, random)
                    : random.Next(openEntrances.Count);

                if (parentOpenIndex < 0)
                {
                    return false;
                }

                InternalOpenEntrance parentOpen = openEntrances[parentOpenIndex];
                TunnelEntrance parentEntrance = Entrances[parentOpen.EntranceIndex];
                TunnelVector3 parentPort = GetPortPosition(parentOpen.Tunnel.Position, parentEntrance, config);
                TunnelVector3 outward = ToWorldDirection(parentEntrance.Direction);

                var candidates = GetOppositeEntrances(parentEntrance.Direction);
                int childEntranceIndex = candidates[random.Next(candidates.Count)];
                TunnelEntrance childEntrance = Entrances[childEntranceIndex];
                TunnelVector3 childPortOffset = GetPortOffset(childEntrance, config);

                bool isSmallRoom = smallRoomConnectionIndices.Contains(index);
                float connectionLength = isSmallRoom
                    ? config.CorridorLength * 2.0f + Math.Max(1.0f, config.SmallRoomLength)
                    : config.CorridorLength;

                TunnelVector3 childPosition = parentPort + outward * connectionLength - childPortOffset;
                TunnelVector3 childPort = GetPortPosition(childPosition, childEntrance, config);
                if (isSmallRoom && !IsPositiveDirectionFromStart(parentOpen.Tunnel.Position, childPosition))
                {
                    isSmallRoom = false;
                    connectionLength = config.CorridorLength;
                    childPosition = parentPort + outward * connectionLength - childPortOffset;
                    childPort = GetPortPosition(childPosition, childEntrance, config);
                }

                if (!CanPlaceTunnelAndConnection(parentOpen.Tunnel.Id, childPosition, parentPort, childPort, isSmallRoom, config, occupiedAreas))
                {
                    continue;
                }

                InternalTunnelNode child = AddTunnel(tunnels, openEntrances, occupiedAreas, childPosition, false, $"Tunnel {index:00}", config);

                TunnelVector3 delta = childPort - parentPort;
                TunnelVector3 center = (parentPort + childPort) * 0.5f;
                TunnelVector3 direction = delta.Normalized;

                normalCorridors.Add(new InternalNormalCorridor(parentPort, childPort, center, direction, delta.Magnitude, index));
                occupiedAreas.AddRange(BuildConnectionAreas(parentPort, childPort, isSmallRoom, config));
                if (!isSmallRoom)
                {
                    smallRoomConnectionIndices.Remove(index);
                }

                parentOpen.Tunnel.ConnectionCount++;
                child.ConnectionCount++;
                parentOpen.Tunnel.Neighbors.Add(child);
                child.Neighbors.Add(parentOpen.Tunnel);
                parentOpen.Tunnel.OpenEntrances[parentOpen.EntranceIndex] = true;
                child.OpenEntrances[childEntranceIndex] = true;

                RemoveOpenEntrance(openEntrances, parentOpenIndex);
                RemoveOpenEntrance(openEntrances, child, childEntranceIndex);
                return true;
            }

            return false;
        }

        private static bool IsPositiveDirectionFromStart(TunnelVector3 parentPosition, TunnelVector3 childPosition)
        {
            const float epsilon = 0.0001f;
            return HorizontalDistanceSqr(childPosition) > HorizontalDistanceSqr(parentPosition) + epsilon;
        }

        private static float HorizontalDistanceSqr(TunnelVector3 position)
        {
            return position.X * position.X + position.Z * position.Z;
        }

        private static void SelectSmallRoomConnections(
            HashSet<int> smallRoomConnectionIndices,
            int connectionCount,
            int configuredSmallRoomCount,
            Random random)
        {
            smallRoomConnectionIndices.Clear();
            int count = Math.Min(Math.Max(0, configuredSmallRoomCount), connectionCount);
            var indices = new List<int>(connectionCount);
            for (int i = 1; i <= connectionCount; i++)
            {
                indices.Add(i);
            }

            for (int i = indices.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (indices[i], indices[swapIndex]) = (indices[swapIndex], indices[i]);
            }

            for (int i = 0; i < count; i++)
            {
                smallRoomConnectionIndices.Add(indices[i]);
            }
        }

        private static InternalTunnelNode GetRandomNonStartEndTunnel(
            List<InternalTunnelNode> tunnels,
            List<InternalOpenEntrance> openEntrances,
            Random random)
        {
            var candidates = new List<InternalTunnelNode>();
            for (int i = 1; i < tunnels.Count; i++)
            {
                if (tunnels[i].ConnectionCount == 1 && HasOpenEntrance(openEntrances, tunnels[i]))
                {
                    candidates.Add(tunnels[i]);
                }
            }
            return candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
        }

        private static InternalTunnelNode AssignRandomSpecialTunnel(
            List<InternalTunnelNode> tunnels,
            List<InternalOpenEntrance> openEntrances,
            Random random)
        {
            var candidates = new List<InternalTunnelNode>();
            for (int i = 1; i < tunnels.Count; i++)
            {
                InternalTunnelNode tunnel = tunnels[i];
                if (tunnel.ConnectionCount == 2 && CountWarpDestinationCandidates(tunnels, openEntrances, tunnel) >= 2)
                {
                    candidates.Add(tunnel);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            InternalTunnelNode specialTunnel = candidates[random.Next(candidates.Count)];
            specialTunnel.IsSpecial = true;
            specialTunnel.Name = $"Special Tunnel {specialTunnel.Id:00}";
            return specialTunnel;
        }

        private static int CountWarpDestinationCandidates(
            List<InternalTunnelNode> tunnels,
            List<InternalOpenEntrance> openEntrances,
            InternalTunnelNode specialTunnel)
        {
            int count = 0;
            foreach (InternalTunnelNode tunnel in tunnels)
            {
                if (tunnel != specialTunnel && !specialTunnel.Neighbors.Contains(tunnel) && HasOpenEntrance(openEntrances, tunnel))
                {
                    count++;
                }
            }
            return count;
        }

        private static void CreateSpecialWarpCorridors(
            List<InternalTunnelNode> tunnels,
            List<InternalOpenEntrance> openEntrances,
            List<TunnelOccupiedArea> occupiedAreas,
            List<WarpCorridorLayout> warpCorridors,
            InternalTunnelNode specialTunnel,
            TunnelGenerationConfig config,
            Random random)
        {
            if (specialTunnel == null)
            {
                return;
            }

            var destinations = new List<InternalTunnelNode>();
            foreach (InternalTunnelNode tunnel in tunnels)
            {
                if (tunnel != specialTunnel && !specialTunnel.Neighbors.Contains(tunnel) && HasOpenEntrance(openEntrances, tunnel))
                {
                    destinations.Add(tunnel);
                }
            }

            for (int i = destinations.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (destinations[i], destinations[swapIndex]) = (destinations[swapIndex], destinations[i]);
            }

            List<int> specialEntrances = GetOpenEntranceIndices(openEntrances, specialTunnel);
            for (int i = specialEntrances.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (specialEntrances[i], specialEntrances[swapIndex]) = (specialEntrances[swapIndex], specialEntrances[i]);
            }

            var warpEntrances = new List<int> { specialEntrances[0], specialEntrances[1] };
            DisableUnusedSpecialEntrances(openEntrances, specialTunnel, warpEntrances);

            for (int pairId = 0; pairId < 2; pairId++)
            {
                int specialIndex = warpCorridors.Count;
                var specialSide = CreateWarpCorridorStub(openEntrances, occupiedAreas, specialTunnel, pairId, "Special", warpEntrances[pairId], config, random);
                if (specialSide == null) continue;
                warpCorridors.Add(specialSide);

                int destinationIndex = warpCorridors.Count;
                var destinationSide = CreateWarpCorridorStub(openEntrances, occupiedAreas, destinations[pairId], pairId, "Destination", -1, config, random);
                if (destinationSide != null)
                {
                    warpCorridors.Add(destinationSide);
                    specialSide.PairedIndex = destinationIndex;
                    destinationSide.PairedIndex = specialIndex;
                }
            }
        }

        private static void CreateWarpCorridorsBetweenEnds(
            List<InternalTunnelNode> tunnels,
            List<InternalOpenEntrance> openEntrances,
            List<TunnelOccupiedArea> occupiedAreas,
            List<WarpCorridorLayout> warpCorridors,
            TunnelGenerationConfig config,
            Random random)
        {
            var endTunnels = new List<InternalTunnelNode>();
            foreach (InternalTunnelNode tunnel in tunnels)
            {
                if (tunnel.ConnectionCount == 1 && HasOpenEntrance(openEntrances, tunnel))
                {
                    endTunnels.Add(tunnel);
                }
            }

            for (int i = endTunnels.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (endTunnels[i], endTunnels[swapIndex]) = (endTunnels[swapIndex], endTunnels[i]);
            }

            int pairId = 100;
            for (int i = 0; i + 1 < endTunnels.Count; i += 2)
            {
                PairWarpCorridors(openEntrances, occupiedAreas, warpCorridors, endTunnels[i], endTunnels[i + 1], pairId++, config, random);
            }

            if (endTunnels.Count >= 3 && endTunnels.Count % 2 != 0)
            {
                PairWarpCorridors(openEntrances, occupiedAreas, warpCorridors, endTunnels[^1], endTunnels[0], pairId, config, random);
            }
        }

        private static void PairWarpCorridors(
            List<InternalOpenEntrance> openEntrances,
            List<TunnelOccupiedArea> occupiedAreas,
            List<WarpCorridorLayout> warpCorridors,
            InternalTunnelNode firstTunnel,
            InternalTunnelNode secondTunnel,
            int pairId,
            TunnelGenerationConfig config,
            Random random)
        {
            int firstIndex = warpCorridors.Count;
            var first = CreateWarpCorridorStub(openEntrances, occupiedAreas, firstTunnel, pairId, "End A", -1, config, random);
            if (first == null) return;
            warpCorridors.Add(first);

            int secondIndex = warpCorridors.Count;
            var second = CreateWarpCorridorStub(openEntrances, occupiedAreas, secondTunnel, pairId, "End B", -1, config, random);
            if (second != null)
            {
                warpCorridors.Add(second);
                first.PairedIndex = secondIndex;
                second.PairedIndex = firstIndex;
            }
        }

        private static WarpCorridorLayout CreateWarpCorridorStub(
            List<InternalOpenEntrance> openEntrances,
            List<TunnelOccupiedArea> occupiedAreas,
            InternalTunnelNode tunnel,
            int pairId,
            string sideName,
            int requiredEntranceIndex,
            TunnelGenerationConfig config,
            Random random)
        {
            int openIndex = FindAvailableWarpEntranceIndex(openEntrances, occupiedAreas, tunnel, requiredEntranceIndex, config, random);
            if (openIndex < 0)
            {
                return null;
            }

            InternalOpenEntrance open = openEntrances[openIndex];
            TunnelEntrance entrance = Entrances[open.EntranceIndex];
            TunnelVector3 start = GetPortPosition(tunnel.Position, entrance, config);
            TunnelVector3 outward = ToWorldDirection(entrance.Direction);
            TunnelVector3 end = start + outward * config.CorridorLength;
            TunnelVector3 delta = end - start;
            TunnelVector3 center = (start + end) * 0.5f;

            var layout = new WarpCorridorLayout(pairId, sideName, tunnel.Id, start, end, center, delta.Normalized, delta.Magnitude);
            occupiedAreas.AddRange(BuildConnectionAreas(start, end, false, config));
            tunnel.OpenEntrances[open.EntranceIndex] = true;
            RemoveOpenEntrance(openEntrances, openIndex);
            return layout;
        }

        private static int FindAvailableWarpEntranceIndex(
            List<InternalOpenEntrance> openEntrances,
            List<TunnelOccupiedArea> occupiedAreas,
            InternalTunnelNode tunnel,
            int requiredEntranceIndex,
            TunnelGenerationConfig config,
            Random random)
        {
            var candidates = new List<int>();
            if (requiredEntranceIndex >= 0)
            {
                int requiredOpenIndex = FindOpenEntranceIndex(openEntrances, tunnel, requiredEntranceIndex);
                if (requiredOpenIndex >= 0)
                {
                    candidates.Add(requiredOpenIndex);
                }
            }
            else
            {
                for (int i = 0; i < openEntrances.Count; i++)
                {
                    if (openEntrances[i].Tunnel == tunnel)
                    {
                        candidates.Add(i);
                    }
                }
                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int swapIndex = random.Next(i + 1);
                    (candidates[i], candidates[swapIndex]) = (candidates[swapIndex], candidates[i]);
                }
            }

            foreach (int candidateIndex in candidates)
            {
                InternalOpenEntrance open = openEntrances[candidateIndex];
                TunnelEntrance entrance = Entrances[open.EntranceIndex];
                TunnelVector3 start = GetPortPosition(tunnel.Position, entrance, config);
                TunnelVector3 end = start + ToWorldDirection(entrance.Direction) * config.CorridorLength;
                bool overlaps = false;

                foreach (TunnelOccupiedArea area in BuildConnectionAreas(start, end, false, config))
                {
                    if (OverlapsAny(area, tunnel.Id, occupiedAreas))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    return candidateIndex;
                }
            }

            return -1;
        }

        private static List<int> GetOpenEntranceIndices(List<InternalOpenEntrance> openEntrances, InternalTunnelNode tunnel)
        {
            var result = new List<int>();
            foreach (var open in openEntrances)
            {
                if (open.Tunnel == tunnel)
                {
                    result.Add(open.EntranceIndex);
                }
            }
            return result;
        }

        private static int FindOpenEntranceIndex(List<InternalOpenEntrance> openEntrances, InternalTunnelNode tunnel, int entranceIndex)
        {
            for (int i = 0; i < openEntrances.Count; i++)
            {
                if (openEntrances[i].Tunnel == tunnel && openEntrances[i].EntranceIndex == entranceIndex)
                {
                    return i;
                }
            }
            return -1;
        }

        private static void DisableUnusedSpecialEntrances(
            List<InternalOpenEntrance> openEntrances,
            InternalTunnelNode specialTunnel,
            List<int> warpEntranceIndices)
        {
            for (int i = openEntrances.Count - 1; i >= 0; i--)
            {
                InternalOpenEntrance open = openEntrances[i];
                if (open.Tunnel != specialTunnel || warpEntranceIndices.Contains(open.EntranceIndex))
                {
                    continue;
                }

                specialTunnel.ActiveEntranceMarkers[open.EntranceIndex] = false;
                openEntrances.RemoveAt(i);
            }
        }

        private static bool HasOpenEntrance(List<InternalOpenEntrance> openEntrances, InternalTunnelNode tunnel)
        {
            foreach (var open in openEntrances)
            {
                if (open.Tunnel == tunnel)
                {
                    return true;
                }
            }
            return false;
        }

        private static int GetRandomOpenEntranceIndex(List<InternalOpenEntrance> openEntrances, InternalTunnelNode tunnel, Random random)
        {
            var candidates = new List<int>();
            for (int i = 0; i < openEntrances.Count; i++)
            {
                if (openEntrances[i].Tunnel == tunnel)
                {
                    candidates.Add(i);
                }
            }
            return candidates.Count == 0 ? -1 : candidates[random.Next(candidates.Count)];
        }

        private static bool CanPlaceTunnelAndConnection(
            int? parentTunnelId,
            TunnelVector3 childPosition,
            TunnelVector3 start,
            TunnelVector3 end,
            bool hasSmallRoom,
            TunnelGenerationConfig config,
            List<TunnelOccupiedArea> occupiedAreas)
        {
            if (OverlapsAny(CreateTunnelArea(childPosition, null, config), null, occupiedAreas))
            {
                return false;
            }

            foreach (TunnelOccupiedArea area in BuildConnectionAreas(start, end, hasSmallRoom, config))
            {
                if (OverlapsAny(area, parentTunnelId, occupiedAreas))
                {
                    return false;
                }
            }

            return true;
        }

        private static TunnelOccupiedArea CreateTunnelArea(TunnelVector3 position, int? ownerTunnelId, TunnelGenerationConfig config)
        {
            TunnelVector2 size = new TunnelVector2(config.TunnelWidth, config.TunnelLength)
                                 * Math.Max(0.1f, config.TunnelOverlapSizeMultiplier)
                                 + TunnelVector2.One * config.PlacementMargin;
            return new TunnelOccupiedArea(new TunnelVector2(position.X, position.Z), size, ownerTunnelId);
        }

        private static List<TunnelOccupiedArea> BuildConnectionAreas(
            TunnelVector3 start,
            TunnelVector3 end,
            bool hasSmallRoom,
            TunnelGenerationConfig config)
        {
            var result = new List<TunnelOccupiedArea>(hasSmallRoom ? 3 : 1);
            TunnelVector3 delta = end - start;
            float totalLength = delta.Magnitude;
            if (totalLength <= 1e-6f)
            {
                return result;
            }

            TunnelVector3 direction = delta / totalLength;
            if (!hasSmallRoom)
            {
                result.Add(CreateSegmentArea((start + end) * 0.5f, direction, totalLength,
                    config.CorridorWidth, config.CorridorOverlapSizeMultiplier, config.PlacementMargin));
                return result;
            }

            float roomLength = Math.Max(1.0f, config.SmallRoomLength);
            float passageLength = config.CorridorLength;

            result.Add(CreateSegmentArea(start + direction * (passageLength * 0.5f), direction,
                passageLength, config.CorridorWidth, config.CorridorOverlapSizeMultiplier, config.PlacementMargin));
            result.Add(CreateSegmentArea(start + direction * (passageLength + roomLength * 0.5f), direction,
                roomLength, Math.Max(config.CorridorWidth, config.SmallRoomWidth), config.SmallRoomOverlapSizeMultiplier, config.PlacementMargin));
            result.Add(CreateSegmentArea(start + direction * (passageLength + roomLength + passageLength * 0.5f),
                direction, passageLength, config.CorridorWidth, config.CorridorOverlapSizeMultiplier, config.PlacementMargin));
            return result;
        }

        private static TunnelOccupiedArea CreateSegmentArea(
            TunnelVector3 center,
            TunnelVector3 direction,
            float length,
            float width,
            float sizeMultiplier,
            float placementMargin)
        {
            float sizeX = Math.Abs(direction.X) * length + Math.Abs(direction.Z) * width;
            float sizeY = Math.Abs(direction.Z) * length + Math.Abs(direction.X) * width;
            TunnelVector2 size = new TunnelVector2(sizeX, sizeY) * Math.Max(0.1f, sizeMultiplier) + TunnelVector2.One * placementMargin;
            return new TunnelOccupiedArea(new TunnelVector2(center.X, center.Z), size, null);
        }

        private static bool OverlapsAny(TunnelOccupiedArea candidate, int? ignoredTunnelId, List<TunnelOccupiedArea> occupiedAreas)
        {
            foreach (TunnelOccupiedArea occupied in occupiedAreas)
            {
                if (ignoredTunnelId.HasValue && occupied.OwnerTunnelId == ignoredTunnelId.Value)
                {
                    continue;
                }
                if (candidate.Overlaps(occupied))
                {
                    return true;
                }
            }
            return false;
        }

        private static List<int> GetOppositeEntrances(TunnelVector2 direction)
        {
            var result = new List<int>(3);
            for (int i = 0; i < Entrances.Length; i++)
            {
                if (TunnelVector2.Dot(Entrances[i].Direction, direction) < -0.99f)
                {
                    result.Add(i);
                }
            }
            return result;
        }

        private static TunnelVector3 GetPortPosition(TunnelVector3 tunnelPosition, TunnelEntrance entrance, TunnelGenerationConfig config)
        {
            return tunnelPosition + GetPortOffset(entrance, config);
        }

        private static TunnelVector3 GetPortOffset(TunnelEntrance entrance, TunnelGenerationConfig config)
        {
            float longitudinalOffset = entrance.NormalizedPosition.Y == 0.0f
                ? 0.0f
                : Math.Sign(entrance.NormalizedPosition.Y) * config.ConnectionPointSpacing;

            return new TunnelVector3(
                entrance.NormalizedPosition.X * config.TunnelWidth,
                0.0f,
                longitudinalOffset);
        }

        private static TunnelVector3 ToWorldDirection(TunnelVector2 direction)
        {
            return new TunnelVector3(direction.X, 0.0f, direction.Y);
        }

        private static void RemoveOpenEntrance(List<InternalOpenEntrance> openEntrances, int index)
        {
            openEntrances.RemoveAt(index);
        }

        private static void RemoveOpenEntrance(List<InternalOpenEntrance> openEntrances, InternalTunnelNode tunnel, int entranceIndex)
        {
            for (int i = openEntrances.Count - 1; i >= 0; i--)
            {
                InternalOpenEntrance open = openEntrances[i];
                if (open.Tunnel == tunnel && open.EntranceIndex == entranceIndex)
                {
                    openEntrances.RemoveAt(i);
                    return;
                }
            }
        }

        private static TunnelBoundsLayout CalculateBounds(List<InternalTunnelNode> tunnels, TunnelGenerationConfig config)
        {
            if (tunnels.Count == 0)
            {
                return new TunnelBoundsLayout(TunnelVector3.Zero, TunnelVector3.Zero);
            }

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            float halfW = config.TunnelWidth * 0.5f;
            float halfH = config.TunnelHeight * 0.5f;
            float halfL = config.TunnelLength * 0.5f;

            foreach (var tunnel in tunnels)
            {
                float px = tunnel.Position.X;
                float py = tunnel.Position.Y;
                float pz = tunnel.Position.Z;

                minX = Math.Min(minX, px - halfW);
                maxX = Math.Max(maxX, px + halfW);
                minY = Math.Min(minY, py);
                maxY = Math.Max(maxY, py + config.TunnelHeight);
                minZ = Math.Min(minZ, pz - halfL);
                maxZ = Math.Max(maxZ, pz + halfL);
            }

            TunnelVector3 center = new TunnelVector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
            TunnelVector3 size = new TunnelVector3(maxX - minX, maxY - minY, maxZ - minZ);
            return new TunnelBoundsLayout(center, size);
        }
    }
}
