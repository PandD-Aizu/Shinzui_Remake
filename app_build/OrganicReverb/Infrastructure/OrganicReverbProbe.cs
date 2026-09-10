using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FMODUnity;
using SteamAudio;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Shinzui.AudioProbe.Infrastructure
{
    // Isolated technical experiment; no dependency on production gameplay or UI layers.
    public sealed class OrganicReverbProbe : MonoBehaviour
    {
        public Transform Listener;
        public StudioEventEmitter Emitter;
        public SteamAudioSource AcousticSource;
        public static bool Completed { get; private set; }
        public static bool Passed { get; private set; }
        readonly List<CaseResult> results = new List<CaseResult>();
        ProbeCapture capture;
        float startedAt;
        bool failed;
        string output;

        [Serializable] public sealed class CaseResult
        {
            public string name;
            public bool reflections;
            public float occlusion;
            public float peak;
            public bool dspFound;
            public bool simulationSourceReady;
            public float dspCpuPercent;
            public bool captureOverflow;
        }
        [Serializable] sealed class Report
        {
            public string unityVersion;
            public string fmodVersion;
            public string steamAudioVersion = "4.8.1";
            public string environment;
            public bool passed;
            public List<CaseResult> cases;
        }

        void Awake()
        {
            Completed = false; Passed = false;
            output = GetArgument("-organicReverbOutput") ?? Path.Combine(UnityEngine.Application.persistentDataPath, "OrganicReverbProbe");
            Directory.CreateDirectory(output);
            startedAt = Time.realtimeSinceStartup;
            UnityEngine.Application.logMessageReceived += OnLog;
        }

        IEnumerator Start()
        {
            // Capture through the mixer even when the CLI session has no physical output device.
            ProbeCapture.Check(RuntimeManager.CoreSystem.getOutput(out var outputType));
            Debug.Log("[OrganicReverbProbe] FMOD output: " + outputType);
            capture = new ProbeCapture(RuntimeManager.CoreSystem);
            yield return new WaitForSecondsRealtime(2);
            for (int station = 0; station < 3; station++)
            {
                string label = new[] { "small-room", "corridor", "outdoor" }[station];
                yield return RunCase(label + "-dry", station * 40, 0, false);
                yield return RunCase(label + "-wet", station * 40, 0, true);
            }
            yield return RunCase("wall-occluded", 0, 2.2f, false);
            yield return RunCase("wall-occluded-wet", 0, 2.2f, true);
            Finish();
        }

        IEnumerator RunCase(string name, float stationX, float offsetX, bool wet)
        {
            Emitter.Stop();
            Listener.position = new Vector3(stationX + offsetX, 1.5f, -1.5f);
            Emitter.transform.position = new Vector3(stationX + offsetX, 1.5f, 1.5f);
            // Keep simulations running throughout; switch only the DSP mix for an equivalent A/B.
            yield return new WaitForSecondsRealtime(1.5f);
            capture.Begin();
            Emitter.Play();
            RuntimeManager.StudioSystem.flushCommands();
            float deadline = Time.realtimeSinceStartup + .75f;
            FMOD.DSP spatializer = default;
            while (!FindSpatializer(out spatializer) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!spatializer.hasHandle()) throw new InvalidOperationException("Steam Audio Spatializer not found in probe event.");
            SetBool(spatializer, "ApplyRefl", wet);
            yield return new WaitForSecondsRealtime(5f);
            // Steam Audio 4.8.1 getInt(SIMULATION_OUTPUTS_HANDLE) always returns -1.
            // Check DSP presence/source creation here; output analysis verifies their actual connection.
            ProbeCapture.Check(RuntimeManager.CoreSystem.getCPUUsage(out var cpu));
            var result = new CaseResult { name = name, reflections = wet,
                occlusion = AcousticSource.GetOutputs(SimulationFlags.Direct).direct.occlusion,
                peak = capture.Finish(Path.Combine(output, name + ".wav")), dspFound = spatializer.hasHandle(),
                simulationSourceReady = AcousticSource.GetSource().Get() != IntPtr.Zero,
                dspCpuPercent = cpu.dsp, captureOverflow = capture.Overflowed };
            results.Add(result);
            if (!result.dspFound || !result.simulationSourceReady || result.captureOverflow) failed = true;
            if (offsetX == 0 && result.peak < .00001f) failed = true;
            Debug.Log("[OrganicReverbProbe] " + JsonUtility.ToJson(result));
            Emitter.Stop();
            yield return new WaitForSecondsRealtime(.4f);
        }

        bool FindSpatializer(out FMOD.DSP result)
        {
            result = default;
            var instance = Emitter.EventInstance;
            if (!instance.isValid() || instance.getChannelGroup(out var group) != FMOD.RESULT.OK) return false;
            group.getNumDSPs(out int count);
            for (int i = 0; i < count; i++)
            {
                group.getDSP(i, out var dsp);
                dsp.getInfo(out string name, out _, out _, out _, out _);
                if (name == "Steam Audio Spatializer") { result = dsp; return true; }
            }
            return false;
        }

        static void SetBool(FMOD.DSP dsp, string name, bool value)
        {
            ProbeCapture.Check(dsp.getNumParameters(out int count));
            for (int i = 0; i < count; i++)
            {
                ProbeCapture.Check(dsp.getParameterInfo(i, out var info));
                if (System.Text.Encoding.UTF8.GetString(info.name).TrimEnd('\0') == name)
                { ProbeCapture.Check(dsp.setParameterBool(i, value)); return; }
            }
            throw new InvalidOperationException("DSP parameter missing: " + name);
        }

        void Update()
        {
            if (!Completed && Time.realtimeSinceStartup - startedAt > 150)
            { failed = true; Debug.LogError("[OrganicReverbProbe] Timed out."); Finish(); }
        }

        void OnLog(string condition, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true;
        }

        void Finish()
        {
            if (Completed) return;
            Emitter.Stop();
            capture?.Dispose(); capture = null;
            RuntimeManager.CoreSystem.getVersion(out uint version);
            Passed = !failed && results.Count == 8;
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(new Report {
                unityVersion = UnityEngine.Application.unityVersion, fmodVersion = version.ToString("X8"),
                environment = UnityEngine.Application.isEditor ? "Editor Play Mode (CLI)" : "Windows Player",
                passed = Passed, cases = results }, true));
            Completed = true;
            Debug.Log("[OrganicReverbProbe] Completed: " + Passed + "; " + output);
            if (!UnityEngine.Application.isEditor && GetArgument("-organicReverbOutput") != null)
                UnityEngine.Application.Quit(Passed ? 0 : 1);
        }

        void OnDestroy()
        {
            UnityEngine.Application.logMessageReceived -= OnLog;
            capture?.Dispose();
        }

        static string GetArgument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
