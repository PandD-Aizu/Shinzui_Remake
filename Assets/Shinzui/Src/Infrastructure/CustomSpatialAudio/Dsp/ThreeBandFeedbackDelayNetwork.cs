using System;
using Shinzui.Application.CustomSpatialAudio;

namespace Shinzui.Infrastructure.CustomSpatialAudio.Dsp
{
    /// <summary>
    /// Three independent eight-line FDNs fed by complementary input bands. Each band has an
    /// orthogonal Householder scattering matrix I - 2/N * 11' and scalar loop attenuation.
    /// Fourth-order Butterworth filters at both the input and stereo outputs isolate each
    /// decay network. Filtering only the excitation lets a slowly decaying low-band network
    /// dominate higher-band measurements. Crossovers still overlap; this is not an exact
    /// frequency-dependent feedback damping design.
    /// All memory is allocated in the constructor. Call only from one audio thread.
    /// </summary>
    internal sealed class ThreeBandFeedbackDelayNetwork
    {
        private const int LineCount = SpatialDspParameters.DelayLineCount;
        private const float Normalization = .3535533905932738f;
        private readonly DelayLine[] lines = new DelayLine[LineCount];
        private readonly float[] workLow = new float[LineCount];
        private readonly float[] workMid = new float[LineCount];
        private readonly float[] workHigh = new float[LineCount];
        private BandFilter inputLow, inputMid, inputHigh;
        private BandFilter leftLow, leftMid, leftHigh, rightLow, rightMid, rightHigh;

        internal ThreeBandFeedbackDelayNetwork(int sampleRate)
        {
            for (int i = 0; i < LineCount; i++)
                lines[i] = new DelayLine(SpatialDspParameters.GetDelayLineSamples(i, sampleRate));
            inputLow = leftLow = rightLow = new BandFilter(sampleRate, 0);
            inputMid = leftMid = rightMid = new BandFilter(sampleRate, 1);
            inputHigh = leftHigh = rightHigh = new BandFilter(sampleRate, 2);
        }

        internal void Process(float low, float mid, float high, SpatialDspParameters previous,
            SpatialDspParameters next, float blend, out float left, out float right)
        {
            float sumLow = 0f, sumMid = 0f, sumHigh = 0f;
            float leftLo = 0f, leftMi = 0f, leftHi = 0f;
            float rightLo = 0f, rightMi = 0f, rightHi = 0f;
            low = inputLow.Process(low);
            mid = inputMid.Process(mid);
            high = inputHigh.Process(high);
            for (int i = 0; i < LineCount; i++)
            {
                ref DelayLine line = ref lines[i];
                float lo = line.Low[line.Position];
                float mi = line.Mid[line.Position];
                float hi = line.High[line.Position];
                workLow[i] = lo;
                workMid[i] = mi;
                workHigh[i] = hi;
                sumLow += lo;
                sumMid += mi;
                sumHigh += hi;
                // Distinct, orthogonal sign patterns decorrelate the two diffuse outputs.
                float leftSign = (i & 1) == 0 ? 1f : -1f;
                float rightSign = (i & 2) == 0 ? 1f : -1f;
                leftLo += lo * leftSign;
                leftMi += mi * leftSign;
                leftHi += hi * leftSign;
                rightLo += lo * rightSign;
                rightMi += mi * rightSign;
                rightHi += hi * rightSign;
            }
            left = (leftLow.Process(leftLo) + leftMid.Process(leftMi) + leftHigh.Process(leftHi)) * Normalization;
            right = (rightLow.Process(rightLo) + rightMid.Process(rightMi) + rightHigh.Process(rightHi)) * Normalization;
            sumLow *= .25f;
            sumMid *= .25f;
            sumHigh *= .25f;
            low *= Normalization;
            mid *= Normalization;
            high *= Normalization;

            for (int i = 0; i < LineCount; i++)
            {
                ref DelayLine line = ref lines[i];
                AcousticBandValues a = previous.GetFeedbackGains(i);
                AcousticBandValues b = next.GetFeedbackGains(i);
                // A convex interpolation keeps every loop gain strictly below one.
                float gainLow = a.Low + (b.Low - a.Low) * blend;
                float gainMid = a.Mid + (b.Mid - a.Mid) * blend;
                float gainHigh = a.High + (b.High - a.High) * blend;
                line.Low[line.Position] = Flush((workLow[i] - sumLow) * gainLow + low);
                line.Mid[line.Position] = Flush((workMid[i] - sumMid) * gainMid + mid);
                line.High[line.Position] = Flush((workHigh[i] - sumHigh) * gainHigh + high);
                if (++line.Position == line.Low.Length) line.Position = 0;
            }
        }

