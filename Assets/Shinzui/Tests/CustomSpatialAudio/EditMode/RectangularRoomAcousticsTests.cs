using System;
using NUnit.Framework;
using Shinzui.Application.CustomSpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio;

namespace Shinzui.Tests.CustomSpatialAudio
{
    public sealed class RectangularRoomAcousticsTests
    {
        private static readonly AcousticVector3 Right = new AcousticVector3(1f, 0f, 0f);
        private static readonly AcousticWall Reflective = new AcousticWall(new AcousticBandValues(0f, 0f, 0f));

        [Test]
        public void CentredSourceAndListener_HaveSixAnalyticalFirstOrderPaths()
        {
            var room = new RectangularAcousticRoom(new AcousticVector3(-5f, -3f, -4f),
                new AcousticVector3(5f, 3f, 4f), Reflective);
            RoomAcousticResponse response = RectangularRoomAcoustics.Calculate(room, default, default, Right);

            Assert.That(response.Direct.DelaySeconds, Is.Zero);
            Assert.That(response.Direct.Amplitude.Mid, Is.EqualTo(1f));
            Assert.That(response.Direct.Pan, Is.Zero);
            float[] distances = { 10f, 10f, 6f, 6f, 8f, 8f };
            var directions = new[]
            {
                new AcousticVector3(-1f, 0f, 0f), new AcousticVector3(1f, 0f, 0f),
                new AcousticVector3(0f, -1f, 0f), new AcousticVector3(0f, 1f, 0f),
                new AcousticVector3(0f, 0f, -1f), new AcousticVector3(0f, 0f, 1f)
            };
            for (int index = 0; index < RoomAcousticResponse.ReflectionCount; index++)
            {
                AcousticPathTap tap = response.GetReflection(index);
                Assert.That(tap.DistanceMetres, Is.EqualTo(distances[index]).Within(0.00001f));
                Assert.That(tap.DelaySeconds, Is.EqualTo(distances[index] / 343f).Within(0.000001f));
                Assert.That(tap.Amplitude.Mid, Is.EqualTo(1f / distances[index]).Within(0.000001f));
                Assert.That(tap.Direction.X, Is.EqualTo(directions[index].X).Within(0.000001f));
                Assert.That(tap.Direction.Y, Is.EqualTo(directions[index].Y).Within(0.000001f));
                Assert.That(tap.Direction.Z, Is.EqualTo(directions[index].Z).Within(0.000001f));
                Assert.That(tap.Pan, Is.EqualTo(directions[index].X).Within(0.000001f));
                Assert.That(response.GetPath(index + 1).DelaySeconds, Is.EqualTo(tap.DelaySeconds));
            }
        }

        [Test]
        public void OffCentrePath_UsesImageDistanceAndSquareRootOfEnergySurvival()
        {
            var wall = new AcousticWall(new AcousticBandValues(0.75f, 0f, 1f));
            var room = new RectangularAcousticRoom(new AcousticVector3(-5f, -5f, -5f),
                new AcousticVector3(5f, 5f, 5f), wall);
            RoomAcousticResponse response = RectangularRoomAcoustics.Calculate(room,
                new AcousticVector3(1f, 0f, 0f), new AcousticVector3(-1f, 0f, 0f), Right);

            Assert.That(response.Direct.DistanceMetres, Is.EqualTo(2f));
            Assert.That(response.Direct.Amplitude.Low, Is.EqualTo(0.5f));
            Assert.That(response.Direct.Pan, Is.EqualTo(1f));
            AcousticPathTap wallPath = response.GetReflection(0);
            Assert.That(wallPath.DistanceMetres, Is.EqualTo(10f));
            Assert.That(wallPath.Amplitude.Low, Is.EqualTo(0.05f).Within(0.000001f));
            Assert.That(wallPath.Amplitude.Mid, Is.EqualTo(0.1f).Within(0.000001f));
            Assert.That(wallPath.Amplitude.High, Is.Zero);
            Assert.That(wallPath.Direction.X, Is.EqualTo(-1f));

            AcousticPathTap floorPath = response.GetReflection(2);
            float floorDistance = (float)Math.Sqrt(104.0);
            Assert.That(floorPath.DistanceMetres, Is.EqualTo(floorDistance).Within(0.00001f));
            Assert.That(floorPath.Direction.X, Is.EqualTo(2f / floorDistance).Within(0.000001f));
            Assert.That(floorPath.Direction.Y, Is.EqualTo(-10f / floorDistance).Within(0.000001f));
        }

