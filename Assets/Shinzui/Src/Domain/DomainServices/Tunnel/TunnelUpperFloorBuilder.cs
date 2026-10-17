using System;
using System.Collections.Generic;
using System.Linq;
using Shinzui.Domain.ValueObjects.Tunnel;

namespace Shinzui.Domain.DomainServices.Tunnel
{
    /// <summary>
    /// 既存の端トンネルを傾斜させて上階と落下口を接続する
    /// </summary>
    internal static class TunnelUpperFloorBuilder
    {
        private sealed class Placement
        {
            public TunnelNodeLayout Ramp;
            public float Slope;
            public float CenterY;
            public TunnelLayoutResult Upper;
            public int AnchorId;
            public int EntranceIndex;
            public int Turns;
            public TunnelVector3 Offset;
            public List<TunnelDropOpeningLayout> Openings;
        }

        /// <summary>
        /// 独立した上階を生成して既存トンネルの上端に接続する
        /// </summary>
        /// <param name="lower">下階の生成結果</param>
        /// <param name="config">階層と落下口の設定</param>
        /// <param name="generateFloor">一階層分の生成処理</param>
        /// <returns>上下階と傾斜と落下口を含む生成結果</returns>
        public static TunnelLayoutResult Build(TunnelLayoutResult lower, TunnelGenerationConfig config,
            Func<TunnelGenerationConfig, TunnelLayoutResult> generateFloor)
        {
            float height = Math.Max(config.UpperFloorHeight, config.TunnelHeight + 3.0f);
            Placement best = null;
            int requiredOpenings = Math.Max(2, config.DropOpeningCount);

            // 下階の平面接続を維持できる端トンネルだけを傾斜の候補にする
            for (int retry = 0; retry < 8; retry++)
            {
                var upperConfig = new TunnelGenerationConfig(config.TunnelCount,
                    unchecked(config.Seed + 104729 * (retry + 1)), config.PlacementAttemptsPerTunnel,
                    config.SmallRoomCount, config.SmallRoomWidth, config.SmallRoomLength,
                    config.TunnelLength, config.MinTunnelLength, config.MaxTunnelLength,
                    config.ConnectionPointSpacing, config.TunnelWidth, config.TunnelHeight,
                    config.CorridorLength, config.CorridorWidth, config.PlacementMargin,
                    config.TunnelOverlapSizeMultiplier, config.CorridorOverlapSizeMultiplier,
                    config.SmallRoomOverlapSizeMultiplier);
                TunnelLayoutResult upper = generateFloor(upperConfig);
                foreach (var ramp in lower.Tunnels)
                {
                    if (ramp.Id == 0 || ramp.IsSpecial) continue;
                    var entrances = FindPhysicalEntrances(lower, ramp);
                    if (entrances.Count != 1) continue;

                    // 接続済みの側面口から遠い方の端を上階まで持ち上げる
                    float portZ = ramp.EntranceMarkers[entrances[0]].LocalPosition.Z;
                    float sign = portZ > 0.0f ? -1.0f : 1.0f;
                    float run = ramp.Length * 0.5f - sign * portZ;
                    float slope = sign * height / run;
                    if (Math.Abs(slope) > 0.5f) continue;
                    var ascent = new TunnelVector3(ramp.Forward.X * sign, 0, ramp.Forward.Y * sign);
                    var high = ramp.Position + ascent * (ramp.Length * 0.5f) + TunnelVector3.Up * height;

                    foreach (var anchor in upper.Tunnels)
                    for (int turns = 0; turns < 4; turns++)
                    foreach (var marker in anchor.EntranceMarkers)
                    {
                        if (marker.IsOpen || !marker.IsActive || anchor.IsSpecial) continue;
                        TunnelVector3 side = WorldOffset(anchor, new TunnelVector3(marker.Index < 3 ? -1 : 1, 0, 0));
                        if ((Rotate(side, turns) + ascent).Magnitude > 0.001f) continue;
                        TunnelVector3 port = anchor.Position + WorldOffset(anchor,
                            new TunnelVector3(marker.LocalPosition.X, 0, marker.LocalPosition.Z));
                        var offset = high - Rotate(port, turns);
                        if (IntersectsRamp(upper, turns, offset, ramp, slope, -slope * portZ, config, height)) continue;
                        var openings = FindOpenings(lower, upper, ramp.Id, turns, offset, config, height);
                        if (best != null && openings.Count <= best.Openings.Count) continue;
                        best = new Placement { Ramp = ramp, Slope = slope, CenterY = -slope * portZ,
                            Upper = upper, AnchorId = anchor.Id, EntranceIndex = marker.Index,
                            Turns = turns, Offset = offset, Openings = openings };
                    }
                }

                if (best != null && best.Openings.Count >= requiredOpenings) break;
            }

            if (best == null || best.Openings.Count < requiredOpenings)
                throw new InvalidOperationException("Unable to fit an upper floor with two safe overlaps; increase tunnel length or placement attempts");

            return Combine(lower, best, config);
        }

