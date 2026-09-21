using System;
using NUnit.Framework;
using Shinzui.Application.CustomSpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio.Dsp;

namespace Shinzui.Tests.CustomSpatialAudio
{
    public sealed class PortalRoomAcousticsTests
    {
        private static readonly AcousticVector3 Right = new AcousticVector3(1f, 0f, 0f);
        private static readonly AcousticBandValues UnityTransmission = new AcousticBandValues(1f, 1f, 1f);

        [Test]
        public void SameConvexCell_UsesDirectSegmentAndDoesNotInventReflectionsOrTail()
        {
            var graph = new PortalAcousticGraph(new[] { Cell(0, 0f, 4f, 0f, 4f) }, Array.Empty<AcousticPortal>());
            PortalAcousticPath path = PortalRoomAcoustics.Calculate(graph, 0, Point(1f, 1f), 0, Point(3f, 1f), Right);
            Assert.That(path.IsReachable, Is.True);
            Assert.That(path.PortalCount, Is.Zero);
            Assert.That(path.Tap.DistanceMetres, Is.EqualTo(2f));
            Assert.That(path.Tap.Amplitude.Mid, Is.EqualTo(.5f));
            Assert.That(path.Tap.Pan, Is.EqualTo(-1f));
            RoomAcousticResponse response = path.ToRoomResponse();
            Assert.That(response.LateGain, Is.Zero);
            for (int i = 0; i < RoomAcousticResponse.ReflectionCount; i++)
                Assert.That(response.GetReflection(i).Amplitude.Mid, Is.Zero);
            Assert.DoesNotThrow(() => SpatialDspParameters.Create(response, 48000));
        }

        [Test]
        public void LBend_AccumulatesAllSegmentsAndArrivesFromLastOpening()
        {
            PortalAcousticPath path = PortalRoomAcoustics.Calculate(Bend(1f), 0, Point(2f, 2f),
                2, Point(6f, 6f), Right);
            double distance = 4.0 + Math.Sqrt(8.0);
            Assert.That(path.IsReachable, Is.True);
            Assert.That(path.PortalCount, Is.EqualTo(2));
            Assert.That(path.GetPortalId(0), Is.EqualTo(10));
            Assert.That(path.GetPortalId(1), Is.EqualTo(20));
            Assert.That(path.Tap.DistanceMetres, Is.EqualTo(distance).Within(.000001));
            Assert.That(path.Tap.DistanceMetres, Is.GreaterThan(Math.Sqrt(32.0)));
            Assert.That(path.Tap.DelaySeconds, Is.EqualTo(distance / 343.0).Within(.0000001));
            Assert.That(path.Tap.Amplitude.Mid, Is.EqualTo(1.0 / distance).Within(.0000001));
            Assert.That(path.Tap.Direction.X, Is.Zero);
            Assert.That(path.Tap.Direction.Y, Is.Zero);
            Assert.That(path.Tap.Direction.Z, Is.EqualTo(-1f));
            Assert.That(path.Tap.Pan, Is.Zero);
        }

        [Test]
        public void RotationChangesPan_WhileRouteAndTravelTimeRemainStable()
        {
            var graph = Bend(1f);
            var a = PortalRoomAcoustics.Calculate(graph, 0, Point(2f, 2f), 2, Point(6f, 6f), Right);
            var b = PortalRoomAcoustics.Calculate(graph, 0, Point(2f, 2f), 2, Point(6f, 6f),
                new AcousticVector3(0f, 0f, 2f));
            Assert.That(b.Tap.Pan, Is.EqualTo(-1f));
            Assert.That(b.Tap.DistanceMetres, Is.EqualTo(a.Tap.DistanceMetres));
            Assert.That(b.Tap.Direction.Z, Is.EqualTo(a.Tap.Direction.Z));
            Assert.That(b.GetPortalId(1), Is.EqualTo(a.GetPortalId(1)));
        }

        [Test]
        public void DoorOpeningAndBandTransmission_MultiplyAmplitudeWithoutChangingTravelTime()
        {
            var rooms = new[] { Cell(0, 0f, 4f, 0f, 4f), Cell(1, 4f, 8f, 0f, 4f) };
            var portal = new AcousticPortal(10, 0, 1, Point(4f, 2f), .25f,
                new AcousticBandValues(1f, .25f, 0f));
            var graph = new PortalAcousticGraph(rooms, new[] { portal });
            var path = PortalRoomAcoustics.Calculate(graph, 0, Point(2f, 2f), 1, Point(6f, 2f), Right);
            Assert.That(path.Tap.DistanceMetres, Is.EqualTo(4f));
            Assert.That(path.Tap.DelaySeconds, Is.EqualTo(4f / 343f));
            Assert.That(path.Tap.Amplitude.Low, Is.EqualTo(.125f));
            Assert.That(path.Tap.Amplitude.Mid, Is.EqualTo(.0625f));
            Assert.That(path.Tap.Amplitude.High, Is.Zero);
        }

