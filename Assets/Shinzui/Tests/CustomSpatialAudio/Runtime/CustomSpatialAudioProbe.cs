using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FMOD;
using Shinzui.Application.CustomSpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio.Dsp;
using Shinzui.Infrastructure.CustomSpatialAudio.Binaural;
using UnityEngine;
using static Shinzui.Infrastructure.CustomSpatialAudio.FmodRoomAcousticProcessor;

namespace Shinzui.CustomSpatialAudio.Probe
{
    /// <summary>Reproducible Core-only signal experiment; never calls RuntimeManager or loads Studio banks.</summary>
    public static class CustomSpatialAudioProbe
    {
        const int Rate = 48000;
        [Serializable] public sealed class CaseResult
        {
            public string name;
            public string output;
            public int sampleRate;
            public int frames;
            public int callbacks;
            public float peak;
            public double energy;
            public double tailEnergy;
            public float targetLowRt60, targetMidRt60, targetHighRt60;
            public bool finite;
            public bool sourceStopped;
            public bool passed;
            public string callbackError;
            public bool captureOverflowed;
        }
        [Serializable] public sealed class Report
        {
            public string unityVersion;
            public string environment;
            public string signalPath = "Private FMOD Core system -> dry PCM -> custom room DSP -> stereo output; no Studio bank or Steam Audio DSP in this signal path.";
            public string limitation = "Room cases use equal-power stereo; binaural cases use measured MIT KEMAR HRIRs. Portal cases are one centre-line path, not physical diffraction. WAV records custom DSP output before the group fader, not a microphone or loopback recording. Perceptual listening evaluation remains required.";
            public bool passed;
            public List<CaseResult> cases = new List<CaseResult>();
        }

