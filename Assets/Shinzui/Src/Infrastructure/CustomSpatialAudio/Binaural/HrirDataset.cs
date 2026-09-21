using System;
using System.Collections.Generic;
using System.IO;
using Shinzui.Application.CustomSpatialAudio;

namespace Shinzui.Infrastructure.CustomSpatialAudio.Binaural
{
    /// <summary>
    /// Immutable elevation-ring HRIR grid. All file I/O, resampling and interpolation happen on
    /// the control thread. Interpolation blends paired impulse responses without ear normalization,
    /// preserving measured interaural level/time cues. Sparse grids may cause interpolation colour.
    /// </summary>
    public sealed class HrirDataset
    {
        private readonly HrirMeasurement[][] rings;
        public int SampleRate { get; }
        public int TapCount { get; }
        public int MeasurementCount { get; }
        public float MinimumElevation => rings[0][0].ElevationDegrees;
        public float MaximumElevation => rings[rings.Length - 1][0].ElevationDegrees;

        public HrirDataset(IEnumerable<HrirMeasurement> measurements)
        {
            if (measurements == null) throw new ArgumentNullException(nameof(measurements));
            var sorted = new List<HrirMeasurement>(measurements);
            if (sorted.Count == 0 || sorted.Count > 10000)
                throw new ArgumentException("An HRIR grid requires 1..10000 measurements.", nameof(measurements));
            sorted.Sort((a, b) => a.ElevationDegrees != b.ElevationDegrees ?
                a.ElevationDegrees.CompareTo(b.ElevationDegrees) : a.AzimuthDegrees.CompareTo(b.AzimuthDegrees));
            if (sorted[0].Filter == null) throw new ArgumentException("Uninitialized measurement.", nameof(measurements));
            SampleRate = sorted[0].Filter.SampleRate;
            TapCount = sorted[0].Filter.TapCount;
            MeasurementCount = sorted.Count;
            var grouped = new List<HrirMeasurement[]>();
            var ring = new List<HrirMeasurement>();
            for (int i = 0; i < sorted.Count; i++)
            {
                HrirMeasurement value = sorted[i];
                if (value.Filter == null || value.Filter.SampleRate != SampleRate || value.Filter.TapCount != TapCount)
                    throw new ArgumentException("All HRIRs must use one sample rate and tap count.", nameof(measurements));
                if (ring.Count > 0 && ring[0].ElevationDegrees != value.ElevationDegrees)
                {
                    grouped.Add(ring.ToArray());
                    ring.Clear();
                }
                if (ring.Count > 0 && ring[ring.Count - 1].AzimuthDegrees == value.AzimuthDegrees)
                    throw new ArgumentException("Duplicate HRIR direction.", nameof(measurements));
                ring.Add(value);
            }
            grouped.Add(ring.ToArray());
            rings = grouped.ToArray();
        }

        /// <summary>Loads the official MIT compact stereo WAV ZIP, mirrors its measured hemisphere, then resamples once.</summary>
        public static HrirDataset LoadMitKemarCompact(string zipPath, int outputSampleRate)
        {
            using (var stream = File.OpenRead(zipPath))
                return LoadMitKemarCompact(stream, outputSampleRate);
        }

        /// <summary>The caller retains ownership of the input stream.</summary>
        public static HrirDataset LoadMitKemarCompact(Stream zipStream, int outputSampleRate) =>
            MitKemarHrirLoader.Load(zipStream, outputSampleRate);

        /// <summary>
        /// Uses world arrival vector pointing from listener toward source. Listener axes are
        /// normalized/orthogonalized; local +X is right, +Y up, +Z front. A coincident source uses
        /// front. Elevation outside the measured range clamps to its nearest ring.
        /// </summary>
        public HrirFilter Interpolate(AcousticVector3 worldArrivalDirection,
            AcousticVector3 listenerForward, AcousticVector3 listenerUp)
        {
            AcousticVector3 forward = Normalize(listenerForward, nameof(listenerForward));
            AcousticVector3 right = Normalize(Cross(Normalize(listenerUp, nameof(listenerUp)), forward), nameof(listenerUp));
            AcousticVector3 up = Cross(forward, right);
            if (!worldArrivalDirection.IsFinite) throw new ArgumentOutOfRangeException(nameof(worldArrivalDirection));
            double length = Math.Sqrt(Dot(worldArrivalDirection, worldArrivalDirection));
            if (length < 1e-12) return InterpolateAngles(0f, 0f);
            double x = Dot(worldArrivalDirection, right) / length;
            double y = Math.Max(-1.0, Math.Min(1.0, Dot(worldArrivalDirection, up) / length));
            double z = Dot(worldArrivalDirection, forward) / length;
            return InterpolateAngles((float)(Math.Atan2(x, z) * 180.0 / Math.PI),
                (float)(Math.Asin(y) * 180.0 / Math.PI));
        }