        [Test]
        public void ClosedDoor_DisconnectedCellsAndZeroTransmission_AreSilent()
        {
            var rooms = new[] { Cell(0, 0f, 4f, 0f, 4f), Cell(1, 4f, 8f, 0f, 4f) };
            AcousticPortal[][] cases =
            {
                Array.Empty<AcousticPortal>(),
                new[] { new AcousticPortal(10, 0, 1, Point(4f, 2f), 0f, UnityTransmission) },
                new[] { new AcousticPortal(10, 0, 1, Point(4f, 2f), 1f, default) }
            };
            foreach (AcousticPortal[] portals in cases)
            {
                var path = PortalRoomAcoustics.Calculate(new PortalAcousticGraph(rooms, portals),
                    0, Point(2f, 2f), 1, Point(6f, 2f), Right);
                Assert.That(path.IsReachable, Is.False);
                Assert.That(path.PortalCount, Is.Zero);
                Assert.That(path.Tap.Amplitude.Low, Is.Zero);
                Assert.That(path.Tap.Amplitude.Mid, Is.Zero);
                Assert.That(path.Tap.Amplitude.High, Is.Zero);
                Assert.That(path.ToRoomResponse().LateGain, Is.Zero);
            }
            Assert.That(PortalRoomAcoustics.Calculate(Bend(0f), 0, Point(2f, 2f),
                2, Point(6f, 6f), Right).IsReachable, Is.False);
        }

        [Test]
        public void SearchIncludesFinalSegment_NotJustCheapestEntryIntoListenerCell()
        {
            var graph = TwoDoors(1f);
            var path = PortalRoomAcoustics.Calculate(graph, 0, Point(1f, 1f), 1, Point(11f, 9f), Right,
                transmissionLossWeightMetres: 0f);
            Assert.That(path.PortalCount, Is.EqualTo(1));
            Assert.That(path.GetPortalId(0), Is.EqualTo(20));
            Assert.That(path.Tap.DistanceMetres, Is.EqualTo(Math.Sqrt(145.0) + 1.0).Within(.000001));
        }

        [Test]
        public void LossWeightCanPreferLongerOpenRoute_AndZeroWeightFindsShortestGeometry()
        {
            var graph = TwoDoors(.001f);
            var geometric = PortalRoomAcoustics.Calculate(graph, 0, Point(1f, 1f), 1, Point(19f, 1f), Right,
                transmissionLossWeightMetres: 0f);
            var weighted = PortalRoomAcoustics.Calculate(graph, 0, Point(1f, 1f), 1, Point(19f, 1f), Right);
            Assert.That(geometric.GetPortalId(0), Is.EqualTo(10));
            Assert.That(weighted.GetPortalId(0), Is.EqualTo(20));
            Assert.That(weighted.Tap.DistanceMetres, Is.EqualTo(2.0 * Math.Sqrt(145.0)).Within(.000002));
            Assert.That(weighted.Tap.Amplitude.Mid, Is.GreaterThan(geometric.Tap.Amplitude.Mid));
        }

        [Test]
        public void EqualCostRoutes_AreDeterministicRegardlessOfInputOrder()
        {
            var rooms = new[] { Cell(1, 4f, 8f, 0f, 4f), Cell(0, 0f, 4f, 0f, 4f) };
            var portals = new[]
            {
                new AcousticPortal(20, 0, 1, Point(4f, 2f), 1f, UnityTransmission),
                new AcousticPortal(10, 0, 1, Point(4f, 2f), 1f, UnityTransmission)
            };
            var a = new PortalAcousticGraph(rooms, portals);
            Array.Reverse(rooms);
            Array.Reverse(portals);
            var b = new PortalAcousticGraph(rooms, portals);
            foreach (var graph in new[] { a, b })
                Assert.That(PortalRoomAcoustics.Calculate(graph, 0, Point(2f, 2f),
                    1, Point(6f, 2f), Right).GetPortalId(0), Is.EqualTo(10));
        }

