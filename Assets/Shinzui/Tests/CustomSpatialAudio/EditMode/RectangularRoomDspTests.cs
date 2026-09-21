using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Shinzui.Application.CustomSpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio.Binaural;
using Shinzui.Infrastructure.CustomSpatialAudio.Dsp;

namespace Shinzui.Tests.CustomSpatialAudio
{
    public sealed class RectangularRoomDspTests
    {
        private const int SampleRate = 48000;

        [Test]
        public void DirectImpulseArrivesAtItsTravelDelayWithEqualPowerPan()
        {
            var dsp = new RectangularRoomDsp(SampleRate);
            dsp.ApplyParameters(Parameters(.01f, new AcousticBandValues(.4f, .4f, .4f), 1f, 0f));
            var input = new float[1024];
            var output = new float[2048];
            input[0] = 1f;
            Assert.That(dsp.Process(input, 0, output, 0, input.Length), Is.True);
            for (int i = 0; i < 480; i++)
                Assert.That(Math.Abs(output[2 * i]) + Math.Abs(output[2 * i + 1]), Is.LessThan(1e-5f));
            Assert.That(output[960], Is.EqualTo(Math.Sqrt(.5)).Within(1e-5));
            Assert.That(output[961], Is.EqualTo(Math.Sqrt(.5)).Within(1e-5));
            Assert.That(Energy(output, 481, 1024), Is.LessThan(1e-9));
        }

        [Test]
        public void EachFirstOrderReflectionUsesItsOwnDelayAndPan()
        {
            var reflections = new AcousticPathTap[6];
            for (int i = 0; i < reflections.Length; i++)
                reflections[i] = Tap((i + 1) * .005f, .5f, i % 2 == 0 ? -1f : 1f);
            var response = new RoomAcousticResponse(Tap(0f, 0f), reflections[0], reflections[1],
                reflections[2], reflections[3], reflections[4], reflections[5],
                new AcousticBandValues(.4f, .4f, .4f), 0f);
            var dsp = new RectangularRoomDsp(SampleRate);
            dsp.ApplyParameters(SpatialDspParameters.Create(response, SampleRate, 0f, 1f, 0f));
            var input = new float[1800];
            var output = new float[input.Length * 2];
            input[0] = 1f;
            dsp.Process(input, 0, output, 0, input.Length);
            for (int i = 0; i < reflections.Length; i++)
            {
                int sample = (i + 1) * 240;
                int expectedChannel = i % 2;
                Assert.That(output[sample * 2 + expectedChannel], Is.EqualTo(.5f).Within(1e-4));
                Assert.That(Math.Abs(output[sample * 2 + 1 - expectedChannel]), Is.LessThan(1e-5));
            }
        }

        [Test]
        public void NullDryInputContinuesTheTailAndResetClearsIt()
        {
            var dsp = new RectangularRoomDsp(SampleRate);
            dsp.ApplyParameters(Parameters(0f, new AcousticBandValues(1f, .7f, .4f), 0f, 1f));
            var impulse = new[] { 1f };
            var first = new float[2];
            var tail = new float[SampleRate];
            dsp.Process(impulse, 0, first, 0, 1);
            dsp.Process(null, 0, tail, 0, tail.Length / 2);
            Assert.That(Energy(tail, 0, tail.Length / 2), Is.GreaterThan(1e-4));
            dsp.Reset();
            dsp.Process(null, 0, tail, 0, tail.Length / 2);
            Assert.That(Energy(tail, 0, tail.Length / 2), Is.EqualTo(0.0));
        }

        [Test]
        public void FullyOpenResponseDoesNotExciteALateTail()
        {
            var dsp = new RectangularRoomDsp(SampleRate);
            dsp.ApplyParameters(Parameters(0f, new AcousticBandValues(.05f, .05f, .05f), 0f, 1f, 0f));
            var input = new float[SampleRate];
            var output = new float[input.Length * 2];
            input[0] = 1f;
            dsp.Process(input, 0, output, 0, input.Length);
            Assert.That(Energy(output, 0, input.Length), Is.EqualTo(0.0));
        }