        [Test]
        public void RotationChangesPan_WithoutChangingWorldPathOrTravelTime()
        {
            var room = Cube(10f, Reflective);
            var source = new AcousticVector3(2f, 0f, 1f);
            var a = RectangularRoomAcoustics.Calculate(room, source, default, Right);
            var b = RectangularRoomAcoustics.Calculate(room, source, default, new AcousticVector3(-2f, 0f, 0f));
            for (int index = 0; index < RoomAcousticResponse.PathCount; index++)
            {
                Assert.That(b.GetPath(index).Pan, Is.EqualTo(-a.GetPath(index).Pan).Within(0.000001f));
                Assert.That(b.GetPath(index).DelaySeconds, Is.EqualTo(a.GetPath(index).DelaySeconds));
                Assert.That(b.GetPath(index).Direction.X, Is.EqualTo(a.GetPath(index).Direction.X));
            }
        }

        [Test]
        public void AllOpenBoundaries_ProduceNoNewEarlyOrLateWetEnergy()
        {
            var wall = new AcousticWall(new AcousticBandValues(0.2f, 0.3f, 0.4f), 1f);
            RoomAcousticResponse response = RectangularRoomAcoustics.Calculate(Cube(10f, wall),
                new AcousticVector3(2f, 0f, 0f), default, Right);
            Assert.That(response.Direct.Amplitude.Mid, Is.EqualTo(0.5f));
            Assert.That(response.LateGain, Is.Zero);
            Assert.That(response.LateBandGain.Low, Is.Zero);
            Assert.That(response.LateBandGain.Mid, Is.Zero);
            Assert.That(response.LateBandGain.High, Is.Zero);
            for (int index = 0; index < RoomAcousticResponse.ReflectionCount; index++)
            {
                Assert.That(response.GetReflection(index).Amplitude.Low, Is.Zero);
                Assert.That(response.GetReflection(index).Amplitude.Mid, Is.Zero);
                Assert.That(response.GetReflection(index).Amplitude.High, Is.Zero);
            }
        }

        [Test]
        public void FullyAbsorbedBand_DoesNotExciteLateReturnWhileOtherBandsSurvive()
        {
            var wall = new AcousticWall(new AcousticBandValues(0f, 0.75f, 1f));
            RoomAcousticResponse response = RectangularRoomAcoustics.Calculate(Cube(10f, wall),
                default, default, Right);
            Assert.That(response.LateBandGain.Low, Is.EqualTo(1f));
            Assert.That(response.LateBandGain.Mid, Is.EqualTo(0.5f));
            Assert.That(response.LateBandGain.High, Is.Zero);
            Assert.That(response.LateGain, Is.EqualTo(Math.Sqrt(1.25 / 3.0)).Within(0.000001));
            Assert.That(response.Rt60Seconds.High, Is.EqualTo(RectangularRoomAcoustics.MinimumRt60Seconds));
        }

        [Test]
        public void OneWallOpening_LosesEnergyOnlyOnItsFirstOrderPathAndShortensTail()
        {
            var room = Cube(10f, Reflective);
            var halfOpen = new AcousticWall(new AcousticBandValues(0f, 0f, 0f), 0.5f);
            var opened = new RectangularAcousticRoom(room.Min, room.Max, halfOpen,
                Reflective, Reflective, Reflective, Reflective, Reflective);
            var closedResponse = RectangularRoomAcoustics.Calculate(room, default, default, Right);
            var openResponse = RectangularRoomAcoustics.Calculate(opened, default, default, Right);

            Assert.That(openResponse.GetReflection(0).Amplitude.Mid,
                Is.EqualTo(0.1f * (float)Math.Sqrt(0.5)).Within(0.000001f));
            for (int index = 1; index < RoomAcousticResponse.ReflectionCount; index++)
                Assert.That(openResponse.GetReflection(index).Amplitude.Mid,
                    Is.EqualTo(closedResponse.GetReflection(index).Amplitude.Mid));
            Assert.That(openResponse.Rt60Seconds.Mid, Is.LessThan(closedResponse.Rt60Seconds.Mid));
            Assert.That(openResponse.LateGain, Is.LessThan(closedResponse.LateGain));
        }

