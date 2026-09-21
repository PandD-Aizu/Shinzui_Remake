using System;
using Shinzui.Application.CustomSpatialAudio;

namespace Shinzui.Infrastructure.CustomSpatialAudio
{
    /// <summary>
    /// Phase A single-room image-source solver. Computes direct and six first-order specular paths.
    /// Walls are axis aligned; no obstacles, inter-room propagation, diffraction, HRTF or transmission.
    /// Run on the control thread, never inside an audio callback.
    /// </summary>
    public static class RectangularRoomAcoustics
    {
        public const float DefaultSpeedOfSound = 343f;
        public const float MinimumRt60Seconds = 0.05f;
        public const float MaximumRt60Seconds = 10f;

        /// <param name="listenerRight">Listener's world-space right axis; normalized internally for stereo pan.</param>
        /// <param name="speedOfSound">Metres per second, bounded to 100..1000 for this prototype.</param>
        /// <param name="referenceDistance">Distance at unit amplitude, 0.01..100 metres; gain is capped at unity.</param>
        public static RoomAcousticResponse Calculate(RectangularAcousticRoom room, AcousticVector3 source,
            AcousticVector3 listener, AcousticVector3 listenerRight,
            float speedOfSound = DefaultSpeedOfSound, float referenceDistance = 1f)
        {
            if (!room.IsValid) throw new ArgumentException("Invalid rectangular room.", nameof(room));
            if (!room.Contains(source)) throw new ArgumentOutOfRangeException(nameof(source), "Source must be inside the room.");
            if (!room.Contains(listener)) throw new ArgumentOutOfRangeException(nameof(listener), "Listener must be inside the room.");
            if (!Finite(speedOfSound) || speedOfSound < 100f || speedOfSound > 1000f)
                throw new ArgumentOutOfRangeException(nameof(speedOfSound));
            if (!Finite(referenceDistance) || referenceDistance < 0.01f || referenceDistance > 100f)
                throw new ArgumentOutOfRangeException(nameof(referenceDistance));

            double rightLength = Math.Sqrt((double)listenerRight.X * listenerRight.X +
                (double)listenerRight.Y * listenerRight.Y + (double)listenerRight.Z * listenerRight.Z);
            if (!listenerRight.IsFinite || rightLength < 0.000001)
                throw new ArgumentException("Listener right axis must be finite and nonzero.", nameof(listenerRight));
            double rightX = listenerRight.X / rightLength;
            double rightY = listenerRight.Y / rightLength;
            double rightZ = listenerRight.Z / rightLength;

            // Work relative to the listener with double intermediates, avoiding image-position float cancellation.
            double dx = (double)source.X - listener.X;
            double dy = (double)source.Y - listener.Y;
            double dz = (double)source.Z - listener.Z;
            var direct = BuildTap(dx, dy, dz, new AcousticBandValues(1f, 1f, 1f),
                rightX, rightY, rightZ, speedOfSound, referenceDistance);
            var negativeX = BuildTap(2.0 * ((double)room.Min.X - listener.X) - dx, dy, dz,
                ReflectionAmplitude(room.NegativeX), rightX, rightY, rightZ, speedOfSound, referenceDistance);
            var positiveX = BuildTap(2.0 * ((double)room.Max.X - listener.X) - dx, dy, dz,
                ReflectionAmplitude(room.PositiveX), rightX, rightY, rightZ, speedOfSound, referenceDistance);
            var negativeY = BuildTap(dx, 2.0 * ((double)room.Min.Y - listener.Y) - dy, dz,
                ReflectionAmplitude(room.NegativeY), rightX, rightY, rightZ, speedOfSound, referenceDistance);
            var positiveY = BuildTap(dx, 2.0 * ((double)room.Max.Y - listener.Y) - dy, dz,
                ReflectionAmplitude(room.PositiveY), rightX, rightY, rightZ, speedOfSound, referenceDistance);
            var negativeZ = BuildTap(dx, dy, 2.0 * ((double)room.Min.Z - listener.Z) - dz,
                ReflectionAmplitude(room.NegativeZ), rightX, rightY, rightZ, speedOfSound, referenceDistance);
            var positiveZ = BuildTap(dx, dy, 2.0 * ((double)room.Max.Z - listener.Z) - dz,
                ReflectionAmplitude(room.PositiveZ), rightX, rightY, rightZ, speedOfSound, referenceDistance);

            double width = (double)room.Max.X - room.Min.X;
            double height = (double)room.Max.Y - room.Min.Y;
            double depth = (double)room.Max.Z - room.Min.Z;
            double yz = height * depth, xz = width * depth, xy = width * height;
            double area = 2.0 * (yz + xz + xy);
            double volume = width * height * depth;
            double lowSurvival = 0.0, midSurvival = 0.0, highSurvival = 0.0;
            for (int wallIndex = 0; wallIndex < RoomAcousticResponse.ReflectionCount; wallIndex++)
            {
                AcousticWall wall = room.GetWall(wallIndex);
                double wallArea = wallIndex < 2 ? yz : wallIndex < 4 ? xz : xy;
                double closedArea = wallArea * (1.0 - wall.OpeningFraction);
                lowSurvival += closedArea * (1.0 - wall.Absorption.Low);
                midSurvival += closedArea * (1.0 - wall.Absorption.Mid);
                highSurvival += closedArea * (1.0 - wall.Absorption.High);
            }
            lowSurvival = Clamp01(lowSurvival / area);
            midSurvival = Clamp01(midSurvival / area);
            highSurvival = Clamp01(highSurvival / area);
            var rt60 = new AcousticBandValues(EstimateRt60(lowSurvival, volume, area, speedOfSound),
                EstimateRt60(midSurvival, volume, area, speedOfSound),
                EstimateRt60(highSurvival, volume, area, speedOfSound));
            var lateBandGain = new AcousticBandValues((float)Math.Sqrt(lowSurvival),
                (float)Math.Sqrt(midSurvival), (float)Math.Sqrt(highSurvival));
            return new RoomAcousticResponse(direct, negativeX, positiveX, negativeY, positiveY,
                negativeZ, positiveZ, rt60, lateBandGain);
        }