        [Test]
        public void LargeRoomLateEnergyCannotPrecedeItsFirstReflection()
        {
            // A source and listener at the centre of a 100 m cube have 100 m first-order paths.
            float reflectionDelay = 100f / 343f;
            AcousticPathTap reflection = Tap(reflectionDelay, .01f);
            var response = new RoomAcousticResponse(Tap(0f, 1f), reflection, reflection, reflection,
                reflection, reflection, reflection, new AcousticBandValues(2f, 1f, .5f));
            var dsp = new RectangularRoomDsp(SampleRate);
            dsp.ApplyParameters(SpatialDspParameters.Create(response, SampleRate, 0f, 0f, 1f));
            var input = new float[SampleRate];
            var output = new float[input.Length * 2];
            input[0] = 1f;
            dsp.Process(input, 0, output, 0, input.Length);
            int firstReflectionFrame = (int)(reflectionDelay * SampleRate);
            Assert.That(Energy(output, 0, firstReflectionFrame), Is.EqualTo(0.0));
            Assert.That(Energy(output, firstReflectionFrame, SampleRate), Is.GreaterThan(1e-6));
        }

        [Test]
        public void FullyAbsorbingRoomProducesNoEarlyOrLateEnergy()
        {
            var room = new RectangularAcousticRoom(new AcousticVector3(-5f, -5f, -5f),
                new AcousticVector3(5f, 5f, 5f), new AcousticWall(new AcousticBandValues(1f, 1f, 1f)));
            RoomAcousticResponse response = RectangularRoomAcoustics.Calculate(room, default, default,
                new AcousticVector3(1f, 0f, 0f));
            var dsp = new RectangularRoomDsp(SampleRate);
            dsp.ApplyParameters(SpatialDspParameters.Create(response, SampleRate, 0f, 1f, 1f));
            var input = new float[SampleRate];
            var output = new float[input.Length * 2];
            input[0] = 1f;
            dsp.Process(input, 0, output, 0, input.Length);
            Assert.That(Energy(output, 0, input.Length), Is.EqualTo(0.0));
        }

        [Test]
        public void ProcessingIsInvariantToDryBufferChunking()
        {
            var parameters = Parameters(.014f, new AcousticBandValues(1.3f, .8f, .3f), 1f, .5f);
            var whole = new RectangularRoomDsp(SampleRate);
            var chunked = new RectangularRoomDsp(SampleRate);
            whole.ApplyParameters(parameters);
            chunked.ApplyParameters(parameters);
            var input = new float[10007];
            for (int i = 0; i < 500; i++) input[i] = (float)Math.Sin(i * .27) * .2f;
            var a = new float[input.Length * 2];
            var b = new float[input.Length * 2];
            whole.Process(input, 0, a, 0, input.Length);
            for (int start = 0; start < input.Length; start += 137)
                chunked.Process(input, start, b, start * 2, Math.Min(137, input.Length - start));
            Assert.That(b, Is.EqualTo(a));
        }

        [Test]
        public void LongerUniformRt60PreservesMoreLateEnergy()
        {
            double shortEnergy = ImpulseLateEnergy(new AcousticBandValues(.2f, .2f, .2f));
            double longEnergy = ImpulseLateEnergy(new AcousticBandValues(1.5f, 1.5f, 1.5f));
            Assert.That(longEnergy, Is.GreaterThan(shortEnergy * 100.0));
        }

        [Test]
        public void LowAndHighRt60ControlsAffectTheirExcitedBandsIndependently()
        {
            var longLow = new AcousticBandValues(1.2f, .3f, .1f);
            var longHigh = new AcousticBandValues(.1f, .3f, 1.2f);
            // Away from crossover regions, each decay network should dominate its band.
            Assert.That(BurstLateEnergy(80f, longLow), Is.GreaterThan(BurstLateEnergy(80f, longHigh) * 3));
            Assert.That(BurstLateEnergy(12000f, longHigh), Is.GreaterThan(BurstLateEnergy(12000f, longLow) * 3));
        }