        [Test]
        public void ListenerExactlyOnOpening_UsesIncidentSegmentAndReverseRouteIsReciprocal()
        {
            var graph = Bend(1f);
            var atDoor = PortalRoomAcoustics.Calculate(graph, 0, Point(2f, 2f), 2, Point(6f, 4f), Right);
            Assert.That(atDoor.Tap.Direction.X, Is.EqualTo(-1.0 / Math.Sqrt(2.0)).Within(.000001));
            Assert.That(atDoor.Tap.Direction.Z, Is.EqualTo(-1.0 / Math.Sqrt(2.0)).Within(.000001));
            var forward = PortalRoomAcoustics.Calculate(graph, 0, Point(2f, 2f), 2, Point(6f, 6f), Right);
            var reverse = PortalRoomAcoustics.Calculate(graph, 2, Point(6f, 6f), 0, Point(2f, 2f), Right);
            Assert.That(reverse.Tap.DistanceMetres, Is.EqualTo(forward.Tap.DistanceMetres));
            Assert.That(reverse.Tap.Amplitude.Mid, Is.EqualTo(forward.Tap.Amplitude.Mid));
            Assert.That(reverse.GetPortalId(0), Is.EqualTo(20));
            Assert.That(reverse.GetPortalId(1), Is.EqualTo(10));
            Assert.That(reverse.Tap.Direction.X, Is.EqualTo(1f));
        }

        [Test]
        public void GraphAndPath_CopyCallerArraysAndPreserveDoorSnapshot()
        {
            var rooms = new[] { Cell(0, 0f, 4f, 0f, 4f), Cell(1, 4f, 8f, 0f, 4f) };
            var portals = new[] { new AcousticPortal(10, 0, 1, Point(4f, 2f), 1f, UnityTransmission) };
            var graph = new PortalAcousticGraph(rooms, portals);
            rooms[0] = default;
            portals[0] = new AcousticPortal(10, 0, 1, Point(4f, 2f), 0f, UnityTransmission);
            var path = PortalRoomAcoustics.Calculate(graph, 0, Point(2f, 2f), 1, Point(6f, 2f), Right);
            Assert.That(path.IsReachable, Is.True);
            Assert.That(path.Tap.Amplitude.Mid, Is.EqualTo(.25f));
            int[] ids = { 10 };
            var copy = new PortalAcousticPath(true, path.Tap, ids);
            ids[0] = 999;
            Assert.That(copy.GetPortalId(0), Is.EqualTo(10));
        }

        [Test]
        public void InvalidGraphGeometryAndIdentifiers_AreRejected()
        {
            var a = Cell(0, 0f, 4f, 0f, 4f);
            var b = Cell(1, 4f, 8f, 0f, 4f);
            Assert.Throws<ArgumentOutOfRangeException>(() => new PortalAcousticGraph(Array.Empty<PortalAcousticRoom>(), Array.Empty<AcousticPortal>()));
            Assert.Throws<ArgumentException>(() => new PortalAcousticGraph(new[] { a, a }, Array.Empty<AcousticPortal>()));
            Assert.Throws<ArgumentException>(() => new PortalAcousticGraph(new[] { a, Cell(1, 3f, 8f, 0f, 4f) }, Array.Empty<AcousticPortal>()));
            Assert.Throws<ArgumentException>(() => new PortalAcousticGraph(new[] { a, b }, new[] { default(AcousticPortal) }));
            Assert.Throws<ArgumentException>(() => new PortalAcousticGraph(new[] { a, b }, new[] { new AcousticPortal(10, 0, 7, Point(4f, 2f), 1f, UnityTransmission) }));
            Assert.Throws<ArgumentException>(() => new PortalAcousticGraph(new[] { a, b }, new[] { new AcousticPortal(10, 0, 1, Point(3f, 2f), 1f, UnityTransmission) }));
            Assert.Throws<ArgumentException>(() => new PortalAcousticGraph(new[] { a, b }, new[] { new AcousticPortal(10, 0, 1, Point(4f, 0f), 1f, UnityTransmission) }));
            Assert.Throws<ArgumentException>(() => new PortalAcousticGraph(new[] { a, Cell(1, 5f, 9f, 0f, 4f) }, new[] { new AcousticPortal(10, 0, 1, Point(4f, 2f), 1f, UnityTransmission) }));
            var portal = new AcousticPortal(10, 0, 1, Point(4f, 2f), 1f, UnityTransmission);
            Assert.Throws<ArgumentException>(() => new PortalAcousticGraph(new[] { a, b }, new[] { portal, portal }));
        }

