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
            public TunnelVector2 Forward { get; }
            public float Length { get; }
            public string Name { get; set; }
            public bool IsSpecial { get; set; }
            public int ConnectionCount { get; set; }
            public HashSet<InternalTunnelNode> Neighbors { get; } = new();
            public bool[] ActiveEntranceMarkers { get; } = new bool[Entrances.Length];
            public bool[] OpenEntrances { get; } = new bool[Entrances.Length];

            public InternalTunnelNode(int id, TunnelVector3 position, TunnelVector2 forward, float length, string name, bool isSpecial)
            {
                Id = id;
                Position = position;
                Forward = forward;
                Length = length;
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
            public bool HasSmallRoom { get; }
            public bool OpenEnd { get; }
            public bool OpenLeft { get; }
            public bool OpenRight { get; }

            /// <summary>
            /// 通路区間の寸法と接続端と開口部を保持する
            /// </summary>
            /// <param name="startPort">入口の位置</param>
            /// <param name="endPort">出口の位置</param>
            /// <param name="center">区間の中心</param>
            /// <param name="direction">区間の前方向</param>
            /// <param name="length">区間の長さ</param>
            /// <param name="connectionIndex">接続の識別番号</param>
            /// <param name="hasSmallRoom">小部屋を配置するか</param>
            /// <param name="openEnd">前方を開けるか</param>
            /// <param name="openLeft">左側を開けるか</param>
            /// <param name="openRight">右側を開けるか</param>
            public InternalNormalCorridor(
                TunnelVector3 startPort,
                TunnelVector3 endPort,
                TunnelVector3 center,
                TunnelVector3 direction,
                float length,
                int connectionIndex,
                bool hasSmallRoom = false,
                bool openEnd = true,
                bool openLeft = false,
                bool openRight = false)
            {
                StartPort = startPort;
                EndPort = endPort;
                Center = center;
                Direction = direction;
                Length = length;
                ConnectionIndex = connectionIndex;
                HasSmallRoom = hasSmallRoom;
                OpenEnd = openEnd;
                OpenLeft = openLeft;
                OpenRight = openRight;
            }
        }

        /// <summary>
        /// 設定とシード値からトンネルと通路のレイアウトを生成する
        /// </summary>
        /// <param name="config">生成設定</param>
        /// <returns>トンネルと通路と小部屋の生成結果</returns>
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
            AddTunnel(tunnels, openEntrances, occupiedAreas, TunnelVector3.Zero, TunnelVector2.Zero, ChooseTunnelLength(config, random), false, "Player Start Tunnel 00", config);

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
                if (corridor.HasSmallRoom)
                {
                    roomNumber++;
                    float width = Math.Max(config.CorridorWidth, config.SmallRoomWidth);
                    float roomLength = Math.Max(1.0f, config.SmallRoomLength);
                    float passageLength = (corridor.Length - roomLength) * 0.5f;

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
                        corridor.Length,
                        corridor.OpenEnd,
                        corridor.OpenLeft,
                        corridor.OpenRight));
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
                    TunnelVector3 localPos = GetPortOffset(Entrances[m], tunnel.Length, config);
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
                    tunnel.Forward,
                    tunnel.Length,
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
            TunnelVector2 forward,
            float length,
            bool isSpecial,
            string objectName,
            TunnelGenerationConfig config)
        {
            int id = tunnels.Count;
            if (forward.Equals(TunnelVector2.Zero))
            {
                forward = TunnelVector2.Right;
            }

            var node = new InternalTunnelNode(id, position, forward, length, objectName, isSpecial);
            tunnels.Add(node);
            occupiedAreas.Add(CreateTunnelArea(node, id, config));

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
                TunnelVector3 parentPort = GetPortPosition(parentOpen.Tunnel, parentEntrance, config);
                TunnelVector3 outward = ToWorldDirection(parentEntrance.Direction, parentOpen.Tunnel.Forward);

                TunnelVector2 childForward = ChooseTunnelForward(random);
                float childLength = ChooseTunnelLength(config, random);
                var candidates = GetOppositeEntrances(outward, childForward);

                // 直交するトンネルは側面の接続口までL字に曲げて接続する
                bool needsBend = candidates.Count == 0;
                if (needsBend)
                {
                    for (int entranceIndex = 0; entranceIndex < Entrances.Length; entranceIndex++)
                    {
                        candidates.Add(entranceIndex);
                    }
                }

                int childEntranceIndex = candidates[random.Next(candidates.Count)];
                TunnelEntrance childEntrance = Entrances[childEntranceIndex];
                TunnelVector3 childPortOffset = ToWorldOffset(GetPortOffset(childEntrance, childLength, config), childForward);

                bool isSmallRoom = smallRoomConnectionIndices.Contains(index);
                float connectionLength = config.CorridorLength * (1.0f + (float)random.NextDouble() * 2.0f);
                float minimumRoomLength = config.CorridorLength * 2.0f + Math.Max(1.0f, config.SmallRoomLength);
                if (isSmallRoom) connectionLength = Math.Max(connectionLength, minimumRoomLength);

                // 親と子の長い側面を避けた位置に曲がり角を設ける
                TunnelVector3 childInward = ToWorldDirection(childEntrance.Direction, childForward) * -1.0f;
                float halfWidth = config.CorridorWidth * 0.5f;
                float firstLength = needsBend ? connectionLength + halfWidth : connectionLength;
                float parentPortOffset = DotHorizontal(parentPort - parentOpen.Tunnel.Position, childInward);
                float secondLength = needsBend
                    ? Math.Max(config.CorridorLength + halfWidth,
                        parentOpen.Tunnel.Length * 0.5f - parentPortOffset + config.PlacementMargin + config.CorridorLength)
                    : 0.0f;
                TunnelVector3 bend = parentPort + outward * firstLength;
                TunnelVector3 childPort = bend + childInward * secondLength;
                TunnelVector3 childPosition = childPort - childPortOffset;

                // 直線区間と曲がり角を、互いに重ならない矩形として扱う
                var route = new List<InternalNormalCorridor>(needsBend ? 3 : 1);
                TunnelVector3 firstEnd = needsBend ? bend - outward * halfWidth : childPort;
                route.Add(CreateStraightCorridor(parentPort, firstEnd, index, isSmallRoom));
                if (needsBend)
                {
                    TunnelVector3 secondStart = bend + childInward * halfWidth;
                    TunnelVector2 side = GetSideDirection(new TunnelVector2(outward.X, outward.Z));
                    bool turnsRight = side.X * childInward.X + side.Y * childInward.Z > 0.0f;
                    route.Add(new InternalNormalCorridor(firstEnd, secondStart, bend, outward,
                        config.CorridorWidth, index, false, false, !turnsRight, turnsRight));
                    route.Add(CreateStraightCorridor(secondStart, childPort, index));
                }

                if (!CanPlaceTunnelAndConnection(parentOpen.Tunnel.Id, childPosition, childForward, childLength, route, config, occupiedAreas))
                {
                    continue;
                }

                InternalTunnelNode child = AddTunnel(tunnels, openEntrances, occupiedAreas, childPosition, childForward, childLength, false, $"Tunnel {index:00}", config);

                normalCorridors.AddRange(route);
                foreach (InternalNormalCorridor segment in route)
                {
                    occupiedAreas.AddRange(BuildCorridorAreas(segment, config));
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

        /// <summary>
        /// 始点と終点から直線区間のレイアウトを作成する
        /// </summary>
        /// <param name="start">区間の始点</param>
        /// <param name="end">区間の終点</param>
        /// <param name="connectionIndex">接続の識別番号</param>
        /// <param name="hasSmallRoom">区間内に小部屋を配置するか</param>
        /// <returns>直線区間のレイアウト</returns>
        private static InternalNormalCorridor CreateStraightCorridor(
            TunnelVector3 start, TunnelVector3 end, int connectionIndex, bool hasSmallRoom = false)
        {
            TunnelVector3 delta = end - start;
            return new InternalNormalCorridor(start, end, (start + end) * 0.5f,
                delta.Normalized, delta.Magnitude, connectionIndex, hasSmallRoom);
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
            TunnelVector3 start = GetPortPosition(tunnel, entrance, config);
            TunnelVector3 outward = ToWorldDirection(entrance.Direction, tunnel.Forward);
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
                TunnelVector3 start = GetPortPosition(tunnel, entrance, config);
                TunnelVector3 end = start + ToWorldDirection(entrance.Direction, tunnel.Forward) * config.CorridorLength;
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

        /// <summary>
        /// 接続先のトンネルと通路の全区間が既存の配置と重ならないか判定する
        /// </summary>
        /// <param name="parentTunnelId">接続元のトンネル番号</param>
        /// <param name="childPosition">接続先の中心</param>
        /// <param name="childForward">接続先の向き</param>
        /// <param name="childLength">接続先の長さ</param>
        /// <param name="route">接続する通路の区間</param>
        /// <param name="config">生成設定</param>
        /// <param name="occupiedAreas">既存の占有領域</param>
        /// <returns>配置可能ならtrue</returns>
        private static bool CanPlaceTunnelAndConnection(
            int? parentTunnelId,
            TunnelVector3 childPosition,
            TunnelVector2 childForward,
            float childLength,
            List<InternalNormalCorridor> route,
            TunnelGenerationConfig config,
            List<TunnelOccupiedArea> occupiedAreas)
        {
            if (OverlapsAny(CreateTunnelArea(childPosition, childForward, childLength, null, config), null, occupiedAreas))
            {
                return false;
            }

            foreach (InternalNormalCorridor segment in route)
            {
                foreach (TunnelOccupiedArea area in BuildCorridorAreas(segment, config))
                {
                    if (OverlapsAny(area, parentTunnelId, occupiedAreas))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 直線区間または曲がり角の占有領域を取得する
        /// </summary>
        /// <param name="segment">判定する通路区間</param>
        /// <param name="config">生成設定</param>
        /// <returns>通路と小部屋の占有領域</returns>
        private static List<TunnelOccupiedArea> BuildCorridorAreas(InternalNormalCorridor segment, TunnelGenerationConfig config)
        {
            // 曲がり角は通路幅と同じ一辺を持つ正方形として判定する
            if (!segment.OpenEnd)
            {
                return new List<TunnelOccupiedArea>
                {
                    CreateSegmentArea(segment.Center, segment.Direction, segment.Length,
                        config.CorridorWidth, config.CorridorOverlapSizeMultiplier, config.PlacementMargin)
                };
            }

            return BuildConnectionAreas(segment.StartPort, segment.EndPort, segment.HasSmallRoom, config);
        }

        private static TunnelOccupiedArea CreateTunnelArea(InternalTunnelNode tunnel, int? ownerTunnelId, TunnelGenerationConfig config)
        {
            return CreateTunnelArea(tunnel.Position, tunnel.Forward, tunnel.Length, ownerTunnelId, config);
        }

        private static TunnelOccupiedArea CreateTunnelArea(TunnelVector3 position, TunnelVector2 forward, float length, int? ownerTunnelId, TunnelGenerationConfig config)
        {
            TunnelVector2 side = GetSideDirection(forward);
            float sizeX = Math.Abs(forward.X) * length + Math.Abs(side.X) * config.TunnelWidth;
            float sizeZ = Math.Abs(forward.Y) * length + Math.Abs(side.Y) * config.TunnelWidth;
            TunnelVector2 size = new TunnelVector2(sizeX, sizeZ)
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
            float passageLength = (totalLength - roomLength) * 0.5f;

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

        private static List<int> GetOppositeEntrances(TunnelVector3 worldDirection, TunnelVector2 tunnelForward)
        {
            var result = new List<int>(3);
            for (int i = 0; i < Entrances.Length; i++)
            {
                TunnelVector3 entranceDirection = ToWorldDirection(Entrances[i].Direction, tunnelForward);
                if (DotHorizontal(entranceDirection, worldDirection) < -0.99f)
                {
                    result.Add(i);
                }
            }
            return result;
        }

        private static TunnelVector3 GetPortPosition(InternalTunnelNode tunnel, TunnelEntrance entrance, TunnelGenerationConfig config)
        {
            return GetPortPosition(tunnel.Position, tunnel.Forward, tunnel.Length, entrance, config);
        }

        private static TunnelVector3 GetPortPosition(TunnelVector3 tunnelPosition, TunnelVector2 tunnelForward, float tunnelLength, TunnelEntrance entrance, TunnelGenerationConfig config)
        {
            return tunnelPosition + ToWorldOffset(GetPortOffset(entrance, tunnelLength, config), tunnelForward);
        }

        private static TunnelVector3 GetPortOffset(TunnelEntrance entrance, float tunnelLength, TunnelGenerationConfig config)
        {
            // 長さ方向のモデル拡縮と同じ比率で外側の接続口を移動する
            float lengthScale = config.TunnelLength > 0.0f ? tunnelLength / config.TunnelLength : 1.0f;
            float spacing = Math.Min(config.ConnectionPointSpacing * lengthScale, tunnelLength * 0.45f);
            float longitudinalOffset = entrance.NormalizedPosition.Y == 0.0f
                ? 0.0f
                : Math.Sign(entrance.NormalizedPosition.Y) * spacing;

            return new TunnelVector3(
                entrance.NormalizedPosition.X * config.TunnelWidth,
                0.0f,
                longitudinalOffset);
        }

        private static TunnelVector3 ToWorldDirection(TunnelVector2 direction, TunnelVector2 tunnelForward)
        {
            TunnelVector2 side = GetSideDirection(tunnelForward);
            return new TunnelVector3(
                side.X * direction.X + tunnelForward.X * direction.Y,
                0.0f,
                side.Y * direction.X + tunnelForward.Y * direction.Y);
        }

        private static TunnelVector3 ToWorldOffset(TunnelVector3 localOffset, TunnelVector2 tunnelForward)
        {
            TunnelVector2 side = GetSideDirection(tunnelForward);
            return new TunnelVector3(
                side.X * localOffset.X + tunnelForward.X * localOffset.Z,
                localOffset.Y,
                side.Y * localOffset.X + tunnelForward.Y * localOffset.Z);
        }

        private static TunnelVector2 GetSideDirection(TunnelVector2 tunnelForward)
        {
            return new TunnelVector2(tunnelForward.Y, -tunnelForward.X);
        }

        private static float DotHorizontal(TunnelVector3 a, TunnelVector3 b)
        {
            return a.X * b.X + a.Z * b.Z;
        }

        private static TunnelVector2 ChooseTunnelForward(Random random)
        {
            return random.Next(2) == 0 ? TunnelVector2.Right : new TunnelVector2(0.0f, 1.0f);
        }

        private static float ChooseTunnelLength(TunnelGenerationConfig config, Random random)
        {
            float min = Math.Min(config.MinTunnelLength, config.MaxTunnelLength);
            float max = Math.Max(config.MinTunnelLength, config.MaxTunnelLength);
            return max <= min + 1e-4f ? min : min + (float)random.NextDouble() * (max - min);
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

            foreach (var tunnel in tunnels)
            {
                float px = tunnel.Position.X;
                float py = tunnel.Position.Y;
                float pz = tunnel.Position.Z;
                TunnelVector2 side = GetSideDirection(tunnel.Forward);
                float halfX = (Math.Abs(tunnel.Forward.X) * tunnel.Length + Math.Abs(side.X) * config.TunnelWidth) * 0.5f;
                float halfZ = (Math.Abs(tunnel.Forward.Y) * tunnel.Length + Math.Abs(side.Y) * config.TunnelWidth) * 0.5f;

                minX = Math.Min(minX, px - halfX);
                maxX = Math.Max(maxX, px + halfX);
                minY = Math.Min(minY, py);
                maxY = Math.Max(maxY, py + config.TunnelHeight);
                minZ = Math.Min(minZ, pz - halfZ);
                maxZ = Math.Max(maxZ, pz + halfZ);
            }

            TunnelVector3 center = new TunnelVector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
            TunnelVector3 size = new TunnelVector3(maxX - minX, maxY - minY, maxZ - minZ);
            return new TunnelBoundsLayout(center, size);
        }
    }
}
