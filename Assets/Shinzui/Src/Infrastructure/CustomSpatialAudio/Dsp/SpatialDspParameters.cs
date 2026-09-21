using System;
using Shinzui.Application.CustomSpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio.Binaural;

namespace Shinzui.Infrastructure.CustomSpatialAudio.Dsp
{
    /// <summary>
    /// Immutable DSP coefficients. Construct on the control thread, then publish the reference
    /// atomically to the audio thread. No geometry or transcendental maths run while rendering.
    /// </summary>
    public sealed class SpatialDspParameters
    {
        public const int MinimumSampleRate = 16000;
        public const int MaximumSampleRate = 96000;
        public const float MaximumPathDelaySeconds = 3f;
        public const int DelayLineCount = 8;

        private static readonly int[] ReferenceDelays = { 1499, 1601, 1867, 1999, 2131, 2269, 2447, 2671 };
        private readonly PathCoefficients[] paths;
        private readonly AcousticBandValues[] feedbackGains;

        public int SampleRate { get; }
        public int HrirPathCount { get; }
        internal AcousticBandValues LateInputGains { get; }
        internal float LateOutputGain { get; }
        internal float LateDelaySamples { get; }

        private SpatialDspParameters(RoomAcousticResponse response, int sampleRate,
            float directGain, float earlyGain, float lateGain, HrirDataset hrirDataset,
            AcousticVector3 listenerForward, AcousticVector3 listenerUp, int maxHrirPaths)
        {
            ValidateSampleRate(sampleRate);
            ValidateRange(directGain, 0f, 4f, nameof(directGain));
            ValidateRange(earlyGain, 0f, 4f, nameof(earlyGain));
            ValidateRange(lateGain, 0f, 4f, nameof(lateGain));
            if (maxHrirPaths < 0 || maxHrirPaths > RoomAcousticResponse.PathCount)
                throw new ArgumentOutOfRangeException(nameof(maxHrirPaths), "The binaural path budget is 0..7.");
            if (hrirDataset != null && hrirDataset.SampleRate != sampleRate)
                throw new ArgumentException("Resample the HRIR dataset to the DSP sample rate before publication.", nameof(hrirDataset));
            if (listenerForward.X == 0f && listenerForward.Y == 0f && listenerForward.Z == 0f)
                listenerForward = new AcousticVector3(0f, 0f, 1f);
            if (listenerUp.X == 0f && listenerUp.Y == 0f && listenerUp.Z == 0f)
                listenerUp = new AcousticVector3(0f, 1f, 0f);
            ValidateRange(response.LateBandGain.Low, 0f, 1f, nameof(response.LateBandGain));
            ValidateRange(response.LateBandGain.Mid, 0f, 1f, nameof(response.LateBandGain));
            ValidateRange(response.LateBandGain.High, 0f, 1f, nameof(response.LateBandGain));
            ValidateRange(response.Rt60Seconds.Low, .01f, 30f, nameof(response.Rt60Seconds));
            ValidateRange(response.Rt60Seconds.Mid, .01f, 30f, nameof(response.Rt60Seconds));
            ValidateRange(response.Rt60Seconds.High, .01f, 30f, nameof(response.Rt60Seconds));

            SampleRate = sampleRate;
            LateInputGains = response.LateBandGain;
            LateOutputGain = lateGain;
            int hrirPathMask = hrirDataset == null ? 0 : SelectHrirPaths(response, maxHrirPaths);
            paths = new PathCoefficients[RoomAcousticResponse.PathCount];
            for (int i = 0; i < paths.Length; i++)
            {
                AcousticPathTap tap = response.GetPath(i);
                ValidateRange(tap.DelaySeconds, 0f, MaximumPathDelaySeconds, nameof(tap.DelaySeconds));
                ValidateRange(tap.Pan, -1f, 1f, nameof(tap.Pan));
                ValidateRange(tap.Amplitude.Low, 0f, 1f, nameof(tap.Amplitude));
                ValidateRange(tap.Amplitude.Mid, 0f, 1f, nameof(tap.Amplitude));
                ValidateRange(tap.Amplitude.High, 0f, 1f, nameof(tap.Amplitude));
                float angle = (tap.Pan + 1f) * (float)(Math.PI * .25);
                float gain = i == 0 ? directGain : earlyGain;
                HrirFilter hrir = (hrirPathMask & (1 << i)) == 0 ? null :
                    hrirDataset.Interpolate(tap.Direction, listenerForward, listenerUp);
                if (hrir != null) HrirPathCount++;
                // An HRIR already contains interaural level differences; do not pan it again.
                paths[i] = new PathCoefficients(tap.DelaySeconds * sampleRate, tap.Amplitude,
                    hrir == null ? (float)Math.Cos(angle) * gain : gain,
                    hrir == null ? (float)Math.Sin(angle) * gain : gain, hrir);
            }

            // The diffuse response cannot precede reflected sound in a large room. Keep
            // this geometry timing independent of the user's early-reflection mix gain.
            float firstReflectionDelay = float.MaxValue;
            for (int i = 1; i < paths.Length; i++)
            {
                PathCoefficients path = paths[i];
                if ((path.Amplitude.Low > 0f || path.Amplitude.Mid > 0f || path.Amplitude.High > 0f) &&
                    path.DelaySamples < firstReflectionDelay)
                    firstReflectionDelay = path.DelaySamples;
            }
            LateDelaySamples = firstReflectionDelay == float.MaxValue ? paths[0].DelaySamples :
                Math.Max(paths[0].DelaySamples, firstReflectionDelay);

            feedbackGains = new AcousticBandValues[DelayLineCount];
            for (int i = 0; i < DelayLineCount; i++)
            {
                double delaySeconds = (double)GetDelayLineSamples(i, sampleRate) / sampleRate;
                feedbackGains[i] = new AcousticBandValues(
                    DecayGain(delaySeconds, response.Rt60Seconds.Low),
                    DecayGain(delaySeconds, response.Rt60Seconds.Mid),
                    DecayGain(delaySeconds, response.Rt60Seconds.High));
            }
        }