        [Test]
        public void InvalidRateDelayAndBufferInputsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RectangularRoomDsp(8000));
            Assert.Throws<ArgumentOutOfRangeException>(() => Parameters(3.01f,
                new AcousticBandValues(.5f, .5f, .5f), 1f, 0f));
            var dsp = new RectangularRoomDsp(SampleRate);
            var wrongRate = SpatialDspParameters.Create(Response(0f, new AcousticBandValues(.5f, .5f, .5f)), 44100);
            Assert.That(dsp.ApplyParameters(wrongRate), Is.False);
            Assert.That(dsp.ApplyParameters(null), Is.False);
            var output = new[] { 123f, 123f };
            Assert.That(dsp.Process(null, 0, output, 0, 2), Is.False);
            Assert.That(output[0], Is.EqualTo(123f));
            Assert.That(dsp.Process(output, 0, output, 0, 1), Is.False);
        }

        [Test]
        public void NonFiniteInputAndRepeatedRoomUpdatesStayFinite()
        {
            var dsp = new RectangularRoomDsp(SampleRate);
            var lowRoom = Parameters(.01f, new AcousticBandValues(3f, 2f, .5f), 1f, 1f);
            var highRoom = Parameters(.05f, new AcousticBandValues(.2f, .4f, 2f), .5f, .3f);
            var input = new float[256];
            var output = new float[512];
            input[0] = float.NaN;
            input[1] = float.PositiveInfinity;
            input[2] = float.NegativeInfinity;
            input[3] = float.MaxValue;
            input[4] = -float.MaxValue;
            for (int block = 0; block < 100; block++)
            {
                dsp.ApplyParameters(block % 2 == 0 ? lowRoom : highRoom);
                Assert.That(dsp.Process(input, 0, output, 0, input.Length), Is.True);
                foreach (float value in output)
                    Assert.That(!float.IsNaN(value) && !float.IsInfinity(value), Is.True);
            }
        }

        [Test]
        public void GainAndDelayChangeCrossfadeWithoutAStepOnAConstantSignal()
        {
            var dsp = new RectangularRoomDsp(SampleRate);
            var rt60 = new AcousticBandValues(.5f, .5f, .5f);
            dsp.ApplyParameters(Parameters(0f, rt60, 1f, 0f));
            var input = new float[5000];
            for (int i = 0; i < input.Length; i++) input[i] = .25f;
            var output = new float[input.Length * 2];
            dsp.Process(input, 0, output, 0, input.Length);
            float before = output[output.Length - 2];
            dsp.ApplyParameters(Parameters(.05f, rt60, 0f, 0f));
            dsp.Process(input, 0, output, 0, 2000);
            Assert.That(output[0], Is.EqualTo(before).Within(1e-6));
            for (int frame = 1; frame < 960; frame++)
            {
                Assert.That(output[frame * 2], Is.LessThanOrEqualTo(output[(frame - 1) * 2] + 1e-6));
                Assert.That(Math.Abs(output[frame * 2] - output[(frame - 1) * 2]), Is.LessThan(.0003f));
            }
            Assert.That(output[960 * 2], Is.EqualTo(0f));
        }

        [Test]
        public void QueuedRoomChangeStartsAtSameSampleRegardlessOfCallbackSize()
        {
            var whole = new RectangularRoomDsp(SampleRate);
            var chunked = new RectangularRoomDsp(SampleRate);
            var rt60 = new AcousticBandValues(.5f, .5f, .5f);
            var first = Parameters(0f, rt60, 1f, 0f);
            var second = Parameters(0f, rt60, .5f, 0f);
            var third = Parameters(0f, rt60, 0f, 0f);
            var input = new float[3000];
            for (int i = 0; i < input.Length; i++) input[i] = .25f;
            var a = new float[input.Length * 2];
            var b = new float[input.Length * 2];
            whole.ApplyParameters(first);
            chunked.ApplyParameters(first);
            whole.Process(input, 0, a, 0, 1);
            chunked.Process(input, 0, b, 0, 1);
            whole.ApplyParameters(second);
            chunked.ApplyParameters(second);
            whole.Process(input, 0, a, 0, 100);
            chunked.Process(input, 0, b, 0, 100);
            whole.ApplyParameters(third);
            chunked.ApplyParameters(third);
            whole.Process(input, 0, a, 0, input.Length);
            for (int start = 0; start < input.Length; start += 137)
                chunked.Process(input, start, b, start * 2, Math.Min(137, input.Length - start));
            Assert.That(b, Is.EqualTo(a));
            Assert.That(a[2000 * 2], Is.EqualTo(0f));
        }

        [TestCase(125f, 1.008f)]
        [TestCase(1000f, .448f)]
        [TestCase(8000f, .216f)]
        public void NarrowBandT20DecayTracksItsConfiguredRt60(float frequency, float expectedRt60)
        {
            // T20 extrapolation of a Hann-windowed narrowband burst. Measuring the rendered
            // output catches the slow-band leakage that individual feedback-gain tests miss.
            var input = new float[SampleRate * 3];
            int burstFrames = SampleRate / 5;
            for (int i = 0; i < burstFrames; i++)
            {
                double envelope = .5 - .5 * Math.Cos(2 * Math.PI * i / (burstFrames - 1));
                input[i] = (float)(.1 * envelope * Math.Sin(2 * Math.PI * frequency * i / SampleRate));
            }
            var output = new float[input.Length * 2];
            var dsp = new RectangularRoomDsp(SampleRate);
            dsp.ApplyParameters(Parameters(0f, new AcousticBandValues(1.008f, .448f, .216f), 0f, 1f));
            Assert.That(dsp.Process(input, 0, output, 0, input.Length), Is.True);
            double measured = ExtrapolateT20(output, burstFrames);
            Console.WriteLine("{0} Hz: output T20 RT60={1:F4}s; configured={2:F4}s.",
                frequency, measured, expectedRt60);
            Assert.That(measured, Is.EqualTo(expectedRt60).Within(expectedRt60 * .15),
                "Measured output T20 must not be controlled by leakage from another decay network.");
        }

        [Test]
        public void WarmProcessingAndQueuedTransitionsAllocateNoManagedMemory()
        {
            var dsp = new RectangularRoomDsp(SampleRate);
            var a = Parameters(.01f, new AcousticBandValues(1f, .5f, .2f), 1f, .25f);
            var b = Parameters(.03f, new AcousticBandValues(.5f, .3f, .1f), .5f, .5f);
            var input = new float[256];
            var output = new float[512];
            input[0] = 1f;
            dsp.ApplyParameters(a);
            for (int block = 0; block < 32; block++)
            {
                dsp.ApplyParameters((block & 1) == 0 ? a : b);
                dsp.Process(input, 0, output, 0, input.Length);
            }
            GC.GetAllocatedBytesForCurrentThread(); // Warm the runtime's per-thread counter as well.
            Assert.That(MeasureProcessingAllocations(dsp, a, b, input, output), Is.EqualTo(0));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void BinauralBudgetReservesDirectThenSelectsStrongestReflectionWithStableTies(int budget)
        {
            var dataset = new HrirDataset(new[] { new HrirMeasurement(0f, 0f,
                new HrirFilter(SampleRate, new[] { 0f }, new[] { 1f })) });
            var response = new RoomAcousticResponse(Tap(0f, .25f), Tap(.01f, .1f, -1f),
                Tap(.02f, .8f, -1f), Tap(.03f, .8f, -1f), default, default, default,
                new AcousticBandValues(.5f, .5f, .5f), 0f);
            var parameters = SpatialDspParameters.Create(response, SampleRate, 1f, 1f, 0f,
                dataset, maxHrirPaths: budget);
            Assert.That(parameters.HrirPathCount, Is.EqualTo(budget));
            var dsp = new RectangularRoomDsp(SampleRate);
            dsp.ApplyParameters(parameters);
            var input = new float[1600]; input[0] = 1f;
            var output = new float[input.Length * 2];
            dsp.Process(input, 0, output, 0, input.Length);
            Assert.That(output[0], Is.EqualTo(0f).Within(1e-6));
            Assert.That(output[1], Is.EqualTo(.25f).Within(1e-6));
            Assert.That(output[480 * 2], Is.EqualTo(.1f).Within(1e-5));
            Assert.That(output[960 * 2 + (budget == 2 ? 1 : 0)], Is.EqualTo(.8f).Within(1e-4));
            Assert.That(output[960 * 2 + (budget == 2 ? 0 : 1)], Is.EqualTo(0f).Within(1e-4));
            Assert.That(output[1440 * 2], Is.EqualTo(.8f).Within(1e-4));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PreparedFractionalHrirKernelsMatchReferenceConvolution(bool uniformBands)
        {
            float[] earLeft = { .75f, -.3f, .2f }, earRight = { 0f, .5f, -.25f };
            var dataset = new HrirDataset(new[] { new HrirMeasurement(0f, 0f,
                new HrirFilter(SampleRate, earLeft, earRight)) });
            var amplitude = uniformBands ? new AcousticBandValues(.4f, .4f, .4f) :
                new AcousticBandValues(.2f, .7f, .4f);
            var direct = new AcousticPathTap(10.25f / SampleRate, 1f, amplitude, default, -1f);
            var response = new RoomAcousticResponse(direct, default, default, default, default,
                default, default, new AcousticBandValues(.5f, .5f, .5f), 0f);
            var reference = new RectangularRoomDsp(SampleRate);
            reference.ApplyParameters(SpatialDspParameters.Create(response, SampleRate, .6f, 0f, 0f));
            var binaural = new RectangularRoomDsp(SampleRate);
            binaural.ApplyParameters(SpatialDspParameters.Create(response, SampleRate, .6f, 0f, 0f, dataset));
            var input = new float[768];
            for (int frame = 0; frame < 512; frame++) input[frame] = (float)Math.Sin(frame * .137) * .2f;
            var dry = new float[input.Length * 2];
            var actual = new float[input.Length * 2];
            reference.Process(input, 0, dry, 0, input.Length);
            binaural.Process(input, 0, actual, 0, input.Length);
            for (int frame = 0; frame < input.Length; frame++)
            {
                double left = 0.0, right = 0.0;
                for (int tap = 0; tap < earLeft.Length && tap <= frame; tap++)
                {
                    left += dry[(frame - tap) * 2] * earLeft[tap];
                    right += dry[(frame - tap) * 2] * earRight[tap];
                }
                Assert.That(actual[frame * 2], Is.EqualTo(left).Within(1e-6));
                Assert.That(actual[frame * 2 + 1], Is.EqualTo(right).Within(1e-6));
            }
        }

        [Test]
        public void BinauralBudgetDefaultsToDirectAndRejectsInvalidLimits()
        {
            var dataset = new HrirDataset(new[] { new HrirMeasurement(0f, 0f,
                new HrirFilter(SampleRate, new[] { 1f }, new[] { 1f })) });
            var response = Response(0f, new AcousticBandValues(.5f, .5f, .5f));
            Assert.That(SpatialDspParameters.Create(response, SampleRate, hrirDataset: dataset).HrirPathCount,
                Is.EqualTo(1));
            Assert.That(SpatialDspParameters.Create(response, SampleRate, hrirDataset: dataset,
                maxHrirPaths: 0).HrirPathCount, Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpatialDspParameters.Create(response,
                SampleRate, maxHrirPaths: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpatialDspParameters.Create(response,
                SampleRate, maxHrirPaths: 8));
        }

        [Test]
        public void LongestFractionalDelayRetainsTheExtraTapOfAMaximumLengthHrir()
        {
            var left = new float[HrirFilter.MaximumTapCount];
            var right = new float[left.Length];
            left[left.Length - 1] = 1f;
            right[right.Length - 1] = -.5f;
            var dataset = new HrirDataset(new[] { new HrirMeasurement(0f, 0f,
                new HrirFilter(SampleRate, left, right)) });
            float delay = (SpatialDspParameters.MaximumPathDelaySeconds * SampleRate - .5f) / SampleRate;
            var response = Response(delay, new AcousticBandValues(.5f, .5f, .5f), 0f);
            var dsp = new RectangularRoomDsp(SampleRate);
            dsp.ApplyParameters(SpatialDspParameters.Create(response, SampleRate, 1f, 0f, 0f, dataset));
            var input = new float[(int)(SpatialDspParameters.MaximumPathDelaySeconds * SampleRate) + left.Length + 3];
            input[0] = 1f;
            var output = new float[input.Length * 2];
            dsp.Process(input, 0, output, 0, input.Length);
            // Match the binary32 sample-delay value published in PathCoefficients. Mono
            // can retain extra precision in the multiplication/subtraction expression;
            // this explicit byte roundtrip forces the same float boundary as publication.
            float sampleDelay = BitConverter.ToSingle(BitConverter.GetBytes(delay * SampleRate), 0);
            int whole = (int)sampleDelay;
            float fraction = sampleDelay - whole;
            int first = whole + left.Length - 1;
            Assert.That(Energy(output, 0, first), Is.EqualTo(0));
            Assert.That(output[first * 2], Is.EqualTo(1f - fraction).Within(1e-6));
            Assert.That(output[(first + 1) * 2], Is.EqualTo(fraction).Within(1e-6));
            Assert.That(output[(first + 1) * 2 + 1], Is.EqualTo(-.5f * fraction).Within(1e-6));
        }

        // Keep assertion boxing outside the measured method: optimizers may otherwise
        // schedule that allocation before the second allocation-counter read.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long MeasureProcessingAllocations(RectangularRoomDsp dsp,
            SpatialDspParameters a, SpatialDspParameters b, float[] input, float[] output)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int block = 0; block < 128; block++)
            {
                dsp.ApplyParameters((block & 1) == 0 ? a : b);
                dsp.Process(input, 0, output, 0, input.Length);
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        private static double ExtrapolateT20(float[] stereo, int startFrame)
        {
            int count = stereo.Length / 2;
            var cumulative = new double[count];
            double energy = 0.0;
            for (int frame = count - 1; frame >= startFrame; frame--)
            {
                energy += (double)stereo[frame * 2] * stereo[frame * 2] +
                    (double)stereo[frame * 2 + 1] * stereo[frame * 2 + 1];
                cumulative[frame] = energy;
            }
            Assert.That(energy, Is.GreaterThan(0));
            double sx = 0, sy = 0, sxx = 0, sxy = 0;
            int points = 0;
            for (int frame = startFrame; frame < count && cumulative[frame] > 0.0; frame++)
            {
                double db = 10.0 * Math.Log10(cumulative[frame] / energy);
                if (db > -5.0) continue;
                if (db < -25.0) break;
                double time = (double)(frame - startFrame) / SampleRate;
                sx += time; sy += db; sxx += time * time; sxy += time * db;
                points++;
            }
            Assert.That(points, Is.GreaterThan(100));
            return -60.0 * (points * sxx - sx * sx) / (points * sxy - sx * sy);
        }

        private static double ImpulseLateEnergy(AcousticBandValues rt60)
        {
            var input = new float[SampleRate];
            input[0] = 1f;
            return RenderLateEnergy(input, rt60, SampleRate / 2, SampleRate);
        }

        private static double BurstLateEnergy(float frequency, AcousticBandValues rt60)
        {
            var input = new float[SampleRate];
            const int burstFrames = 4800;
            for (int i = 0; i < burstFrames; i++)
            {
                double envelope = .5 - .5 * Math.Cos(2 * Math.PI * i / (burstFrames - 1));
                input[i] = (float)(.1 * envelope * Math.Sin(2 * Math.PI * frequency * i / SampleRate));
            }
            return RenderLateEnergy(input, rt60, SampleRate / 3, SampleRate / 2);
        }

        private static double RenderLateEnergy(float[] input, AcousticBandValues rt60, int start, int end)
        {
            var dsp = new RectangularRoomDsp(SampleRate);
            dsp.ApplyParameters(Parameters(0f, rt60, 0f, 1f));
            var output = new float[input.Length * 2];
            dsp.Process(input, 0, output, 0, input.Length);
            return Energy(output, start, end);
        }

        private static double Energy(float[] stereo, int startFrame, int endFrame)
        {
            double energy = 0.0;
            for (int i = startFrame * 2; i < endFrame * 2; i++) energy += (double)stereo[i] * stereo[i];
            return energy;
        }

        private static SpatialDspParameters Parameters(float delay, AcousticBandValues rt60,
            float directGain, float lateGain, float lateInputGain = 1f)
        {
            return SpatialDspParameters.Create(Response(delay, rt60, lateInputGain), SampleRate,
                directGain, 0f, lateGain);
        }

        private static RoomAcousticResponse Response(float delay, AcousticBandValues rt60, float lateInputGain = 1f)
        {
            return new RoomAcousticResponse(Tap(delay, 1f), default, default, default, default, default,
                default, rt60, lateInputGain);
        }

        private static AcousticPathTap Tap(float delay, float gain, float pan = 0f)
        {
            return new AcousticPathTap(delay, delay * 343f, new AcousticBandValues(gain, gain, gain), default, pan);
        }
    }
}
