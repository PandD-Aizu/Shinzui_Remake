using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FMOD;
using FMODUnity;
using Shinzui.Application.SpatialAudio;
using Shinzui.DI.CustomSpatialAudio;
using Shinzui.DI.GenerateTunnel;
using Shinzui.View;
using Shinzui.View.GenerateTunnel;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shinzui.CustomSpatialAudio.GameProbe
{
    /// <summary>Launches the actual game scene. Automated checks run only with -customAudioGameCheck.</summary>
    public sealed class CustomAudioGamePlayerProbe : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public int rooms, portals, processors, callbackErrors, builds;
            public float audiblePeak, seMutedPeak, masterMutedPeak, bgmMutedPeak, pausedPeak;
            public List<string> errors = new List<string>();
        }
        readonly Report report = new Report();
        bool checking;
        string output;
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            var args = Environment.GetCommandLineArgs();
            checking = Array.IndexOf(args, "-customAudioGameCheck") >= 0;
            int index = Array.IndexOf(args, "-customAudioOutput");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1] : UnityEngine.Application.persistentDataPath;
            if (checking) UnityEngine.Application.logMessageReceived += OnLog;
        }
        IEnumerator Start()
        {
            var work = Run();
            while (true)
            {
                bool next;
                try { next = work.MoveNext(); }
                catch (Exception error) { report.errors.Add(error.ToString()); Finish(); yield break; }
                if (!next) break;
                yield return work.Current;
            }
            if (checking) Finish(); else Destroy(gameObject);
        }
        IEnumerator Run()
        {
            yield return SceneManager.LoadSceneAsync("LatestStageGenerateTemp", LoadSceneMode.Single);
            if (!checking) yield break;
            yield return new WaitForSecondsRealtime(2);
            var scene = SceneManager.GetActiveScene();
            var binding = Find<CustomTunnelAudioBinding>(scene);
            Require(binding && binding.Runtime.World != null, "Main scene did not build custom acoustics.");
            var service = binding.Runtime.Voices;
            report.rooms = binding.Runtime.World.Graph.RoomCount; report.portals = binding.Runtime.World.Graph.PortalCount;
            report.processors = service.ProcessorCount;
            var player = Find<PlayerView>(scene);
            Require(player && player.MainCamera, "Main scene player/listener is missing.");
            var room = binding.Runtime.World.Graph.GetRoom(0).Bounds;
            var geometry = Find<TunnelMapView>(scene).GeometryRoot;
            var position = geometry.TransformPoint(new Vector3((room.Min.X + room.Max.X) / 2, room.Min.Y + .5f, (room.Min.Z + room.Max.Z) / 2));
            player.Warp(position - player.transform.position);
            yield return new WaitForSecondsRealtime(.5f);
            var p = player.MainCamera.transform.position + player.MainCamera.transform.right;
            var request = new SpatialAudioRequest("footstep.prototype", AudioPose.At(p.x, p.y, p.z));
            var bus = RuntimeManager.GetBus("bus:/WorldSE");
            bus.getChannelGroup(out var group); group.getDSP(CHANNELCONTROL_DSP_INDEX.HEAD, out var meter);
            meter.setMeteringEnabled(false, true);
            var master = RuntimeManager.GetVCA("vca:/Master"); var se = RuntimeManager.GetVCA("vca:/SE"); var bgm = RuntimeManager.GetVCA("vca:/BGM");
            master.setVolume(1); se.setVolume(1); bgm.setVolume(1);
            yield return new WaitForSecondsRealtime(.1f);
            Require(service.Play(request).Accepted, "Game voice was rejected.");
            yield return new WaitForSecondsRealtime(.08f); report.audiblePeak = Peak(meter);
            se.setVolume(0); yield return new WaitForSecondsRealtime(.1f); report.seMutedPeak = Peak(meter);
            se.setVolume(1); master.setVolume(0); yield return new WaitForSecondsRealtime(.1f); report.masterMutedPeak = Peak(meter);
            master.setVolume(1); bgm.setVolume(0); service.StopAll(SpatialAudioStopMode.Immediate);
            Require(service.Play(request).Accepted, "Recycled game voice was rejected.");
            yield return new WaitForSecondsRealtime(.08f); report.bgmMutedPeak = Peak(meter);
            Time.timeScale = 0; yield return new WaitForSecondsRealtime(.15f); report.pausedPeak = Peak(meter); Time.timeScale = 1;
            Require(report.audiblePeak > 1e-5f && report.bgmMutedPeak > 1e-5f, "Custom sound did not reach the game bus.");
            Require(report.seMutedPeak < 1e-8f && report.masterMutedPeak < 1e-8f && report.pausedPeak < 1e-8f, "Mute/pause leaked sound.");
            binding.Runtime.NotifyWarp(); Require(service.ActiveVoiceCount == 0, "Warp left old acoustic voices.");
            Require(Find<GenerateTunnelLifetimeScope>(scene).Regenerate(42), "Main scene regeneration failed.");
            report.builds = binding.BuildCount; report.callbackErrors = service.CallbackErrorCount;
            Require(report.builds == 2 && report.callbackErrors == 0, "Duplicate generation or DSP callback failure.");
            var empty = SceneManager.CreateScene("Custom audio unload check"); SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(scene);
            Require(service.ActiveVoiceCount == 0 && service.Play(request).Status == SpatialAudioPlayStatus.Disposed, "Unload leaked a stage voice.");
            report.passed = report.errors.Count == 0;
        }
        void Finish()
        {
            Time.timeScale = 1;
            UnityEngine.Application.logMessageReceived -= OnLog;
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "main-player-report.json"), JsonUtility.ToJson(report, true));
            UnityEngine.Application.Quit(report.passed ? 0 : 1);
        }
        void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || message.Contains("Missing DSP plugin")) report.errors.Add(message + "\n" + stack); }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static float Peak(DSP dsp)
        { dsp.getMeteringInfo(IntPtr.Zero, out DSP_METERING_INFO data); return Math.Max(data.peaklevel[0], data.peaklevel[1]); }
        static T Find<T>(Scene scene) where T : Component
        { foreach (var root in scene.GetRootGameObjects()) { var value = root.GetComponentInChildren<T>(); if (value) return value; } return null; }
    }
}
