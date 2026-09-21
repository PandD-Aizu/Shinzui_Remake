using System;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Shinzui.Application.CustomSpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio.Binaural;
using Shinzui.Infrastructure.CustomSpatialAudio.Dsp;

namespace Shinzui.Tests.CustomSpatialAudio
{
    public sealed class BinauralHrirTests
    {
        private static readonly AcousticVector3 Front = new AcousticVector3(0, 0, 1);
        private static readonly AcousticVector3 Up = new AcousticVector3(0, 1, 0);

        [Test]
        public void FiltersAndDatasetDefensivelyOwnTheirInputs()
        {
            var left = new[] { 1f, .5f };
            var right = new[] { .25f, 0f };
            var filter = new HrirFilter(48000, left, right);
            var measurements = new[] { new HrirMeasurement(0, 0, filter) };
            var dataset = new HrirDataset(measurements);
            left[0] = right[0] = 8f;
            measurements[0] = default;
            Assert.That(dataset.InterpolateAngles(0, 0).GetLeft(0), Is.EqualTo(1f));
            Assert.That(dataset.InterpolateAngles(0, 0).GetRight(0), Is.EqualTo(.25f));
            Assert.Throws<ArgumentException>(() => new HrirFilter(48000, new[] { float.NaN }, new[] { 0f }));
            Assert.Throws<ArgumentException>(() => new HrirDataset(new[] { default(HrirMeasurement) }));
        }

        [Test]
        public void AzimuthWrapAndElevationInterpolationAreContinuous()
        {
            var dataset = new HrirDataset(new[] {
                Measurement(350, -10, 0), Measurement(10, -10, 2),
                Measurement(350, 10, 4), Measurement(10, 10, 6) });
            Assert.That(dataset.InterpolateAngles(0, 0).GetLeft(0), Is.EqualTo(3f).Within(1e-6));
            Assert.That(dataset.InterpolateAngles(360, 0).GetLeft(0), Is.EqualTo(3f).Within(1e-6));
            Assert.That(dataset.InterpolateAngles(-.001f, 0).GetLeft(0),
                Is.EqualTo(dataset.InterpolateAngles(.001f, 0).GetLeft(0)).Within(.0003));
            Assert.That(dataset.InterpolateAngles(0, -90).GetLeft(0), Is.EqualTo(1f).Within(1e-6));
            Assert.That(dataset.InterpolateAngles(0, 90).GetLeft(0), Is.EqualTo(5f).Within(1e-6));
        }

        [Test]
        public void ListenerYawAndPitchUseSourceRelativeDirection()
        {
            var dataset = new HrirDataset(new[] {
                Measurement(0, 0, 1), Measurement(90, 0, 2), Measurement(180, 0, 3),
                Measurement(270, 0, 4), Measurement(0, 90, 5) });
            var right = new AcousticVector3(1, 0, 0);
            Assert.That(dataset.Interpolate(right, Front, Up).GetLeft(0), Is.EqualTo(2f));
            Assert.That(dataset.Interpolate(right, right, Up).GetLeft(0), Is.EqualTo(1f));
            Assert.That(dataset.Interpolate(Up, Front, Up).GetLeft(0), Is.EqualTo(5f));
            Assert.That(dataset.Interpolate(Up, Up, new AcousticVector3(0, 0, -1)).GetLeft(0), Is.EqualTo(1f));
            Assert.That(dataset.Interpolate(default, Front, Up).GetLeft(0), Is.EqualTo(1f));
            Assert.Throws<ArgumentException>(() => dataset.Interpolate(Front, Up, Up));
        }

        [TestCase(16000)]
        [TestCase(48000)]
        [TestCase(96000)]
        public void SincResamplingPreservesImpulseGainAndEarTimeDifference(int rate)
        {
            var left = new float[128];
            var right = new float[128];
            left[40] = 1f;
            right[20] = .5f;
            HrirFilter converted = new HrirFilter(44100, left, right).Resample(rate);
            double sumLeft = 0, sumRight = 0;
            for (int i = 0; i < converted.TapCount; i++)
            {
                sumLeft += converted.GetLeft(i);
                sumRight += converted.GetRight(i);
            }
            Assert.That(sumLeft, Is.EqualTo(1.0).Within(.01));
            Assert.That(sumRight, Is.EqualTo(.5).Within(.005));
            Assert.That((PeakIndex(converted, true) - PeakIndex(converted, false)) / (double)rate,
                Is.EqualTo(20.0 / 44100).Within(1.1 / rate));
            Assert.That(PeakIndex(converted, false) / (double)rate,
                Is.EqualTo(36.0 / 44100).Within(1.1 / rate));
        }

