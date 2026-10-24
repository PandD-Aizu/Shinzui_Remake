using System.Linq;
using NUnit.Framework;
using Shinzui.Domain.DomainServices.Tunnel;
using Shinzui.Domain.ValueObjects.Tunnel;

namespace Shinzui.Tests
{
    public class TunnelLayoutGeneratorTests
    {
        /// <summary>
        /// 上下階の生成を独立して切り替え、傾斜と落下口の接続を維持できることを確認する
        /// </summary>
        /// <param name="upper">上階を生成するか</param>
        /// <param name="lower">下階を生成するか</param>
        /// <param name="seed">生成シード</param>
        [TestCase(false, false, 2777)]
        [TestCase(true, false, 2777)]
        [TestCase(false, true, 2777)]
        [TestCase(true, true, 2777)]
        [TestCase(true, true, 42)]
        [TestCase(true, true, 12345)]
        public void GenerateLayout_FloorTogglesPreserveSignedConnections(bool upper, bool lower, int seed)
        {
            var config = new TunnelGenerationConfig { GenerateUpperFloor = upper, GenerateLowerFloor = lower, Seed = seed };
            var generator = new TunnelLayoutGenerator();
            var result = generator.GenerateLayout(config);
            var repeated = generator.GenerateLayout(config);
            int adjacentCount = (upper ? 1 : 0) + (lower ? 1 : 0);

            // 通常階の本数は変えず、有効な階層だけを同じ本数で追加する
            Assert.That(result.Tunnels.Count(t => t.FloorIndex == 0), Is.EqualTo(8));
            Assert.That(result.Tunnels.Count(t => t.FloorIndex == 1), Is.EqualTo(upper ? 8 : 0));
            Assert.That(result.Tunnels.Count(t => t.FloorIndex == -1), Is.EqualTo(lower ? 8 : 0));
            Assert.That(result.Tunnels.Select(t => t.Id).Distinct().Count(), Is.EqualTo(result.Tunnels.Count));
            Assert.That(result.Tunnels.Count(t => t.Slope != 0), Is.EqualTo(adjacentCount));
            Assert.That(result.DropOpenings.Count, Is.EqualTo(adjacentCount * 2));

            foreach (var ramp in result.Tunnels.Where(t => t.Slope != 0))
            {
                Assert.That(ramp.FloorIndex, Is.Zero);
                Assert.That(ramp.Id, Is.Not.Zero);
                var mouth = ramp.EntranceMarkers.Single(m => m.IsOpen);
                Assert.That(ramp.Position.Y + ramp.Slope * mouth.LocalPosition.Z, Is.EqualTo(0).Within(0.001f));
                float half = ramp.Length / (float)System.Math.Sqrt(1 + ramp.Slope * ramp.Slope) * 0.5f;
                var exit = ramp.Position + new TunnelVector3(ramp.Forward.X, ramp.Slope, ramp.Forward.Y) * (half * ramp.SlopeExitSign);
                int floor = System.Math.Sign(exit.Y);
                Assert.That(exit.Y, Is.EqualTo(floor > 0 ? config.UpperFloorHeight : -config.LowerFloorDepth).Within(0.001f));
                Assert.That(result.Tunnels.Where(t => t.FloorIndex == floor).Any(t => t.EntranceMarkers.Any(m =>
                {
                    var port = t.Position + new TunnelVector3(t.Forward.Y * m.LocalPosition.X + t.Forward.X * m.LocalPosition.Z,
                        0, -t.Forward.X * m.LocalPosition.X + t.Forward.Y * m.LocalPosition.Z);
                    return m.IsOpen && (port - exit).Magnitude < 0.001f;
                })), Is.True);
            }

            foreach (var opening in result.DropOpenings)
            {
                var lowNode = result.Tunnels.Single(t => t.Id == opening.LowerTunnelId);
                var highNode = result.Tunnels.Single(t => t.Id == opening.UpperTunnelId);
                Assert.That(lowNode.Slope, Is.Zero);
                Assert.That(highNode.Slope, Is.Zero);
                Assert.That(lowNode.FloorIndex, Is.EqualTo(highNode.FloorIndex - 1));
                Assert.That(opening.LowerFloorY, Is.LessThan(opening.UpperFloorY));
            }
            Assert.That(repeated.Tunnels.Select(t => t.Position), Is.EqualTo(result.Tunnels.Select(t => t.Position)));
            Assert.That(repeated.DropOpenings.Select(o => o.Center), Is.EqualTo(result.DropOpenings.Select(o => o.Center)));
            foreach (var warp in result.WarpCorridors.Where(w => w.PairedIndex >= 0))
            {
                var paired = result.WarpCorridors[warp.PairedIndex];
                Assert.That(paired.PairId, Is.EqualTo(warp.PairId));
                Assert.That(result.WarpCorridors[paired.PairedIndex], Is.SameAs(warp));
            }

            // 下階を追加しても、生成済みの上階とその落下口を変更しない
            if (upper && lower)
            {
                var upperOnly = generator.GenerateLayout(new TunnelGenerationConfig { GenerateUpperFloor = true, Seed = seed });
                foreach (var node in upperOnly.Tunnels.Where(t => t.FloorIndex == 1 || t.Slope != 0))
                {
                    var retained = result.Tunnels.Single(t => t.Id == node.Id);
                    Assert.That(retained.Position, Is.EqualTo(node.Position));
                    Assert.That(retained.FloorIndex, Is.EqualTo(node.FloorIndex));
                    Assert.That(retained.Slope, Is.EqualTo(node.Slope));
                    Assert.That(retained.SlopeExitSign, Is.EqualTo(node.SlopeExitSign));
                }
                Assert.That(result.DropOpenings.Take(2).Select(o => o.Center),
                    Is.EqualTo(upperOnly.DropOpenings.Select(o => o.Center)));
            }
        }

