using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Shinzui.Application.CustomSpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio.Binaural;
using Shinzui.Infrastructure.CustomSpatialAudio.Dsp;
using UnityEngine;

namespace Shinzui.CustomSpatialAudio.Probe
{
    /// <summary>Relative workload evidence, not an FMOD device underrun or whole-game CPU measurement.</summary>
    public static class CustomSpatialAudioPerformanceProbe
    {
        [Serializable] public sealed class Measurement
        {
            public string renderer;
            public int voices;
            public int hrirPaths;
            public double audioBlockMilliseconds, meanBlockMilliseconds, p95BlockMilliseconds, maxBlockMilliseconds;
            public long allocatedBytes;
            public bool finite;
        }
        [Serializable] public sealed class Report
        {
            public string unityVersion;
            public string environment;
            public string scope = "Serial pure-DSP render of 512-frame blocks at 48 kHz, after warmup. Includes one FDN per voice. Excludes FMOD, geometry, HRIR interpolation, GPU, and gameplay. Development build timings are machine-dependent; no production voice budget is inferred.";
            public List<Measurement> cases = new List<Measurement>();
        }

        public static void Run(string outputDirectory, HrirDataset dataset)
        {
            var wall = new AcousticWall(new AcousticBandValues(.12f,.25f,.45f));
            var room = new RectangularAcousticRoom(new AcousticVector3(-3,0,-4), new AcousticVector3(3,3,4), wall);
            var response = RectangularRoomAcoustics.Calculate(room, new AcousticVector3(1,1.5f,1),
                new AcousticVector3(0,1.5f,-1), new AcousticVector3(1,0,0));
            var report = new Report { unityVersion = UnityEngine.Application.unityVersion,
                environment = UnityEngine.Application.isEditor ? "Editor" : "Windows Player" };
            foreach (int voices in new[] { 1, 8, 16 })
            {
                report.cases.Add(Measure(SpatialDspParameters.Create(response, 48000), voices, "stereo", 0));
                report.cases.Add(Measure(SpatialDspParameters.Create(response, 48000, 1, 1, .25f,
                    dataset, new AcousticVector3(0,0,1), new AcousticVector3(0,1,0)), voices, "MIT KEMAR direct FIR", 1));
            }
            report.cases.Add(Measure(SpatialDspParameters.Create(response, 48000, 1, 1, .25f,
                dataset, new AcousticVector3(0,0,1), new AcousticVector3(0,1,0), maxHrirPaths: 7),
                1, "MIT KEMAR all-path FIR (quality comparison)", 7));
            File.WriteAllText(Path.Combine(outputDirectory, "performance.json"), JsonUtility.ToJson(report, true));
            foreach (var item in report.cases)
                if (!item.finite || item.allocatedBytes != 0)
                    throw new InvalidOperationException("Audio DSP produced invalid samples or allocated during processing.");
        }

        static Measurement Measure(SpatialDspParameters parameters, int voices, string renderer, int hrirPaths)
        {
            const int frames = 512, blocks = 64;
            var processors = new RectangularRoomDsp[voices];
            for (int i = 0; i < voices; i++)
            {
                processors[i] = new RectangularRoomDsp(48000);
                processors[i].ApplyParameters(parameters);
            }
            var input = new float[frames];
            var output = new float[frames * 2];
            for (int i = 0; i < frames; i++) input[i] = .01f * (float)Math.Sin(i * .17);
            for (int block = 0; block < 16; block++)
                for (int i = 0; i < voices; i++) processors[i].Process(input, 0, output, 0, frames);
            var timings = new double[blocks];
            bool finite = true;
            long allocationStart = GC.GetAllocatedBytesForCurrentThread();
            for (int block = 0; block < blocks; block++)
            {
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < voices; i++)
                {
                    processors[i].Process(input, 0, output, 0, frames);
                    for (int j = 0; j < output.Length; j++)
                        finite &= !float.IsNaN(output[j]) && !float.IsInfinity(output[j]);
                }
                timings[block] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
            double total = 0;
            foreach (double timing in timings) total += timing;
            Array.Sort(timings);
            return new Measurement { renderer = renderer, voices = voices, hrirPaths = hrirPaths, audioBlockMilliseconds = frames / 48.0,
                meanBlockMilliseconds = total / blocks, p95BlockMilliseconds = timings[(int)(blocks * .95)],
                maxBlockMilliseconds = timings[blocks - 1], allocatedBytes = allocated, finite = finite };
        }
    }
}