        [Test]
        public void WavReaderHonoursChannelsAndRejectsTruncatedOrCompressedData()
        {
            byte[] wav = PcmWav();
            HrirFilter filter = HrirWavReader.ReadPcm16Stereo(wav);
            Assert.That(filter.GetLeft(0), Is.EqualTo(.5f));
            Assert.That(filter.GetRight(0), Is.EqualTo(-.25f));
            Assert.That(filter.GetLeft(1), Is.EqualTo(-1f));
            var truncated = new byte[wav.Length - 1];
            Array.Copy(wav, truncated, truncated.Length);
            Assert.Throws<InvalidDataException>(() => HrirWavReader.ReadPcm16Stereo(truncated));
            wav[20] = 3;
            Assert.Throws<InvalidDataException>(() => HrirWavReader.ReadPcm16Stereo(wav));
        }

        [Test]
        public void BinauralImpulseCombinesFractionalTravelDelayWithMeasuredEarsWithoutExtraPan()
        {
            var dataset = OneFilter(48000, new[] { 1f, .5f }, new[] { .25f, 0f });
            var dsp = new RectangularRoomDsp(48000);
            dsp.ApplyParameters(Parameters(2.5f / 48000, dataset));
            var input = new float[16];
            var output = new float[32];
            input[0] = 1f;
            Assert.That(dsp.Process(input, 0, output, 0, input.Length), Is.True);
            Assert.That(output[4], Is.EqualTo(.5f).Within(1e-6));
            Assert.That(output[6], Is.EqualTo(.75f).Within(1e-6));
            Assert.That(output[8], Is.EqualTo(.25f).Within(1e-6));
            Assert.That(output[5], Is.EqualTo(.125f).Within(1e-6));
            Assert.That(output[7], Is.EqualTo(.125f).Within(1e-6));
        }

        [Test]
        public void MaximumTravelDelayCanReadTheLastSupportedHrirTap()
        {
            const int rate = 16000;
            var left = new float[HrirFilter.MaximumTapCount];
            var right = new float[left.Length];
            left[left.Length - 1] = 1f;
            var dataset = OneFilter(rate, left, right);
            var dsp = new RectangularRoomDsp(rate);
            dsp.ApplyParameters(Parameters(3f, dataset));
            int arrival = 3 * rate + left.Length - 1;
            var input = new float[arrival + 2];
            var output = new float[input.Length * 2];
            input[0] = 1f;
            dsp.Process(input, 0, output, 0, input.Length);
            Assert.That(output[2 * arrival], Is.EqualTo(1f).Within(1e-5));
            for (int i = 0; i < arrival; i++)
                Assert.That(Math.Abs(output[i * 2]) + Math.Abs(output[i * 2 + 1]), Is.LessThan(1e-6));
        }

        [Test]
        public void RateMismatchIsRejectedBeforeAudioPublication()
        {
            var dataset = OneFilter(44100, new[] { 1f }, new[] { 1f });
            Assert.Throws<ArgumentException>(() => SpatialDspParameters.Create(Response(0), 48000, hrirDataset: dataset));
        }

        [Test]
        public void BinauralTransitionsUseExistingHistoryAndAllocateNothingInAudioProcessing()
        {
            var a = Parameters(0, OneFilter(48000, new[] { 1f }, new[] { .25f }));
            var b = Parameters(0, OneFilter(48000, new[] { .25f }, new[] { 1f }));
            var dsp = new RectangularRoomDsp(48000);
            var input = new float[1024];
            var output = new float[2048];
            for (int i = 0; i < input.Length; i++) input[i] = 1f;
            dsp.ApplyParameters(a);
            dsp.Process(input, 0, output, 0, input.Length);
            dsp.ApplyParameters(b);
            long allocated = MeasureProcessAllocation(dsp, input, output);
            Assert.That(allocated, Is.Zero);
            Assert.That(output[0], Is.EqualTo(1f).Within(1e-5));
            Assert.That(output[1918], Is.EqualTo(.25f).Within(1e-5));
            for (int i = 1; i < input.Length; i++)
                Assert.That(Math.Abs(output[i * 2] - output[(i - 1) * 2]), Is.LessThan(.002f));
        }

