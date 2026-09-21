using System;

namespace Shinzui.Infrastructure.CustomSpatialAudio.Binaural
{
    /// <summary>Immutable, paired ear impulse responses. Construct and resample on the control thread.</summary>
    public sealed class HrirFilter
    {
        public const int MaximumTapCount = 512;
        private readonly float[] left, right;

        public int SampleRate { get; }
        public int TapCount => left.Length;

        public HrirFilter(int sampleRate, float[] left, float[] right)
        {
            if (sampleRate < 16000 || sampleRate > 96000)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (left == null || right == null || left.Length == 0 ||
                left.Length != right.Length || left.Length > MaximumTapCount)
                throw new ArgumentException("HRIR ears must contain the same 1..512 samples.");
            ValidateSamples(left);
            ValidateSamples(right);
            SampleRate = sampleRate;
            this.left = (float[])left.Clone();
            this.right = (float[])right.Clone();
        }

        public float GetLeft(int tap) => left[tap];
        public float GetRight(int tap) => right[tap];

        /// <summary>
        /// Windowed-sinc conversion with antialiasing and impulse-response gain compensation.
        /// A rate conversion adds 16/sourceRate seconds of common causal filter latency; ear
        /// time differences remain intact. Zero padding retains the resampling filter tail.
        /// </summary>
        public HrirFilter Resample(int sampleRate)
        {
            if (sampleRate < 16000 || sampleRate > 96000)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (sampleRate == SampleRate) return this;
            const int radius = 16;
            double ratio = (double)sampleRate / SampleRate;
            int count = (int)Math.Ceiling((TapCount + radius * 2) * ratio);
            if (count > MaximumTapCount)
                throw new ArgumentException("Resampled HRIR exceeds the 512-tap capacity.", nameof(sampleRate));
            var resampledLeft = new float[count];
            var resampledRight = new float[count];
            double cutoff = Math.Min(1.0, ratio) * .95;
            for (int n = 0; n < count; n++)
            {
                double sourcePosition = n / ratio - radius;
                int start = Math.Max(0, (int)Math.Ceiling(sourcePosition - radius));
                int end = Math.Min(TapCount - 1, (int)Math.Floor(sourcePosition + radius));
                double l = 0, r = 0;
                for (int j = start; j <= end; j++)
                {
                    double distance = sourcePosition - j;
                    double phase = Math.PI * distance * cutoff;
                    double sinc = Math.Abs(phase) < 1e-10 ? 1.0 : Math.Sin(phase) / phase;
                    double window = .5 + .5 * Math.Cos(Math.PI * distance / radius);
                    double weight = cutoff * sinc * window / ratio;
                    l += left[j] * weight;
                    r += right[j] * weight;
                }
                resampledLeft[n] = (float)l;
                resampledRight[n] = (float)r;
            }
            return new HrirFilter(sampleRate, resampledLeft, resampledRight);
        }

        internal HrirFilter MirrorEars() => new HrirFilter(SampleRate, right, left);

        private static void ValidateSamples(float[] samples)
        {
            for (int i = 0; i < samples.Length; i++)
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 16f)
                    throw new ArgumentException("HRIR coefficients must be finite and within [-16, 16].");
        }
    }

    /// <summary>Measured source direction: 0 azimuth is front, +90 right, +90 elevation above.</summary>
    public readonly struct HrirMeasurement
    {
        public readonly float AzimuthDegrees, ElevationDegrees;
        public readonly HrirFilter Filter;

        public HrirMeasurement(float azimuthDegrees, float elevationDegrees, HrirFilter filter)
        {
            if (float.IsNaN(azimuthDegrees) || float.IsInfinity(azimuthDegrees) ||
                float.IsNaN(elevationDegrees) || float.IsInfinity(elevationDegrees) ||
                elevationDegrees < -90f || elevationDegrees > 90f)
                throw new ArgumentOutOfRangeException(nameof(azimuthDegrees));
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            AzimuthDegrees = ((azimuthDegrees % 360f) + 360f) % 360f;
            ElevationDegrees = elevationDegrees;
            Filter = filter;
        }
    }
}
