using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shinzui.Application.CustomSpatialAudio;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Application.UseCases.Tunnel;
using Shinzui.Infrastructure.CustomSpatialAudio;

namespace Shinzui.Tests.CustomSpatialAudio
{
    public sealed class GeneratedAcousticWorldTests
    {
        [TestCase(2777)] [TestCase(42)] [TestCase(1234)] [TestCase(1)] [TestCase(999)]
        public void ProductionDimensionsProduceConnectedNonoverlappingCells(int seed)
        {
            var useCase = new GenerateTunnelUseCase(null);
            Assert.That(useCase.GenerateOnce(new TunnelGenerationRequestDto { Seed = seed, TunnelCount = 8 }, out var map));
            var world = new GeneratedAcousticWorld(map);
            Assert.That(world.Graph.RoomCount, Is.GreaterThan(map.Tunnels.Count));
            Assert.That(world.Graph.PortalCount, Is.GreaterThan(0));
            foreach (var c in map.NormalCorridors)
            {
                int i = world.FindRoom(new AcousticVector3(c.CenterX, c.CenterY + 1, c.CenterZ));
                Assert.That(i, Is.GreaterThanOrEqualTo(0));
                int edges = 0;
                for (int p = 0; p < world.Graph.PortalCount; p++)
                    if (world.Graph.GetPortal(p).RoomAId == i || world.Graph.GetPortal(p).RoomBId == i) edges++;
                Assert.That(edges, Is.EqualTo(2), "A normal corridor must join both physical tunnel mouths, seed " + seed);
            }
        }

        internal static TunnelMapDto TwoRooms() => new TunnelMapDto
        {
            Dimensions = new TunnelDimensionsDto { TunnelWidth = 4, TunnelHeight = 3, TunnelLength = 6, CorridorWidth = 2 },
            Tunnels = new[] { new TunnelNodeDto { Id = 0 }, new TunnelNodeDto { Id = 1, PositionX = 8 } },
            NormalCorridors = new[] { new NormalCorridorDto { CenterX = 4, DirX = 1, Length = 4 } },
            SmallRooms = Array.Empty<SmallRoomDto>(), WarpCorridors = Array.Empty<WarpCorridorDto>()
        };

        [Test]
        public void ClosedPortalAndOutsidePositionsAreSilent()
        {
            var world = new GeneratedAcousticWorld(TwoRooms());
            var a = new AcousticVector3(0, 1, 0); var b = new AcousticVector3(8, 1, 0); var right = new AcousticVector3(1, 0, 0);
            Assert.That(world.Calculate(a, b, right).Direct.Amplitude.Mid, Is.GreaterThan(0));
            world.SetPortalOpening(0, 0);
            Assert.That(world.Calculate(a, b, right).Direct.Amplitude.Mid, Is.Zero);
            world.SetPortalOpening(0, 1);
            Assert.That(world.Calculate(a, b, right).Direct.Amplitude.Mid, Is.GreaterThan(0));
            Assert.That(world.Calculate(new AcousticVector3(100, 1, 0), b, right).Direct.Amplitude.Mid, Is.Zero);
        }

        [Test]
        public void PhysicalDoorInsideACellFollowsOpeningWithoutAFalseWarpConnection()
        {
            var world = new GeneratedAcousticWorld(TwoRooms());
            var source = new AcousticVector3(0, 1, -2); var listener = new AcousticVector3(0, 1, 2);
            var right = new AcousticVector3(1, 0, 0);
            var normal = new AcousticVector3(0, 0, 1); var centre = new AcousticVector3(0, 1.5f, 0);
            float open = world.Calculate(source, listener, right).Direct.Amplitude.Mid;
            world.SetDoors(new[] { new AcousticDoorState(centre, normal, 4, 3, .25f) });
            Assert.That(world.Calculate(source, listener, right).Direct.Amplitude.Mid, Is.EqualTo(open * .5f).Within(1e-6));
            world.SetDoors(new[] { new AcousticDoorState(centre, normal, 4, 3, 0) });
            Assert.That(world.Calculate(source, listener, right).Direct.Amplitude.Mid, Is.Zero);
            world.SetDoors(new[] { new AcousticDoorState(centre, normal, 4, 3, 1) });
            Assert.That(world.Calculate(source, listener, right).Direct.Amplitude.Mid, Is.EqualTo(open));
        }

        [Test]
        public void WarpPairDoesNotCreateAnAcousticTeleport()
        {
            var map = TwoRooms();
            map = new TunnelMapDto { Dimensions = map.Dimensions, Tunnels = map.Tunnels,
                NormalCorridors = Array.Empty<NormalCorridorDto>(), SmallRooms = map.SmallRooms,
                WarpCorridors = new[] {
                    new WarpCorridorDto { CenterX = -3, DirX = -1, Length = 2, PairId = 0, PairedIndex = 1 },
                    new WarpCorridorDto { CenterX = 11, DirX = 1, Length = 2, PairId = 0, PairedIndex = 0 } } };
            var world = new GeneratedAcousticWorld(map);
            Assert.That(world.Calculate(new AcousticVector3(-3, 1, 0), new AcousticVector3(11, 1, 0),
                new AcousticVector3(1, 0, 0)).Direct.Amplitude.Mid, Is.Zero);
        }
    }
}