        /// <summary>
        /// 同じ生成規則で上階ができ、傾斜の上下端と落下口がつながることを確認する
        /// </summary>
        /// <param name="seed">生成に使用するシード値</param>
        [TestCase(2777)]
        [TestCase(42)]
        [TestCase(12345)]
        public void GenerateLayout_UpperFloorPreservesConnectionsAndProvidesDistinctDrops(int seed)
        {
            var config = new TunnelGenerationConfig { GenerateUpperFloor = true, Seed = seed };
            var generator = new TunnelLayoutGenerator();
            var result = generator.GenerateLayout(config);
            var repeated = generator.GenerateLayout(config);

            // 追加した別の坂ではなく既存の下階トンネル一本だけが傾斜する
            Assert.That(result.Tunnels.Count(t => t.FloorIndex == 0), Is.EqualTo(config.TunnelCount));
            Assert.That(result.Tunnels.Count(t => t.FloorIndex == 1), Is.EqualTo(config.TunnelCount));
            var ramp = result.Tunnels.Single(t => t.Slope != 0.0f);
            Assert.That(ramp.Id, Is.Not.EqualTo(0));
            Assert.That(ramp.FloorIndex, Is.Zero);
            Assert.That(System.Math.Abs(ramp.Slope), Is.LessThanOrEqualTo(0.5f));
            var mouth = ramp.EntranceMarkers.Single(m => m.IsOpen);
            Assert.That(ramp.Position.Y + mouth.LocalPosition.Y, Is.EqualTo(0.16f).Within(0.001f));

            // 上端の位置が上階の開いた接続口と一致する
            float horizontalLength = ramp.Length / (float)System.Math.Sqrt(1 + ramp.Slope * ramp.Slope);
            float sign = System.Math.Sign(ramp.Slope);
            var high = ramp.Position + new TunnelVector3(ramp.Forward.X, ramp.Slope, ramp.Forward.Y)
                * (horizontalLength * 0.5f * sign);
            Assert.That(high.Y, Is.EqualTo(config.UpperFloorHeight).Within(0.001f));
            Assert.That(result.Tunnels.Where(t => t.FloorIndex == 1).Any(t => t.EntranceMarkers.Any(m =>
            {
                var port = t.Position + new TunnelVector3(t.Forward.Y * m.LocalPosition.X + t.Forward.X * m.LocalPosition.Z,
                    0, -t.Forward.X * m.LocalPosition.X + t.Forward.Y * m.LocalPosition.Z);
                return m.IsOpen && (port - high).Magnitude < 0.001f;
            })), Is.True);

            // 落下口は異なる下階のトンネル内部にあり、シードで再現できる
            Assert.That(result.DropOpenings.Count, Is.EqualTo(2));
            Assert.That(result.DropOpenings.Select(o => o.LowerTunnelId).Distinct().Count(), Is.EqualTo(2));
            for (int i = 0; i < result.DropOpenings.Count; i++)
            {
                var opening = result.DropOpenings[i];
                Assert.That(opening.LowerTunnelId, Is.Not.EqualTo(ramp.Id));
                Assert.That(opening.Center, Is.EqualTo(repeated.DropOpenings[i].Center));
                Assert.That(result.Tunnels.Single(t => t.Id == opening.UpperTunnelId).FloorIndex, Is.EqualTo(1));
            }

            // ペアの識別番号と参照先は上下階を結合しても衝突しない
            foreach (var warp in result.WarpCorridors.Where(w => w.PairedIndex >= 0))
                Assert.That(result.WarpCorridors[warp.PairedIndex].PairId, Is.EqualTo(warp.PairId));
        }