        [Test]
        public void BundledKemarDataContainsMeasuredFrontBackElevationAndCorrectEarMirroring()
        {
            string path = Path.Combine(UnityEngine.Application.streamingAssetsPath,
                "CustomSpatialAudio", "Kemar", "mit-kemar-compact.zip");
            HrirDataset dataset = HrirDataset.LoadMitKemarCompact(path, 48000);
            Assert.That(dataset.MeasurementCount, Is.EqualTo(710));
            Assert.That(dataset.MinimumElevation, Is.EqualTo(-40));
            Assert.That(dataset.MaximumElevation, Is.EqualTo(90));
            HrirFilter front = dataset.InterpolateAngles(0, 0);
            HrirFilter back = dataset.InterpolateAngles(180, 0);
            HrirFilter up = dataset.InterpolateAngles(0, 90);
            HrirFilter right = dataset.InterpolateAngles(90, 0);
            HrirFilter left = dataset.InterpolateAngles(270, 0);
            double frontBackDifference = 0, frontUpDifference = 0;
            for (int i = 0; i < front.TapCount; i++)
            {
                frontBackDifference += Math.Abs(front.GetLeft(i) - back.GetLeft(i));
                frontUpDifference += Math.Abs(front.GetLeft(i) - up.GetLeft(i));
                Assert.That(left.GetLeft(i), Is.EqualTo(right.GetRight(i)));
                Assert.That(left.GetRight(i), Is.EqualTo(right.GetLeft(i)));
            }
            Assert.That(frontBackDifference, Is.GreaterThan(.1));
            Assert.That(frontUpDifference, Is.GreaterThan(.1));
            Assert.That(PeakIndex(right, false), Is.LessThan(PeakIndex(right, true)));
        }

        private static HrirMeasurement Measurement(float azimuth, float elevation, float value) =>
            new HrirMeasurement(azimuth, elevation, new HrirFilter(48000, new[] { value }, new[] { value }));

        // Keep assertion boxing outside the measured method even under aggressive JIT optimization.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long MeasureProcessAllocation(RectangularRoomDsp dsp, float[] input, float[] output)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            dsp.Process(input, 0, output, 0, input.Length);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        private static HrirDataset OneFilter(int rate, float[] left, float[] right) =>
            new HrirDataset(new[] { new HrirMeasurement(0, 0, new HrirFilter(rate, left, right)) });

        private static SpatialDspParameters Parameters(float delay, HrirDataset dataset) =>
            SpatialDspParameters.Create(Response(delay), dataset.SampleRate, 1, 0, 0, dataset);

        private static RoomAcousticResponse Response(float delay)
        {
            var direct = new AcousticPathTap(delay, 0, new AcousticBandValues(1, 1, 1), Front, 1);
            var silent = new AcousticPathTap(0, 0, default, Front, 0);
            return new RoomAcousticResponse(direct, silent, silent, silent, silent, silent, silent,
                new AcousticBandValues(1, 1, 1), 0);
        }

        private static int PeakIndex(HrirFilter filter, bool left)
        {
            int peak = 0;
            float maximum = 0;
            for (int i = 0; i < filter.TapCount; i++)
            {
                float magnitude = Math.Abs(left ? filter.GetLeft(i) : filter.GetRight(i));
                if (magnitude > maximum) { maximum = magnitude; peak = i; }
            }
            return peak;
        }

        private static byte[] PcmWav()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(0x46464952u); writer.Write(44u); writer.Write(0x45564157u);
                    writer.Write(0x20746d66u); writer.Write(16u); writer.Write((ushort)1); writer.Write((ushort)2);
                    writer.Write(48000u); writer.Write(192000u); writer.Write((ushort)4); writer.Write((ushort)16);
                    writer.Write(0x61746164u); writer.Write(8u);
                    writer.Write((short)16384); writer.Write((short)-8192);
                    writer.Write(short.MinValue); writer.Write(short.MaxValue);
                }
                return stream.ToArray();
            }
        }
    }
}
