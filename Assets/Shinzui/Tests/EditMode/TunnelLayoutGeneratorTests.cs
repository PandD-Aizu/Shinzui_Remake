using System.Linq;
using NUnit.Framework;
using Shinzui.Domain.DomainServices.Tunnel;
using Shinzui.Domain.ValueObjects.Tunnel;

namespace Shinzui.Tests
{
    public class TunnelLayoutGeneratorTests
    {
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