        [Test]
        public void Rt60_MatchesEyringEstimateForUniformCube_AndRespondsToScaleAndAbsorption()
        {
            var wall = new AcousticWall(new AcousticBandValues(0.1f, 0.2f, 0.4f));
            var response = RectangularRoomAcoustics.Calculate(Cube(10f, wall), default, default, Right);
            double expectedMid = 24.0 * Math.Log(10.0) * 1000.0 / (-343.0 * 600.0 * Math.Log(0.8));
            Assert.That(response.Rt60Seconds.Mid, Is.EqualTo(expectedMid).Within(0.00001));
            Assert.That(response.Rt60Seconds.Low, Is.GreaterThan(response.Rt60Seconds.Mid));
            Assert.That(response.Rt60Seconds.Mid, Is.GreaterThan(response.Rt60Seconds.High));
            var larger = RectangularRoomAcoustics.Calculate(Cube(20f, wall), default, default, Right);
            Assert.That(larger.Rt60Seconds.Mid, Is.EqualTo(2f * response.Rt60Seconds.Mid).Within(0.00001f));
        }

        [Test]
        public void ReflectiveAndAbsorbingExtremes_HaveExplicitBoundedDecayParameters()
        {
            var reflective = RectangularRoomAcoustics.Calculate(Cube(10f, Reflective), default, default, Right);
            var absorbingWall = new AcousticWall(new AcousticBandValues(1f, 1f, 1f));
            var absorbing = RectangularRoomAcoustics.Calculate(Cube(10f, absorbingWall), default, default, Right);
            Assert.That(reflective.Rt60Seconds.Mid, Is.EqualTo(RectangularRoomAcoustics.MaximumRt60Seconds));
            Assert.That(absorbing.Rt60Seconds.Mid, Is.EqualTo(RectangularRoomAcoustics.MinimumRt60Seconds));
            Assert.That(absorbing.LateGain, Is.Zero);
        }

        [Test]
        public void LargestRoomAndSlowestSound_FitThreeSecondDelayBudget()
        {
            var room = new RectangularAcousticRoom(default, new AcousticVector3(100f, 100f, 100f), Reflective);
            var response = RectangularRoomAcoustics.Calculate(room, new AcousticVector3(100f, 0f, 0f),
                new AcousticVector3(100f, 100f, 100f), Right, 100f);
            Assert.That(response.GetReflection(0).DelaySeconds,
                Is.EqualTo(Math.Sqrt(60000.0) / 100.0).Within(0.000001));
            for (int index = 0; index < RoomAcousticResponse.PathCount; index++)
                Assert.That(response.GetPath(index).DelaySeconds, Is.InRange(0f, 3f));
        }

        [Test]
        public void InvalidNumericInputsAndBounds_AreRejectedBeforeDsp()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AcousticVector3(float.NaN, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AcousticBandValues(0f, float.PositiveInfinity, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AcousticWall(new AcousticBandValues(1.1f, 0f, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AcousticWall(default, -0.1f));
            Assert.Throws<ArgumentException>(() => new RectangularAcousticRoom(default, default, Reflective));
            Assert.Throws<ArgumentException>(() => Cube(101f, Reflective));
            Assert.Throws<ArgumentException>(() => RectangularRoomAcoustics.Calculate(default, default, default, Right));
            var room = Cube(10f, Reflective);
            Assert.Throws<ArgumentOutOfRangeException>(() => RectangularRoomAcoustics.Calculate(room,
                new AcousticVector3(6f, 0f, 0f), default, Right));
            Assert.Throws<ArgumentOutOfRangeException>(() => RectangularRoomAcoustics.Calculate(room,
                default, new AcousticVector3(0f, -6f, 0f), Right));
            Assert.Throws<ArgumentException>(() => RectangularRoomAcoustics.Calculate(room, default, default, default));
            Assert.Throws<ArgumentOutOfRangeException>(() => RectangularRoomAcoustics.Calculate(room,
                default, default, Right, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => RectangularRoomAcoustics.Calculate(room,
                default, default, Right, 99f));
            Assert.Throws<ArgumentOutOfRangeException>(() => RectangularRoomAcoustics.Calculate(room,
                default, default, Right, 343f, 0f));
        }

        private static RectangularAcousticRoom Cube(float edge, AcousticWall wall)
        {
            float half = edge * 0.5f;
            return new RectangularAcousticRoom(new AcousticVector3(-half, -half, -half),
                new AcousticVector3(half, half, half), wall);
        }
    }
}