        /// <summary>
        /// 通常通路または小部屋へ実際につながる側面口を取得する
        /// </summary>
        /// <param name="layout">調べる階層</param>
        /// <param name="node">調べるトンネル</param>
        /// <returns>通常接続に使用する入口番号</returns>
        private static List<int> FindPhysicalEntrances(TunnelLayoutResult layout, TunnelNodeLayout node)
        {
            var result = new List<int>();
            foreach (var marker in node.EntranceMarkers)
            {
                var port = node.Position + WorldOffset(node, marker.LocalPosition);
                port = new TunnelVector3(port.X, node.Position.Y, port.Z);
                bool found = layout.NormalCorridors.Any(c => Near(c.StartPort, port) || Near(c.EndPort, port))
                    || layout.SmallRooms.Any(r => Near(r.Center - r.Direction * (r.TotalLength * 0.5f), port)
                        || Near(r.Center + r.Direction * (r.TotalLength * 0.5f), port));
                if (found) result.Add(marker.Index);
            }
            return result;
        }

        /// <summary>
        /// 上階の構造物が傾斜の天井へ食い込む配置を除外する
        /// </summary>
        /// <param name="upper">上階の元レイアウト</param>
        /// <param name="turns">水平回転の回数</param>
        /// <param name="offset">平行移動</param>
        /// <param name="ramp">傾斜にするトンネル</param>
        /// <param name="slope">水平距離あたりの高さ</param>
        /// <param name="centerY">傾斜の中央の高さ</param>
        /// <param name="config">寸法設定</param>
        /// <param name="height">上階の床高</param>
        /// <returns>傾斜と干渉するならtrue</returns>
        private static bool IntersectsRamp(TunnelLayoutResult upper, int turns, TunnelVector3 offset,
            TunnelNodeLayout ramp, float slope, float centerY, TunnelGenerationConfig config, float height)
        {
            var rampArea = Area(ramp.Position, ramp.Forward, config.TunnelWidth, ramp.Length);
            var areas = new List<TunnelOccupiedArea>();
            foreach (var node in upper.Tunnels)
                areas.Add(Area(Rotate(node.Position, turns) + offset, Rotate(node.Forward, turns), config.TunnelWidth, node.Length));
            foreach (var c in upper.NormalCorridors)
            {
                var d = Rotate(c.Direction, turns);
                areas.Add(Area(Rotate(c.Center, turns) + offset, new TunnelVector2(d.X, d.Z), config.CorridorWidth, c.Length));
            }
            foreach (var r in upper.SmallRooms)
            {
                var d = Rotate(r.Direction, turns);
                areas.Add(Area(Rotate(r.Center, turns) + offset, new TunnelVector2(d.X, d.Z), r.Width, r.TotalLength));
            }
            foreach (var w in upper.WarpCorridors)
            {
                var d = Rotate(w.Direction, turns);
                areas.Add(Area(Rotate(w.Center, turns) + offset, new TunnelVector2(d.X, d.Z), config.CorridorWidth, w.Length));
            }

            foreach (var area in areas)
            {
                if (!Intersection(rampArea, area, out var min, out var max)) continue;
                float z0 = (min.X - ramp.Position.X) * ramp.Forward.X + (min.Y - ramp.Position.Z) * ramp.Forward.Y;
                float z1 = (max.X - ramp.Position.X) * ramp.Forward.X + (max.Y - ramp.Position.Z) * ramp.Forward.Y;
                // モデルの床厚と踊り場の補正分も天井の余裕として確保する
                if (centerY + Math.Max(z0 * slope, z1 * slope) + config.TunnelHeight
                    + Math.Max(1.5f, config.PlacementMargin) > height - 0.05f) return true;
            }
            return false;
        }