        [Test]
        public void GenerateLayout_AllTunnelsHaveSixEntranceMarkersAndOpenEntrances()
        {
            var generator = new TunnelLayoutGenerator();
            var config = new TunnelGenerationConfig
            {
                TunnelCount = 6,
                Seed = 12345
            };

            TunnelLayoutResult result = generator.GenerateLayout(config);

            Assert.That(result.Tunnels, Is.Not.Null);
            Assert.That(result.Tunnels.Count, Is.GreaterThanOrEqualTo(5));

            foreach (var tunnel in result.Tunnels)
            {
                Assert.That(tunnel.EntranceMarkers.Count, Is.EqualTo(6));
                Assert.That(tunnel.OpenEntrances, Is.Not.Null);
                Assert.That(tunnel.OpenEntrances.Count, Is.EqualTo(6));

                for (int i = 0; i < 6; i++)
                {
                    Assert.That(tunnel.EntranceMarkers[i].IsOpen, Is.EqualTo(tunnel.OpenEntrances[i]));
                }
            }
        }

        [Test]
        public void GenerateLayout_ConnectedEntrancesAreOpenAndUnusedAreClosed()
        {
            var generator = new TunnelLayoutGenerator();
            var config = new TunnelGenerationConfig
            {
                TunnelCount = 8,
                SmallRoomCount = 2,
                Seed = 2777
            };

            TunnelLayoutResult result = generator.GenerateLayout(config);

            // 区間数ではなく接続数からトンネルの開口部の総数を確認する
            int totalOpenEntrances = result.Tunnels.Sum(t => t.OpenEntrances.Count(isOpen => isOpen));

            int connectionCount = result.NormalCorridors.Select(c => c.ConnectionIndex)
                .Concat(result.SmallRooms.Select(r => r.ConnectionIndex)).Distinct().Count();
            int expectedConnections = connectionCount * 2
                                    + result.WarpCorridors.Count;

            Assert.That(totalOpenEntrances, Is.EqualTo(expectedConnections));

            // 各トンネルにおいて少なくとも1つの出口が開いていること
            foreach (var tunnel in result.Tunnels)
            {
                int openCount = tunnel.OpenEntrances.Count(isOpen => isOpen);
                Assert.That(openCount, Is.GreaterThan(0));
                Assert.That(openCount, Is.LessThanOrEqualTo(6));
            }
        }

        [Test]
        public void GenerateLayout_IsDeterministicWithSameSeed()
        {
            var generator = new TunnelLayoutGenerator();
            var config1 = new TunnelGenerationConfig { TunnelCount = 7, Seed = 9999 };
            var config2 = new TunnelGenerationConfig { TunnelCount = 7, Seed = 9999 };

            TunnelLayoutResult result1 = generator.GenerateLayout(config1);
            TunnelLayoutResult result2 = generator.GenerateLayout(config2);

            Assert.That(result1.Tunnels.Count, Is.EqualTo(result2.Tunnels.Count));
            for (int i = 0; i < result1.Tunnels.Count; i++)
            {
                for (int m = 0; m < 6; m++)
                {
                    Assert.That(result1.Tunnels[i].OpenEntrances[m], Is.EqualTo(result2.Tunnels[i].OpenEntrances[m]));
                    Assert.That(result1.Tunnels[i].EntranceMarkers[m].IsOpen, Is.EqualTo(result2.Tunnels[i].EntranceMarkers[m].IsOpen));
                }
            }
        }

        [Test]
        public void GenerateLayout_UsesRandomTunnelLengthWithinConfiguredRange()
        {
            var generator = new TunnelLayoutGenerator();
            var config = new TunnelGenerationConfig
            {
                TunnelCount = 8,
                Seed = 31415,
                MinTunnelLength = 90.0f,
                MaxTunnelLength = 150.0f,
                ConnectionPointSpacing = 30.0f
            };

            TunnelLayoutResult result = generator.GenerateLayout(config);

            Assert.That(result.Tunnels.All(t => t.Length >= 90.0f && t.Length <= 150.0f), Is.True);
            Assert.That(result.Tunnels.Select(t => t.Length).Distinct().Count(), Is.GreaterThan(1));

            // 長さ方向のモデル拡縮に合わせて外側の接続口も移動することを確認する
            foreach (var tunnel in result.Tunnels)
            {
                float expectedSpacing = config.ConnectionPointSpacing * tunnel.Length / config.TunnelLength;
                Assert.That(tunnel.EntranceMarkers[0].LocalPosition.Z,
                    Is.EqualTo(expectedSpacing).Within(0.001f));
            }
        }

