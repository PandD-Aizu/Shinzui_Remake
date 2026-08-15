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

            // 全トンネルで、開いている出口の総数が（通常通路の接続口2つ * 通路数 + 小部屋の接続口2つ * 小部屋数 + ワープ接続口1つ * ワープ数）と整合すること
            int totalOpenEntrances = result.Tunnels.Sum(t => t.OpenEntrances.Count(isOpen => isOpen));

            int expectedConnections = result.NormalCorridors.Count * 2
                                    + result.SmallRooms.Count * 2
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
    }
}