        private static AcousticPathTap BuildTap(double x, double y, double z, AcousticBandValues reflection,
            double rightX, double rightY, double rightZ, float soundSpeed, float referenceDistance)
        {
            double distance = Math.Sqrt(x * x + y * y + z * z);
            double inverseDistance = distance > 0.0 ? 1.0 / distance : 0.0;
            var direction = new AcousticVector3((float)(x * inverseDistance), (float)(y * inverseDistance),
                (float)(z * inverseDistance));
            float attenuation = (float)(referenceDistance / Math.Max(referenceDistance, distance));
            var amplitude = new AcousticBandValues(reflection.Low * attenuation,
                reflection.Mid * attenuation, reflection.High * attenuation);
            float pan = (float)Math.Max(-1.0, Math.Min(1.0, (x * rightX + y * rightY + z * rightZ) * inverseDistance));
            return new AcousticPathTap((float)(distance / soundSpeed), (float)distance, amplitude, direction, pan);
        }

        private static AcousticBandValues ReflectionAmplitude(AcousticWall wall)
        {
            double closedFraction = 1.0 - wall.OpeningFraction;
            return new AcousticBandValues((float)Math.Sqrt((1.0 - wall.Absorption.Low) * closedFraction),
                (float)Math.Sqrt((1.0 - wall.Absorption.Mid) * closedFraction),
                (float)Math.Sqrt((1.0 - wall.Absorption.High) * closedFraction));
        }

        // Eyring diffuse-field approximation: 24 ln(10) V / (-c S ln(mean energy survival)).
        // At small absorption this approaches Sabine. This is not an accurate long-tunnel/open-field model.
        // A perfectly reflective room is capped rather than advertised as a finite physical decay.
        // A fully absorbing/open room uses the minimum RT60 and zero LateGain, so it creates no new tail.
        private static float EstimateRt60(double survival, double volume, double area, float soundSpeed)
        {
            if (survival <= 0.0) return MinimumRt60Seconds;
            if (survival >= 1.0) return MaximumRt60Seconds;
            double estimate = 24.0 * Math.Log(10.0) * volume / (-soundSpeed * area * Math.Log(survival));
            return (float)Math.Max(MinimumRt60Seconds, Math.Min(MaximumRt60Seconds, estimate));
        }

        private static double Clamp01(double value) => Math.Max(0.0, Math.Min(1.0, value));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
