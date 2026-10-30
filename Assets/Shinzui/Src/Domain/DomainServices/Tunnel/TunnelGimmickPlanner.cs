using System;
using System.Collections.Generic;
using System.Linq;
using Shinzui.Domain.ValueObjects.Tunnel;

namespace Shinzui.Domain.DomainServices.Tunnel
{
    /// <summary>通路断面と側壁へ決定論的にギミックを割り当てる</summary>
    public sealed class TunnelGimmickPlanner : ITunnelGimmickPlanner
    {
        /// <inheritdoc />
        public IReadOnlyList<TunnelGimmickPlacement> Plan(TunnelLayoutResult layout, TunnelGenerationConfig config,
            int? goalTunnelId, int goalEndSign)
        {
            var random = new Random(unchecked(layout.Seed ^ 0x47494d4b));
            var placements = new List<TunnelGimmickPlacement>();

            // 両側壁が閉じた通路を抽選し、両端から離して巣を張る
            var corridors = layout.NormalCorridors.Select((corridor, index) => (Corridor: corridor, Index: index))
                .Where(c => !c.Corridor.OpenLeft && !c.Corridor.OpenRight && c.Corridor.Length >= 3).ToList();
            Shuffle(corridors, random);
            foreach (var candidate in corridors.Take(Math.Max(0, config.SpiderWebCount)))
            {
                var corridor = candidate.Corridor;
                float z = (float)((random.NextDouble() - 0.5) * (corridor.Length - 3));
                placements.Add(new TunnelGimmickPlacement(TunnelGimmickKind.SpiderWeb,
                    candidate.Index, z, 0, random.Next()));
            }

            // 通常の平坦なトンネルごとに最大8回、側壁上の空き位置を探す
            var tunnels = layout.Tunnels.Where(t => t.Id != 0 && !t.IsSpecial && t.Slope == 0).ToList();
            Shuffle(tunnels, random);
            var alarmPositions = new List<TunnelVector3>();
            foreach (var tunnel in tunnels)
            {
                if (alarmPositions.Count >= config.FireAlarmCount) break;
                float minZ = -tunnel.Length * 0.5f + Math.Max(0, config.FireAlarmEndMargin);
                float maxZ = tunnel.Length * 0.5f - Math.Max(0, config.FireAlarmEndMargin);
                if (tunnel.Id == goalTunnelId)
                {
                    if (goalEndSign < 0) minZ = Math.Max(minZ, -tunnel.Length * 0.5f + 20);
                    else maxZ = Math.Min(maxZ, tunnel.Length * 0.5f - 20);
                }
                if (minZ > maxZ) continue;

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    int side = random.Next(2) == 0 ? -1 : 1;
                    float z = minZ + (float)random.NextDouble() * (maxZ - minZ);
                    if (tunnel.EntranceMarkers.Any(m => m.IsOpen && Math.Sign(m.LocalPosition.X) == side &&
                        Math.Abs(m.LocalPosition.Z - z) < config.FireAlarmMouthMargin)) continue;

                    // 上下どちらの落下口からも長さ方向に距離を取り、報知器同士も離す
                    if (layout.DropOpenings.Any(o => (o.LowerTunnelId == tunnel.Id || o.UpperTunnelId == tunnel.Id) &&
                        Math.Abs((o.Center.X - tunnel.Position.X) * tunnel.Forward.X +
                                 (o.Center.Z - tunnel.Position.Z) * tunnel.Forward.Y - z) < o.Size + 2)) continue;
                    var position = tunnel.Position + new TunnelVector3(tunnel.Forward.X, 0, tunnel.Forward.Y) * z +
                        new TunnelVector3(tunnel.Forward.Y, 0, -tunnel.Forward.X) * (side * config.TunnelWidth * 0.5f);
                    if (alarmPositions.Any(p => (p - position).Magnitude < config.FireAlarmMinSpacing)) continue;

                    placements.Add(new TunnelGimmickPlacement(TunnelGimmickKind.FireAlarm, tunnel.Id, z, side, 0));
                    alarmPositions.Add(position);
                    break;
                }
            }
            // 既存の巣と報知器の抽選後だけ乱数を使い、配置結果を維持する
            var lockerPositions = new List<TunnelVector3>();
            var candidates = new List<(TunnelGimmickPlacement Placement, TunnelVector3 Position)>();
            var rooms = layout.SmallRooms.ToList();
            Shuffle(rooms, random);
            foreach (var room in rooms)
            {
                if (room.Width < 3f || room.RoomLength < 2f) continue;
                int side = random.Next(2) == 0 ? -1 : 1;
                var position = room.Center + new TunnelVector3(room.Direction.Z, 0f, -room.Direction.X) * (side * (room.Width * .5f - .4f));
                if (alarmPositions.Any(p => (p - position).Magnitude < 3f)) continue;
                candidates.Add((new TunnelGimmickPlacement(TunnelGimmickKind.HidingLocker, -room.RoomNumber - 1, 0f, side, 0), position));
            }
            foreach (var tunnel in tunnels)
            {
                float min = -tunnel.Length * .5f + Math.Max(2f, config.FireAlarmEndMargin);
                float max = tunnel.Length * .5f - Math.Max(2f, config.FireAlarmEndMargin);
                if (tunnel.Id == goalTunnelId)
                {
                    if (goalEndSign < 0) min = Math.Max(min, -tunnel.Length * .5f + 20f);
                    else max = Math.Min(max, tunnel.Length * .5f - 20f);
                }
                if (min > max) continue;
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    int side = random.Next(2) == 0 ? -1 : 1;
                    float z = min + (float)random.NextDouble() * (max - min);
                    if (tunnel.EntranceMarkers.Any(m => m.IsOpen && Math.Sign(m.LocalPosition.X) == side
                        && Math.Abs(m.LocalPosition.Z - z) < Math.Max(2f, config.FireAlarmMouthMargin))) continue;
                    if (layout.DropOpenings.Any(o => (o.LowerTunnelId == tunnel.Id || o.UpperTunnelId == tunnel.Id)
                        && Math.Abs((o.Center.X - tunnel.Position.X) * tunnel.Forward.X + (o.Center.Z - tunnel.Position.Z) * tunnel.Forward.Y - z) < o.Size + 2)) continue;
                    var position = tunnel.Position + new TunnelVector3(tunnel.Forward.X, 0f, tunnel.Forward.Y) * z
                        + new TunnelVector3(tunnel.Forward.Y, 0f, -tunnel.Forward.X) * (side * (config.TunnelWidth * .5f - .4f));
                    if (alarmPositions.Any(p => (p - position).Magnitude < 3f)) continue;
                    candidates.Add((new TunnelGimmickPlacement(TunnelGimmickKind.HidingLocker, tunnel.Id, z, side, 0), position));
                }
            }
            // 最初の1個は小部屋を優先し、以後は既存ロッカーから最も遠い候補を選ぶ
            var usedHosts = new HashSet<int>();
            while (lockerPositions.Count < config.HidingLockerCount && candidates.Count > 0)
            {
                // 未配置の部屋やトンネルを優先し、不足時だけ同じ場所へ追加する
                var available = candidates.Where(c => !usedHosts.Contains(c.Placement.HostId)).ToList();
                if (available.Count == 0) available = candidates;
                var selected = lockerPositions.Count == 0 ? available[0]
                    : available.OrderByDescending(c => lockerPositions.Min(p => (p - c.Position).Magnitude)).First();
                placements.Add(selected.Placement);
                lockerPositions.Add(selected.Position);
                usedHosts.Add(selected.Placement.HostId);

                // 選択済み候補と最低間隔を満たさない候補を除外する
                candidates.Remove(selected);
                candidates.RemoveAll(c => (c.Position - selected.Position).Magnitude < config.HidingLockerMinSpacing);
            }
            // 車は最後に抽選し、既存ギミックの配置と乱数消費を維持する
            var carPositions = new List<TunnelVector3>();
            const float carRadius = 2.1f;
            float carWidth = config.CarPlacementWidth > 0f ? Math.Min(config.TunnelWidth, config.CarPlacementWidth) : config.TunnelWidth;
            float xRange = carWidth * .5f - carRadius - 1f;
            for (int pass = 0; pass < Math.Max(0, config.CarCount) && xRange >= 0f; pass++)
            foreach (var tunnel in tunnels)
            {
                if (carPositions.Count >= config.CarCount) break;
                float min = -tunnel.Length * .5f + Math.Max(6f, config.FireAlarmEndMargin);
                float max = tunnel.Length * .5f - Math.Max(6f, config.FireAlarmEndMargin);
                if (tunnel.Id == goalTunnelId)
                {
                    if (goalEndSign < 0) min = Math.Max(min, -tunnel.Length * .5f + 20f + carRadius);
                    else max = Math.Min(max, tunnel.Length * .5f - 20f - carRadius);
                }
                if (min > max) continue;

                // 全周の占有半径を確保し、接続口と落下口を避ける
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    float z = min + (float)random.NextDouble() * (max - min);
                    float x = ((float)random.NextDouble() * 2f - 1f) * xRange;
                    if (tunnel.EntranceMarkers.Any(m => m.IsOpen && Math.Abs(m.LocalPosition.Z - z)
                        < config.CorridorWidth * .5f + carRadius + 1f)) continue;
                    if (layout.DropOpenings.Any(o => (o.LowerTunnelId == tunnel.Id || o.UpperTunnelId == tunnel.Id)
                        && Math.Abs((o.Center.X - tunnel.Position.X) * tunnel.Forward.X
                            + (o.Center.Z - tunnel.Position.Z) * tunnel.Forward.Y - z) < o.Size + carRadius)) continue;
                    var position = tunnel.Position + new TunnelVector3(tunnel.Forward.X, 0f, tunnel.Forward.Y) * z
                        + new TunnelVector3(tunnel.Forward.Y, 0f, -tunnel.Forward.X) * x;
                    if (carPositions.Any(p => (p - position).Magnitude < Math.Max(carRadius * 2f + 1f, config.CarMinSpacing))
                        || alarmPositions.Any(p => (p - position).Magnitude < carRadius + 1.5f)
                        || lockerPositions.Any(p => (p - position).Magnitude < carRadius + 1.5f)) continue;

                    float yaw = (float)random.NextDouble() * 360f;
                    placements.Add(new TunnelGimmickPlacement(TunnelGimmickKind.Car, tunnel.Id, z, 0,
                        random.Next(), x, yaw));
                    carPositions.Add(position);
                    break;
                }
            }
            return placements;
        }

        /// <summary>Fisher–Yates法で候補を並べ替える</summary>
        /// <typeparam name="T">候補の型</typeparam>
        /// <param name="items">並べ替える候補</param>
        /// <param name="random">配置専用の乱数</param>
        private static void Shuffle<T>(IList<T> items, Random random)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