        /// <summary>
        /// 上下のトンネル内部が重なる場所から異なる着地先を選ぶ
        /// </summary>
        /// <param name="lower">下階</param>
        /// <param name="upper">上階</param>
        /// <param name="rampId">落下先にしない傾斜トンネル</param>
        /// <param name="turns">水平回転の回数</param>
        /// <param name="offset">平行移動</param>
        /// <param name="config">開口部の設定</param>
        /// <param name="height">上階の高さ</param>
        /// <returns>下階へ安全に落下できる候補</returns>
        private static List<TunnelDropOpeningLayout> FindOpenings(TunnelLayoutResult lower, TunnelLayoutResult upper,
            int rampId, int turns, TunnelVector3 offset, TunnelGenerationConfig config, float height)
        {
            var result = new List<TunnelDropOpeningLayout>();
            float size = Math.Min(config.DropOpeningSize, config.TunnelWidth - 1.0f);
            float usableWidth = Math.Min(config.TunnelWidth - 0.5f, size + 1.0f);
            foreach (var low in lower.Tunnels)
            {
                if (low.Id == rampId) continue;
                foreach (var high in upper.Tunnels)
                {
                    if (result.Any(o => o.UpperTunnelId == high.Id + lower.Tunnels.Count)) continue;
                    // 端のポータルや壁に落下口が重ならないよう前後に余裕を残す
                    float lowMargin = Math.Max(size + 2.0f, low.Length * 0.08f);
                    float highMargin = Math.Max(size + 2.0f, high.Length * 0.08f);
                    if (low.Length <= lowMargin * 2 + size || high.Length <= highMargin * 2 + size) continue;
                    var lowArea = Area(low.Position, low.Forward, usableWidth, low.Length - lowMargin * 2);
                    var highArea = Area(Rotate(high.Position, turns) + offset, Rotate(high.Forward, turns), usableWidth, high.Length - highMargin * 2);
                    if (!Intersection(lowArea, highArea, out var min, out var max)) continue;
                    if (max.X - min.X < size + 0.25f || max.Y - min.Y < size + 0.25f) continue;
                    var center = new TunnelVector3((min.X + max.X) * 0.5f, 0, (min.Y + max.Y) * 0.5f);
                    result.Add(new TunnelDropOpeningLayout(low.Id, high.Id + lower.Tunnels.Count, center, size, 0, height));
                    break;
                }
            }
            return result;
        }