        [Test]
        public void InvalidQueryAndDoorValues_AreRejectedBeforeDsp()
        {
            var graph = Bend(1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => new AcousticPortal(1, 0, 1, default, float.NaN, UnityTransmission));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AcousticPortal(1, 0, 1, default, 1f, new AcousticBandValues(1.1f, 1f, 1f)));
            Assert.Throws<ArgumentException>(() => new AcousticPortal(1, 0, 0, default, 1f, UnityTransmission));
            Assert.Throws<ArgumentOutOfRangeException>(() => PortalRoomAcoustics.Calculate(graph, 7, Point(2f, 2f), 2, Point(6f, 6f), Right));
            Assert.Throws<ArgumentOutOfRangeException>(() => PortalRoomAcoustics.Calculate(graph, 0, Point(6f, 6f), 2, Point(6f, 6f), Right));
            Assert.Throws<ArgumentException>(() => PortalRoomAcoustics.Calculate(graph, 0, Point(2f, 2f), 2, Point(6f, 6f), default));
            Assert.Throws<ArgumentOutOfRangeException>(() => PortalRoomAcoustics.Calculate(graph, 0, Point(2f, 2f), 2, Point(6f, 6f), Right, speedOfSound: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => PortalRoomAcoustics.Calculate(graph, 0, Point(2f, 2f), 2, Point(6f, 6f), Right, referenceDistance: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PortalRoomAcoustics.Calculate(graph, 0, Point(2f, 2f), 2, Point(6f, 6f), Right, transmissionLossWeightMetres: -1f));
        }

        [Test]
        public void PathBeyondDspBudget_IsNeverClampedToAnEarlierArrival()
        {
            var rooms = new[]
            {
                Cell(0, 0f, 100f, 0f, 4f), Cell(1, 100f, 200f, 0f, 4f),
                Cell(2, 200f, 300f, 0f, 4f), Cell(3, 300f, 400f, 0f, 4f)
            };
            var portals = new[]
            {
                new AcousticPortal(10, 0, 1, Point(100f, 2f), 1f, UnityTransmission),
                new AcousticPortal(20, 1, 2, Point(200f, 2f), 1f, UnityTransmission),
                new AcousticPortal(30, 2, 3, Point(300f, 2f), 1f, UnityTransmission)
            };
            var path = PortalRoomAcoustics.Calculate(new PortalAcousticGraph(rooms, portals), 0,
                Point(1f, 2f), 3, Point(399f, 2f), Right, speedOfSound: 100f);
            Assert.That(path.Tap.DistanceMetres, Is.EqualTo(398f));
            Assert.That(path.Tap.DelaySeconds, Is.EqualTo(3.98f));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpatialDspParameters.Create(path.ToRoomResponse(), 48000));
        }

        private static PortalAcousticGraph Bend(float secondDoorOpening) => new PortalAcousticGraph(
            new[] { Cell(0, 0f, 4f, 0f, 4f), Cell(1, 4f, 8f, 0f, 4f), Cell(2, 4f, 8f, 4f, 8f) },
            new[]
            {
                new AcousticPortal(10, 0, 1, Point(4f, 2f), 1f, UnityTransmission),
                new AcousticPortal(20, 1, 2, Point(6f, 4f), secondDoorOpening, UnityTransmission)
            });

        private static PortalAcousticGraph TwoDoors(float firstOpening) => new PortalAcousticGraph(
            new[] { Cell(0, 0f, 10f, 0f, 10f), Cell(1, 10f, 20f, 0f, 10f) },
            new[]
            {
                new AcousticPortal(10, 0, 1, Point(10f, 1f), firstOpening, UnityTransmission),
                new AcousticPortal(20, 0, 1, Point(10f, 9f), 1f, UnityTransmission)
            });

        private static AcousticVector3 Point(float x, float z) => new AcousticVector3(x, 1f, z);

        private static PortalAcousticRoom Cell(int id, float minX, float maxX, float minZ, float maxZ) =>
            new PortalAcousticRoom(id, new RectangularAcousticRoom(new AcousticVector3(minX, 0f, minZ),
                new AcousticVector3(maxX, 3f, maxZ), new AcousticWall(new AcousticBandValues(.2f, .3f, .4f))));
    }
}