        internal void Reset()
        {
            inputLow.Reset(); inputMid.Reset(); inputHigh.Reset();
            leftLow.Reset(); leftMid.Reset(); leftHigh.Reset();
            rightLow.Reset(); rightMid.Reset(); rightHigh.Reset();
            for (int i = 0; i < LineCount; i++)
            {
                ref DelayLine line = ref lines[i];
                Array.Clear(line.Low, 0, line.Low.Length);
                Array.Clear(line.Mid, 0, line.Mid.Length);
                Array.Clear(line.High, 0, line.High.Length);
                line.Position = 0;
            }
        }

        private static float Flush(float value) => value > -1e-20f && value < 1e-20f ? 0f : value;

        private struct BandFilter
        {
            private Biquad first, second, third, fourth;
            private readonly bool isMid;

            internal BandFilter(int sampleRate, int band)
            {
                const double q1 = .541196100146197;
                const double q2 = 1.306562964876377;
                isMid = band == 1;
                double frequency = band == 2 ? 4000.0 : 250.0;
                bool highPass = band != 0;
                first = new Biquad(sampleRate, frequency, q1, highPass);
                second = new Biquad(sampleRate, frequency, q2, highPass);
                third = isMid ? new Biquad(sampleRate, 4000.0, q1, false) : default;
                fourth = isMid ? new Biquad(sampleRate, 4000.0, q2, false) : default;
            }

            internal float Process(float sample)
            {
                double value = second.Process(first.Process(sample));
                if (isMid) value = fourth.Process(third.Process(value));
                return (float)value;
            }

            internal void Reset()
            {
                first.Reset(); second.Reset(); third.Reset(); fourth.Reset();
            }
        }

        // Bilinear-transform LPF/HPF coefficients from the RBJ Audio EQ Cookbook:
        // https://www.w3.org/TR/audio-eq-cookbook/ . Double state avoids low-cutoff
        // roundoff at 96 kHz. No coefficient design takes place on the audio thread.
        private struct Biquad
        {
            private readonly double b0, b1, b2, a1, a2;
            private double z1, z2;

            internal Biquad(int sampleRate, double frequency, double q, bool highPass)
            {
                double omega = 2.0 * Math.PI * frequency / sampleRate;
                double cosine = Math.Cos(omega);
                double alpha = Math.Sin(omega) / (2.0 * q);
                double inverseA0 = 1.0 / (1.0 + alpha);
                b0 = (highPass ? 1.0 + cosine : 1.0 - cosine) * .5 * inverseA0;
                b1 = (highPass ? -2.0 : 2.0) * b0;
                b2 = b0;
                a1 = -2.0 * cosine * inverseA0;
                a2 = (1.0 - alpha) * inverseA0;
                z1 = z2 = 0.0;
            }

            internal double Process(double sample)
            {
                double value = b0 * sample + z1;
                z1 = Flush(b1 * sample - a1 * value + z2);
                z2 = Flush(b2 * sample - a2 * value);
                return value;
            }

            internal void Reset() { z1 = z2 = 0.0; }
            private static double Flush(double value) => value > -1e-20 && value < 1e-20 ? 0.0 : value;
        }

        private struct DelayLine
        {
            public readonly float[] Low, Mid, High;
            public int Position;

            public DelayLine(int length)
            {
                Low = new float[length];
                Mid = new float[length];
                High = new float[length];
                Position = 0;
            }
        }
    }
}