        /// <summary>
        /// 上下階を一つの結果にまとめ識別番号と開口部を補正する
        /// </summary>
        /// <param name="lower">下階</param>
        /// <param name="placement">上階と傾斜の配置</param>
        /// <param name="config">生成設定</param>
        /// <returns>階層を結合した生成結果</returns>
        private static TunnelLayoutResult Combine(TunnelLayoutResult lower, Placement placement, TunnelGenerationConfig config)
        {
            var nodes = new List<TunnelNodeLayout>();
            var corridors = new List<NormalCorridorLayout>(lower.NormalCorridors);
            var rooms = new List<SmallRoomLayout>(lower.SmallRooms);
            var warps = new List<WarpCorridorLayout>();
            var removedPairs = new HashSet<int>(lower.WarpCorridors.Where(w => w.TunnelId == placement.Ramp.Id).Select(w => w.PairId));
            foreach (var node in lower.Tunnels)
            {
                bool isRamp = node.Id == placement.Ramp.Id;
                var open = node.OpenEntrances.ToArray();
                foreach (var warp in lower.WarpCorridors.Where(w => removedPairs.Contains(w.PairId) && w.TunnelId == node.Id))
                {
                    foreach (var marker in node.EntranceMarkers)
                    {
                        var port = node.Position + WorldOffset(node, new TunnelVector3(marker.LocalPosition.X, 0, marker.LocalPosition.Z));
                        if (Near(port, warp.StartPort)) open[marker.Index] = false;
                    }
                }
                var markers = node.EntranceMarkers.Select(m => new TunnelEntranceMarkerLayout(m.Index, m.Name,
                    new TunnelVector3(m.LocalPosition.X, m.LocalPosition.Y + (isRamp ? placement.Slope * m.LocalPosition.Z : 0), m.LocalPosition.Z),
                    m.IsActive, open[m.Index])).ToArray();
                float length = isRamp ? node.Length * (float)Math.Sqrt(1.0f + placement.Slope * placement.Slope) : node.Length;
                nodes.Add(new TunnelNodeLayout(node.Id, isRamp ? "Upper Floor Inclined Tunnel " + node.Id : node.Name,
                    node.Position + TunnelVector3.Up * (isRamp ? placement.CenterY : 0), node.Forward, length,
                    node.IsSpecial, markers, open, 0, isRamp ? placement.Slope : 0));
            }

            // 消したワープの相手側も閉じ、残したペアのリスト番号を詰め直す
            var lowerWarpIndices = new Dictionary<int, int>();
            for (int i = 0; i < lower.WarpCorridors.Count; i++)
            {
                if (removedPairs.Contains(lower.WarpCorridors[i].PairId)) continue;
                lowerWarpIndices[i] = warps.Count;
                warps.Add(CopyWarp(lower.WarpCorridors[i], 0, TunnelVector3.Zero, 0, 0));
            }
            foreach (var pair in lowerWarpIndices)
            {
                int originalPair = lower.WarpCorridors[pair.Key].PairedIndex;
                warps[pair.Value].PairedIndex = lowerWarpIndices.TryGetValue(originalPair, out int paired) ? paired : -1;
            }

            int nodeOffset = lower.Tunnels.Count;
            int connectionOffset = lower.Tunnels.Count;
            foreach (var node in placement.Upper.Tunnels)
            {
                var open = node.OpenEntrances.ToArray();
                if (node.Id == placement.AnchorId) open[placement.EntranceIndex] = true;
                var markers = node.EntranceMarkers.Select(m => new TunnelEntranceMarkerLayout(m.Index, m.Name,
                    m.LocalPosition, m.IsActive, open[m.Index])).ToArray();
                nodes.Add(new TunnelNodeLayout(node.Id + nodeOffset, "Upper Floor " + node.Name,
                    Rotate(node.Position, placement.Turns) + placement.Offset, Rotate(node.Forward, placement.Turns),
                    node.Length, node.IsSpecial, markers, open, 1));
            }
            foreach (var c in placement.Upper.NormalCorridors)
                corridors.Add(new NormalCorridorLayout(c.ConnectionIndex + connectionOffset,
                    Rotate(c.StartPort, placement.Turns) + placement.Offset, Rotate(c.EndPort, placement.Turns) + placement.Offset,
                    Rotate(c.Center, placement.Turns) + placement.Offset, Rotate(c.Direction, placement.Turns),
                    c.Length, c.OpenEnd, c.OpenLeft, c.OpenRight));
            foreach (var r in placement.Upper.SmallRooms)
                rooms.Add(new SmallRoomLayout(r.RoomNumber + lower.SmallRooms.Count, r.ConnectionIndex + connectionOffset,
                    Rotate(r.Center, placement.Turns) + placement.Offset, Rotate(r.Direction, placement.Turns),
                    r.TotalLength, r.Width, r.RoomLength, r.PassageLength));
            int warpOffset = warps.Count;
            foreach (var w in placement.Upper.WarpCorridors)
            {
                var copy = CopyWarp(w, placement.Turns, placement.Offset, nodeOffset, 1000);
                copy.PairedIndex = w.PairedIndex < 0 ? -1 : w.PairedIndex + warpOffset;
                warps.Add(copy);
            }

            // 傾斜の上下端も含めてカメラの範囲を広げる
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var node in nodes)
            {
                float horizontalLength = node.Length / (float)Math.Sqrt(1.0f + node.Slope * node.Slope);
                var area = Area(node.Position, node.Forward, config.TunnelWidth, horizontalLength);
                minX = Math.Min(minX, area.Center.X - area.Size.X * 0.5f); maxX = Math.Max(maxX, area.Center.X + area.Size.X * 0.5f);
                minZ = Math.Min(minZ, area.Center.Y - area.Size.Y * 0.5f); maxZ = Math.Max(maxZ, area.Center.Y + area.Size.Y * 0.5f);
                minY = Math.Min(minY, node.Position.Y - Math.Abs(node.Slope) * horizontalLength * 0.5f);
                maxY = Math.Max(maxY, node.Position.Y + Math.Abs(node.Slope) * horizontalLength * 0.5f + config.TunnelHeight);
            }
            var bounds = new TunnelBoundsLayout(new TunnelVector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f),
                new TunnelVector3(maxX - minX, maxY - minY, maxZ - minZ));
            return new TunnelLayoutResult(nodes, corridors, rooms, warps, lower.SpecialTunnelId, bounds, config.Seed,
                placement.Openings.Take(config.DropOpeningCount).ToArray());
        }

        /// <summary>
        /// ワープ区間を水平回転と平行移動で複製する
        /// </summary>
        /// <param name="warp">元の区間</param>
        /// <param name="turns">水平回転の回数</param>
        /// <param name="offset">平行移動</param>
        /// <param name="nodeOffset">トンネル番号の加算値</param>
        /// <param name="pairOffset">ペア番号の加算値</param>
        /// <returns>変換後の区間</returns>
        private static WarpCorridorLayout CopyWarp(WarpCorridorLayout warp, int turns, TunnelVector3 offset, int nodeOffset, int pairOffset)
        {
            return new WarpCorridorLayout(warp.PairId + pairOffset, warp.SideName, warp.TunnelId + nodeOffset,
                Rotate(warp.StartPort, turns) + offset, Rotate(warp.EndPort, turns) + offset,
                Rotate(warp.Center, turns) + offset, Rotate(warp.Direction, turns), warp.Length);
        }

        /// <summary>
        /// トンネル内の位置を水平のマップ座標へ変換する
        /// </summary>
        /// <param name="node">基準トンネル</param>
        /// <param name="local">ローカル位置</param>
        /// <returns>水平座標の差分</returns>
        private static TunnelVector3 WorldOffset(TunnelNodeLayout node, TunnelVector3 local)
        {
            return new TunnelVector3(node.Forward.Y * local.X + node.Forward.X * local.Z, local.Y,
                -node.Forward.X * local.X + node.Forward.Y * local.Z);
        }

        /// <summary>
        /// 点または方向をXZ平面で90度単位に回転する
        /// </summary>
        /// <param name="value">変換する値</param>
        /// <param name="turns">回転回数</param>
        /// <returns>回転後の値</returns>
        private static TunnelVector3 Rotate(TunnelVector3 value, int turns)
        {
            for (int i = 0; i < turns; i++) value = new TunnelVector3(value.Z, value.Y, -value.X);
            return value;
        }

        /// <summary>
        /// 水平方向を90度単位に回転する
        /// </summary>
        /// <param name="value">変換する方向</param>
        /// <param name="turns">回転回数</param>
        /// <returns>回転後の方向</returns>
        private static TunnelVector2 Rotate(TunnelVector2 value, int turns)
        {
            var result = Rotate(new TunnelVector3(value.X, 0, value.Y), turns);
            return new TunnelVector2(result.X, result.Z);
        }

        /// <summary>
        /// 軸に平行なトンネルの水平矩形を作成する
        /// </summary>
        /// <param name="center">水平中心</param>
        /// <param name="forward">長さ方向</param>
        /// <param name="width">幅</param>
        /// <param name="length">水平の長さ</param>
        /// <returns>水平の占有矩形</returns>
        private static TunnelOccupiedArea Area(TunnelVector3 center, TunnelVector2 forward, float width, float length)
        {
            return new TunnelOccupiedArea(new TunnelVector2(center.X, center.Z),
                new TunnelVector2(Math.Abs(forward.X) * length + Math.Abs(forward.Y) * width,
                    Math.Abs(forward.Y) * length + Math.Abs(forward.X) * width));
        }

        /// <summary>
        /// 二つの水平矩形の共通部分を取得する
        /// </summary>
        /// <param name="a">一つ目の矩形</param>
        /// <param name="b">二つ目の矩形</param>
        /// <param name="min">共通部分の下限</param>
        /// <param name="max">共通部分の上限</param>
        /// <returns>面積を持つ共通部分があればtrue</returns>
        private static bool Intersection(TunnelOccupiedArea a, TunnelOccupiedArea b, out TunnelVector2 min, out TunnelVector2 max)
        {
            min = new TunnelVector2(Math.Max(a.Center.X - a.Size.X * 0.5f, b.Center.X - b.Size.X * 0.5f),
                Math.Max(a.Center.Y - a.Size.Y * 0.5f, b.Center.Y - b.Size.Y * 0.5f));
            max = new TunnelVector2(Math.Min(a.Center.X + a.Size.X * 0.5f, b.Center.X + b.Size.X * 0.5f),
                Math.Min(a.Center.Y + a.Size.Y * 0.5f, b.Center.Y + b.Size.Y * 0.5f));
            return max.X - min.X > 0.01f && max.Y - min.Y > 0.01f;
        }

        /// <summary>
        /// 計算誤差の範囲で接続端が一致するか判定する
        /// </summary>
        /// <param name="a">一つ目の端点</param>
        /// <param name="b">二つ目の端点</param>
        /// <returns>一致するならtrue</returns>
        private static bool Near(TunnelVector3 a, TunnelVector3 b) => (a - b).Magnitude < 0.01f;
    }
}