        [Test]
        public void GenerateLayout_UsesOnlyHorizontalOrVerticalTunnelDirections()
        {
            var generator = new TunnelLayoutGenerator();
            var config = new TunnelGenerationConfig
            {
                TunnelCount = 8,
                Seed = 27182,
                MinTunnelLength = 100.0f,
                MaxTunnelLength = 130.0f
            };

            TunnelLayoutResult result = generator.GenerateLayout(config);

            Assert.That(result.Tunnels.All(t =>
                (t.Forward.X == 1.0f && t.Forward.Y == 0.0f) ||
                (t.Forward.X == 0.0f && t.Forward.Y == 1.0f)), Is.True);
            Assert.That(result.Tunnels.Select(t => t.Forward).Distinct().Count(), Is.EqualTo(2));
        }

        /// <summary>
        /// 接続口が向かい合わない向きの抽選が含まれても生成が完了することを確認する
        /// </summary>
        /// <param name="seed">生成に使用するシード値</param>
        [TestCase(12345)]
        [TestCase(2777)]
        [TestCase(27182)]
        public void GenerateLayout_RetriesIncompatibleDirectionsWithoutStopping(int seed)
        {
            // 長さと向きを抽選してトンネルを生成する
            var generator = new TunnelLayoutGenerator();
            var config = new TunnelGenerationConfig
            {
                TunnelCount = 8,
                Seed = seed,
                MinTunnelLength = 100.0f,
                MaxTunnelLength = 130.0f
            };

            TunnelLayoutResult result = generator.GenerateLayout(config);

            // 開始地点と接続先のトンネルが生成されていることを確認する
            Assert.That(result.Tunnels.Count, Is.EqualTo(config.TunnelCount));
            Assert.That(result.NormalCorridors.Select(c => c.ConnectionIndex)
                .Concat(result.SmallRooms.Select(r => r.ConnectionIndex)).Distinct().Count(),
                Is.EqualTo(result.Tunnels.Count - 1));
            Assert.That(result.Tunnels[0].Position, Is.EqualTo(TunnelVector3.Zero));
        }

        /// <summary>
        /// L字通路の各区間と小部屋の接続端に隙間がないことを確認する
        /// </summary>
        /// <param name="seed">生成に使用するシード値</param>
        [TestCase(2777)]
        [TestCase(42)]
        [TestCase(12345)]
        public void GenerateLayout_OrthogonalRoutesHaveContinuousPortsAndOneRoomPerConnection(int seed)
        {
            // 小部屋を含む縦横のトンネルを生成する
            var config = new TunnelGenerationConfig { Seed = seed, TunnelCount = 8, SmallRoomCount = 2 };
            TunnelLayoutResult result = new TunnelLayoutGenerator().GenerateLayout(config);
            Assert.That(result.Tunnels.Count, Is.EqualTo(8));
            Assert.That(result.NormalCorridors.Any(c => !c.OpenEnd), Is.True);
            Assert.That(result.SmallRooms.GroupBy(r => r.ConnectionIndex).All(g => g.Count() == 1), Is.True);
            Assert.That(result.SmallRooms.Count, Is.EqualTo(config.SmallRoomCount));

            foreach (var route in result.NormalCorridors.GroupBy(c => c.ConnectionIndex))
            {
                // 小部屋区間が先頭にある場合はその出口から連続性を確認する
                var room = result.SmallRooms.FirstOrDefault(r => r.ConnectionIndex == route.Key);
                TunnelVector3? previousEnd = room == null ? null : room.Center + room.Direction * (room.TotalLength * 0.5f);
                foreach (var segment in route)
                {
                    Assert.That(segment.Length, Is.GreaterThan(0.0f));
                    Assert.That(System.Math.Abs(segment.Direction.X) + System.Math.Abs(segment.Direction.Z),
                        Is.EqualTo(1.0f).Within(0.0001f));
                    if (previousEnd.HasValue)
                    {
                        Assert.That((segment.StartPort - previousEnd.Value).Magnitude, Is.LessThan(0.001f));
                    }

                    // 曲がり角は正面を閉じ、曲がる側の開口部だけを開ける
                    if (!segment.OpenEnd)
                    {
                        Assert.That(segment.Length, Is.EqualTo(config.CorridorWidth));
                        Assert.That(segment.OpenLeft != segment.OpenRight, Is.True);
                    }

                    previousEnd = segment.EndPort;
                }
            }

            // 小部屋と前後の通路が区間全体の長さを満たしていることを確認する
            foreach (var room in result.SmallRooms)
            {
                Assert.That(room.PassageLength, Is.GreaterThan(0.0f));
                Assert.That(room.RoomLength + room.PassageLength * 2.0f,
                    Is.EqualTo(room.TotalLength).Within(0.001f));
            }
        }
    }
}