        public HrirFilter InterpolateAngles(float azimuthDegrees, float elevationDegrees)
        {
            if (float.IsNaN(azimuthDegrees) || float.IsInfinity(azimuthDegrees) ||
                float.IsNaN(elevationDegrees) || float.IsInfinity(elevationDegrees))
                throw new ArgumentOutOfRangeException(nameof(azimuthDegrees));
            float azimuth = ((azimuthDegrees % 360f) + 360f) % 360f;
            int upper = 0;
            while (upper < rings.Length - 1 && rings[upper][0].ElevationDegrees < elevationDegrees) upper++;
            int lower = upper == 0 || elevationDegrees >= MaximumElevation ? upper : upper - 1;
            float elevationBlend = lower == upper ? 0f : Math.Max(0f, Math.Min(1f,
                (elevationDegrees - rings[lower][0].ElevationDegrees) /
                (rings[upper][0].ElevationDegrees - rings[lower][0].ElevationDegrees)));
            SelectAzimuth(rings[lower], azimuth, out HrirFilter a, out HrirFilter b, out float ab);
            SelectAzimuth(rings[upper], azimuth, out HrirFilter c, out HrirFilter d, out float cd);
            if (elevationBlend == 0f && ab == 0f) return a;
            if (elevationBlend == 1f && cd == 0f) return c;
            var left = new float[TapCount];
            var right = new float[TapCount];
            for (int i = 0; i < TapCount; i++)
            {
                float lowLeft = a.GetLeft(i) + (b.GetLeft(i) - a.GetLeft(i)) * ab;
                float highLeft = c.GetLeft(i) + (d.GetLeft(i) - c.GetLeft(i)) * cd;
                float lowRight = a.GetRight(i) + (b.GetRight(i) - a.GetRight(i)) * ab;
                float highRight = c.GetRight(i) + (d.GetRight(i) - c.GetRight(i)) * cd;
                left[i] = lowLeft + (highLeft - lowLeft) * elevationBlend;
                right[i] = lowRight + (highRight - lowRight) * elevationBlend;
            }
            return new HrirFilter(SampleRate, left, right);
        }

        private static void SelectAzimuth(HrirMeasurement[] ring, float azimuth,
            out HrirFilter a, out HrirFilter b, out float blend)
        {
            int upper = 0;
            while (upper < ring.Length && ring[upper].AzimuthDegrees < azimuth) upper++;
            if (upper < ring.Length && ring[upper].AzimuthDegrees == azimuth)
            {
                a = b = ring[upper].Filter;
                blend = 0f;
                return;
            }
            int lower = upper == 0 ? ring.Length - 1 : upper - 1;
            if (upper == ring.Length) upper = 0;
            float lowAngle = ring[lower].AzimuthDegrees;
            float highAngle = ring[upper].AzimuthDegrees;
            if (highAngle <= lowAngle) highAngle += 360f;
            if (azimuth < lowAngle) azimuth += 360f;
            a = ring[lower].Filter;
            b = ring[upper].Filter;
            blend = lower == upper ? 0f : (azimuth - lowAngle) / (highAngle - lowAngle);
        }

        private static AcousticVector3 Normalize(AcousticVector3 vector, string parameter)
        {
            double length = Math.Sqrt(Dot(vector, vector));
            if (!vector.IsFinite || length < 1e-10) throw new ArgumentException("Listener axes must be finite and independent.", parameter);
            return new AcousticVector3((float)(vector.X / length), (float)(vector.Y / length), (float)(vector.Z / length));
        }

        private static double Dot(AcousticVector3 a, AcousticVector3 b) =>
            (double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z;

        private static AcousticVector3 Cross(AcousticVector3 a, AcousticVector3 b) => new AcousticVector3(
            a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    }
}