        /// <summary>
        /// Creates control-thread coefficients. With a dataset, the default budget renders
        /// direct sound binaurally and reflections with stereo pan. A budget of 2..7 adds
        /// strongest reflected paths; 0 disables HRIRs. Profile the chosen budget on target.
        /// </summary>
        public static SpatialDspParameters Create(RoomAcousticResponse response, int sampleRate,
            float directGain = 1f, float earlyGain = 1f, float lateGain = .25f,
            HrirDataset hrirDataset = null, AcousticVector3 listenerForward = default,
            AcousticVector3 listenerUp = default, int maxHrirPaths = 1)
        {
            return new SpatialDspParameters(response, sampleRate, directGain, earlyGain, lateGain,
                hrirDataset, listenerForward, listenerUp, maxHrirPaths);
        }

        // The default reserves HRIR convolution for the direct sound. Additional slots use
        // the strongest reflected band-amplitude energy; ties preserve wall/path order.
        // Unselected paths retain equal-power pan. Seven is an explicit evaluation mode.
        private static int SelectHrirPaths(RoomAcousticResponse response, int budget)
        {
            if (budget == 0) return 0;
            int selected = 1;
            for (int slot = 1; slot < budget; slot++)
            {
                int best = -1;
                double bestEnergy = -1.0;
                for (int path = 1; path < RoomAcousticResponse.PathCount; path++)
                {
                    if ((selected & (1 << path)) != 0) continue;
                    AcousticBandValues amplitude = response.GetPath(path).Amplitude;
                    double energy = (double)amplitude.Low * amplitude.Low +
                        (double)amplitude.Mid * amplitude.Mid + (double)amplitude.High * amplitude.High;
                    if (energy > bestEnergy) { bestEnergy = energy; best = path; }
                }
                if (best < 0 || bestEnergy <= 0.0) break;
                selected |= 1 << best;
            }
            return selected;
        }

        /// <summary>Prime delay lengths, scaled from the 48 kHz prototype. Control-thread use.</summary>
        public static int GetDelayLineSamples(int lineIndex, int sampleRate)
        {
            ValidateSampleRate(sampleRate);
            if (lineIndex < 0 || lineIndex >= DelayLineCount)
                throw new ArgumentOutOfRangeException(nameof(lineIndex));
            int candidate = (int)Math.Round((double)ReferenceDelays[lineIndex] * sampleRate / 48000);
            if ((candidate & 1) == 0) candidate++;
            while (!IsPrime(candidate)) candidate += 2;
            return candidate;
        }

        internal PathCoefficients GetPath(int index) => paths[index];
        internal AcousticBandValues GetFeedbackGains(int index) => feedbackGains[index];

        internal static void ValidateSampleRate(int sampleRate)
        {
            if (sampleRate < MinimumSampleRate || sampleRate > MaximumSampleRate)
                throw new ArgumentOutOfRangeException(nameof(sampleRate), "Supported sample rates are 16000..96000 Hz.");
        }

        private static void ValidateRange(float value, float minimum, float maximum, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentOutOfRangeException(name);
        }

        private static float DecayGain(double delaySeconds, float rt60Seconds)
        {
            return (float)Math.Pow(10.0, -3.0 * delaySeconds / rt60Seconds);
        }

        private static bool IsPrime(int number)
        {
            if (number < 2) return false;
            for (int divisor = 3; divisor * divisor <= number; divisor += 2)
                if (number % divisor == 0) return false;
            return true;
        }

        internal readonly struct PathCoefficients
        {
            public readonly float DelaySamples;
            public readonly AcousticBandValues Amplitude;
            public readonly float LeftGain, RightGain;
            public readonly int WholeDelaySamples;
            public readonly bool UniformAmplitude;
            // These arrays are created once, retained privately by the owning immutable
            // parameters and only read by the renderer. Never mutate after publication.
            public readonly float[] LeftKernel, RightKernel;

            public PathCoefficients(float delaySamples, AcousticBandValues amplitude, float leftGain, float rightGain,
                HrirFilter hrir)
            {
                DelaySamples = delaySamples;
                Amplitude = amplitude;
                LeftGain = leftGain;
                RightGain = rightGain;
                WholeDelaySamples = (int)delaySamples;
                UniformAmplitude = amplitude.Low == amplitude.Mid && amplitude.Mid == amplitude.High;
                LeftKernel = RightKernel = null;
                if (hrir == null) return;
                float fraction = delaySamples - WholeDelaySamples;
                int length = hrir.TapCount + (fraction == 0f ? 0 : 1);
                LeftKernel = new float[length];
                RightKernel = new float[length];
                float bandGain = UniformAmplitude ? amplitude.Low : 1f;
                for (int tap = 0; tap < hrir.TapCount; tap++)
                {
                    float left = hrir.GetLeft(tap) * leftGain * bandGain;
                    float right = hrir.GetRight(tap) * rightGain * bandGain;
                    LeftKernel[tap] += left * (1f - fraction);
                    RightKernel[tap] += right * (1f - fraction);
                    if (fraction != 0f)
                    {
                        LeftKernel[tap + 1] += left * fraction;
                        RightKernel[tap + 1] += right * fraction;
                    }
                }
            }
        }
    }
}