        public static Report Run(string outputDirectory, bool realtime)
        {
            Directory.CreateDirectory(outputDirectory);
            var report = new Report
            {
                unityVersion = UnityEngine.Application.unityVersion,
                environment = UnityEngine.Application.isEditor ? "Editor" : "Windows Player",
                passed = true
            };
            // Same sample and source/listener geometry for all small-room component comparisons.
            if (realtime)
            {
                report.cases.Add(RunCase("small-room-audition", Room(false, false), outputDirectory, 1, 1, .25f, true));
                var response = BinauralResponse(new AcousticVector3(1,0,0));
                var parameters = SpatialDspParameters.Create(response, Rate, 1, 0, 0, LoadDataset(),
                    new AcousticVector3(0,0,1), new AcousticVector3(0,1,0));
                report.cases.Add(RunResponseCase("binaural-right-audition", response, parameters, outputDirectory, true, false));
            }
            else
            {
                report.cases.Add(RunCase("small-room-direct", Room(false, false), outputDirectory, 1, 0, 0, false));
                report.cases.Add(RunCase("small-room-early", Room(false, false), outputDirectory, 0, 1, 0, false));
                report.cases.Add(RunCase("small-room-late", Room(false, false), outputDirectory, 0, 0, .25f, false));
                report.cases.Add(RunCase("small-room-mixed", Room(false, false), outputDirectory, 1, 1, .25f, false));
                report.cases.Add(RunCase("straight-tunnel-mixed", Room(true, false), outputDirectory, 1, 1, .25f, false));
                report.cases.Add(RunCase("open-boundaries-mixed", Room(false, true), outputDirectory, 1, 1, .25f, false));
                AddPortalCases(report, outputDirectory);
                AddBinauralCases(report, outputDirectory);
            }
            foreach (var item in report.cases) report.passed &= item.passed;
            string path = Path.Combine(outputDirectory, realtime ? "realtime-report.json" : "report.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidOperationException("Custom acoustic probe failed. See " + path);
            UnityEngine.Debug.Log("[CustomSpatialAudio] Passed: " + Path.GetFullPath(path));
            return report;
        }

        static RectangularAcousticRoom Room(bool corridor, bool open)
        {
            var wall = new AcousticWall(new AcousticBandValues(.12f, .25f, .45f), open ? 1 : 0);
            return new RectangularAcousticRoom(new AcousticVector3(-3, 0, corridor ? -15 : -4),
                new AcousticVector3(3, 3, corridor ? 15 : 4), wall);
        }

        static CaseResult RunCase(string name, RectangularAcousticRoom room, string outputDirectory,
            float direct, float early, float late, bool realtime)
        {
            var response = RectangularRoomAcoustics.Calculate(room, new AcousticVector3(1, 1.5f, 1),
                new AcousticVector3(0, 1.5f, -1), new AcousticVector3(1, 0, 0));
            return RunResponseCase(name, response, SpatialDspParameters.Create(response, Rate, direct, early, late),
                outputDirectory, realtime, late > 0 && response.LateGain > 0);
        }

        static CaseResult RunResponseCase(string name, RoomAcousticResponse response, SpatialDspParameters parameters,
            string outputDirectory, bool realtime, bool expectTail, bool expectSilence = false)
        {
            Check(Factory.System_Create(out FMOD.System system));
            Sound sound = default;
            FmodRoomAcousticProcessor effect = null;
            bool initialized = false;
            try
            {
                Check(system.setOutput(realtime ? OUTPUTTYPE.WASAPI : OUTPUTTYPE.NOSOUND_NRT));
                Check(system.setSoftwareFormat(Rate, SPEAKERMODE.STEREO, 0));
                Check(system.setDSPBufferSize(512, 4));
                Check(system.init(32, INITFLAGS.NORMAL, IntPtr.Zero));
                initialized = true;
                Check(system.getSoftwareFormat(out int rate, out _, out _));
                Check(system.getOutput(out OUTPUTTYPE output));
                Check(system.getMasterChannelGroup(out ChannelGroup master));
                effect = new FmodRoomAcousticProcessor(system, master, parameters, 5);
                byte[] pcm = Pulse(rate, realtime);
                var info = new CREATESOUNDEXINFO
                {
                    cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(), length = (uint)pcm.Length,
                    numchannels = 1, defaultfrequency = rate, format = SOUND_FORMAT.PCMFLOAT
                };
                Check(system.createSound(pcm, MODE.OPENMEMORY | MODE.OPENRAW | MODE._2D | MODE.LOOP_OFF, ref info, out sound));
                effect.BeginCapture();
                Check(system.playSound(sound, effect.Group, true, out Channel channel));
                Check(channel.setVolumeRamp(false));
                Check(channel.setPaused(false));
                // Three seconds of processing after a <=30ms excitation; no silent sound used to keep the tail alive.
                if (realtime)
                {
                    var timer = Stopwatch.StartNew();
                    while (timer.Elapsed.TotalSeconds < 3) { Check(system.update()); System.Threading.Thread.Sleep(5); }
                }
                else
                {
                    for (int i = 0; i < rate * 3 / 512 + 4; i++) Check(system.update());
                }
                RESULT playingResult = channel.isPlaying(out bool isPlaying);
                bool stopped = playingResult == RESULT.ERR_INVALID_HANDLE || playingResult == RESULT.OK && !isPlaying;
                float[] samples = effect.FinishCapture();
                var result = Measure(name, samples, rate);
                result.output = output.ToString();
                result.callbacks = effect.CallbackCount;
                result.callbackError = effect.CallbackError.ToString();
                result.captureOverflowed = effect.CaptureOverflowed;
                result.sourceStopped = stopped;
                result.targetLowRt60 = response.Rt60Seconds.Low;
                result.targetMidRt60 = response.Rt60Seconds.Mid;
                result.targetHighRt60 = response.Rt60Seconds.High;
                result.passed = result.finite && (expectSilence ? result.peak == 0 : result.peak > 0 && result.peak < 1) && result.frames >= rate * 2 &&
                    result.callbacks > 0 && effect.CallbackError == RESULT.OK && !effect.CaptureOverflowed && stopped &&
                    (expectTail ? result.tailEnergy > 1e-16 : result.tailEnergy < 1e-12);
                WriteWave(Path.Combine(outputDirectory, name + ".wav"), samples, rate);
                return result;
            }
            finally
            {
                effect?.Dispose();
                if (sound.hasHandle()) Check(sound.release());
                if (initialized) Check(system.close());
                Check(system.release());
            }
        }

        static void AddPortalCases(Report report, string directory)
        {
            var wall = new AcousticWall(new AcousticBandValues(.12f, .25f, .45f));
            var rooms = new[]
            {
                new PortalAcousticRoom(0, new RectangularAcousticRoom(new AcousticVector3(0,0,0), new AcousticVector3(8,3,2), wall)),
                new PortalAcousticRoom(1, new RectangularAcousticRoom(new AcousticVector3(6,0,2), new AcousticVector3(8,3,10), wall))
            };
            foreach (float opening in new[] { 1f, .25f, 0f })
            {
                var graph = new PortalAcousticGraph(rooms, new[] { new AcousticPortal(0, 0, 1,
                    new AcousticVector3(7,1.5f,2), opening, new AcousticBandValues(1,1,1)) });
                var path = PortalRoomAcoustics.Calculate(graph, 0, new AcousticVector3(1,1.5f,1),
                    1, new AcousticVector3(7,1.5f,9), new AcousticVector3(1,0,0));
                var response = path.ToRoomResponse();
                string name = opening == 1 ? "l-corridor-open" : opening == 0 ? "l-corridor-closed" : "l-corridor-quarter-open";
                report.cases.Add(RunResponseCase(name, response, SpatialDspParameters.Create(response, Rate, 1, 0, 0),
                    directory, false, false, opening == 0));
            }
        }

        static void AddBinauralCases(Report report, string directory)
        {
            var dataset = LoadDataset();
            var positions = new[] { new AcousticVector3(0,0,1), new AcousticVector3(0,0,-1),
                new AcousticVector3(0,1,0), new AcousticVector3(1,0,0) };
            var names = new[] { "front", "back", "up", "right" };
            for (int i = 0; i < positions.Length; i++)
            {
                var response = BinauralResponse(positions[i]);
                var parameters = SpatialDspParameters.Create(response, Rate, 1, 0, 0,
                    dataset, new AcousticVector3(0,0,1), new AcousticVector3(0,1,0));
                report.cases.Add(RunResponseCase("binaural-" + names[i], response, parameters, directory, false, false));
            }
            CustomSpatialAudioPerformanceProbe.Run(directory, dataset);
        }

        static HrirDataset LoadDataset() => HrirDataset.LoadMitKemarCompact(
            Path.Combine(UnityEngine.Application.streamingAssetsPath, "CustomSpatialAudio", "Kemar", "mit-kemar-compact.zip"), Rate);

        static RoomAcousticResponse BinauralResponse(AcousticVector3 direction)
        {
            var tap = new AcousticPathTap(1f / 343f, 1, new AcousticBandValues(1,1,1), direction, direction.X);
            return new RoomAcousticResponse(tap, default, default, default, default, default, default,
                new AcousticBandValues(.1f,.1f,.1f), 0);
        }

        static byte[] Pulse(int rate, bool audition)
        {
            var values = new float[audition ? rate / 30 : 1];
            uint random = 0x12345678;
            values[0] = .5f;
            if (audition)
                for (int i = 0; i < values.Length; i++)
                {
                    random = random * 1664525u + 1013904223u;
                    values[i] = ((random >> 8) / 8388608f - 1) * .25f * (1f - i / (float)values.Length);
                }
            var bytes = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        static CaseResult Measure(string name, float[] data, int rate)
        {
            var result = new CaseResult { name = name, sampleRate = rate, frames = data.Length / 2, finite = true };
            for (int i = 0; i < data.Length; i++)
            {
                float value = data[i];
                result.finite &= !float.IsNaN(value) && !float.IsInfinity(value);
                result.peak = Math.Max(result.peak, Math.Abs(value));
                result.energy += (double)value * value;
                if (i >= rate * 2 / 5) result.tailEnergy += (double)value * value;
            }
            return result;
        }

        static void WriteWave(string path, float[] samples, int rate)
        {
            // IEEE float WAV preserves the measured level and small tail without normalization/PCM quantization.
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 4);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)3); writer.Write((short)2); writer.Write(rate); writer.Write(rate * 8);
                writer.Write((short)8); writer.Write((short)32);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 4);
                foreach (float value in samples) writer.Write(value);
            }
        }
    }
}
