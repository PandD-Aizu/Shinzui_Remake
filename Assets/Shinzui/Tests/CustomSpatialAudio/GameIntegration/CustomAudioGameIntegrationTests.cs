#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FMOD;
using FMODUnity;
using NUnit.Framework;
using Shinzui.Application.SpatialAudio;
using Shinzui.Application.CustomSpatialAudio;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Presentation.GenerateTunnel;
using VContainer;
using Shinzui.DI.CustomSpatialAudio;
using Shinzui.DI.GenerateTunnel;
using Shinzui.DI.TunnelAcoustics;
using Shinzui.Infrastructure.CustomSpatialAudio;
using Shinzui.View;
using Shinzui.View.GenerateTunnel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Shinzui.Tests.CustomSpatialAudio.GameIntegration
{
    public sealed class CustomAudioGameIntegrationTests
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public int rooms, portals, initialBuilds, footsteps, misses, processors, callbackErrors;
            public string mapAudit;
            public bool legacyBgmStarts, legacyUiStarts;
            public float audiblePeak, seMutedPeak, masterMutedPeak, bgmMutedPeak, pausedPeak;
            public List<string> errors = new List<string>();
        }
        [UnityTest]
        public IEnumerator GeneratedMainSceneRoutesCustomAudioAndSurvivesLifecycle()
        {
            var report = new Report();
            void OnLog(string condition, string stack, LogType type)
            { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(condition + "\n" + stack); }
            UnityEngine.Application.logMessageReceived += OnLog;
            LogAssert.ignoreFailingMessages = true;
            var originalScene = SceneManager.GetActiveScene();
            Scene scene = default;
            var config = Resources.Load<CustomSpatialAudioConfiguration>("SpatialAudio/CustomSpatialAudioConfiguration");
            if (!config)
            {
                config = ScriptableObject.CreateInstance<CustomSpatialAudioConfiguration>();
                config.Sounds = new[] { new CustomSpatialAudioConfiguration.DrySound {
                    Id = "footstep.prototype", Path = "CustomSpatialAudio/Audio/FootstepPrototype.wav" } };
                AssetDatabase.CreateAsset(config, "Assets/Shinzui/Audio/Resources/SpatialAudio/CustomSpatialAudioConfiguration.asset");
                AssetDatabase.SaveAssets();
            }
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Shinzui/Scenes/LatestStageGenerateTemp.unity", new LoadSceneParameters(LoadSceneMode.Additive));
                scene = SceneManager.GetSceneByPath("Assets/Shinzui/Scenes/LatestStageGenerateTemp.unity");
                SceneManager.SetActiveScene(scene);
                yield return new WaitForSecondsRealtime(2);
                var binding = Find<CustomTunnelAudioBinding>(scene);
                Assert.That(binding, Is.Not.Null);
                Assert.That(Find<TunnelAudioBinding>(scene), Is.Null, "Only one footstep backend may be attached.");
                Assert.That(binding.Runtime.World, Is.Not.Null);
                Assert.That(CustomTunnelAudioBinding.Attach(Find<TunnelMapView>(scene)), Is.SameAs(binding));
                report.initialBuilds = binding.BuildCount;
                report.rooms = binding.Runtime.World.Graph.RoomCount;
                report.portals = binding.Runtime.World.Graph.PortalCount;
                report.processors = binding.Runtime.Voices.ProcessorCount;
                Assert.That(report.initialBuilds, Is.EqualTo(1));
                var scope = Find<GenerateTunnelLifetimeScope>(scene);
                Assert.That(scope.Container.Resolve<ISpatialAudioService>(), Is.SameAs(binding.Runtime.Voices));
                var map = scope.Container.Resolve<GenerateTunnelPresenter>().CurrentMap;
                report.mapAudit = $"tunnels={map.Tunnels.Count}, corridors={map.NormalCorridors.Count}, smallRooms={map.SmallRooms.Count}, warps={map.WarpCorridors.Count}; dimensions={map.Dimensions.TunnelWidth},{map.Dimensions.TunnelHeight},{map.Dimensions.TunnelLength}";
                AssertCorridorConnections(map, binding.Runtime.World);
                var player = Find<PlayerView>(scene);
                Assert.That(player, Is.Not.Null);
                var room = binding.Runtime.World.Graph.GetRoom(0).Bounds;
                var root = Find<TunnelMapView>(scene).GeometryRoot;
                var position = root.TransformPoint(new Vector3((room.Min.X + room.Max.X) / 2, room.Min.Y + .5f, (room.Min.Z + room.Max.Z) / 2));
                player.Warp(position - player.transform.position);
                yield return new WaitForSecondsRealtime(.5f);
                var service = binding.Runtime.Voices;
                var source = player.MainCamera.transform.position + player.MainCamera.transform.right;
                var request = new SpatialAudioRequest(config.FootstepEventId, AudioPose.At(source.x, source.y, source.z));
                var bus = RuntimeManager.GetBus(config.BusPath);
                Assert.That(bus.getChannelGroup(out var group), Is.EqualTo(RESULT.OK));
                Assert.That(group.getDSP(CHANNELCONTROL_DSP_INDEX.HEAD, out var meter), Is.EqualTo(RESULT.OK));
                meter.setMeteringEnabled(false, true);
                var master = RuntimeManager.GetVCA("vca:/Master"); var se = RuntimeManager.GetVCA("vca:/SE"); var bgm = RuntimeManager.GetVCA("vca:/BGM");
                master.getVolume(out float oldMaster); se.getVolume(out float oldSe); bgm.getVolume(out float oldBgm);
                try
                {
                    var settingsApplier = new Shinzui.Infrastructure.Services.UnitySettingsApplier();
                    var audioSettings = new Shinzui.Domain.Settings.AudioSettings { SystemVolume = 1, SeVolume = 1, BgmVolume = 1 };
                    settingsApplier.ApplyAudio(audioSettings);
                    yield return new WaitForSecondsRealtime(.1f);
                    Assert.That(service.Play(request).Accepted); yield return new WaitForSecondsRealtime(.08f);
                    report.audiblePeak = Peak(meter);
                    audioSettings.SeVolume = 0; settingsApplier.ApplyAudio(audioSettings);
                    yield return new WaitForSecondsRealtime(.1f); report.seMutedPeak = Peak(meter);
                    audioSettings.SeVolume = 1; audioSettings.SystemVolume = 0; settingsApplier.ApplyAudio(audioSettings);
                    yield return new WaitForSecondsRealtime(.1f); report.masterMutedPeak = Peak(meter);
                    audioSettings.SystemVolume = 1; audioSettings.BgmVolume = 0; settingsApplier.ApplyAudio(audioSettings);
                    service.StopAll(SpatialAudioStopMode.Immediate);
                    Assert.That(service.Play(request).Accepted); yield return new WaitForSecondsRealtime(.08f); report.bgmMutedPeak = Peak(meter);
                    Time.timeScale = 0; yield return new WaitForSecondsRealtime(.15f); report.pausedPeak = Peak(meter);
                    Assert.That(report.audiblePeak, Is.GreaterThan(1e-5f));
                    Assert.That(report.seMutedPeak, Is.LessThan(1e-8f));
                    Assert.That(report.masterMutedPeak, Is.LessThan(1e-8f));
                    Assert.That(report.bgmMutedPeak, Is.GreaterThan(1e-5f));
                    Assert.That(report.pausedPeak, Is.LessThan(1e-8f));
                    Time.timeScale = 1;
                }
                finally { master.setVolume(oldMaster); se.setVolume(oldSe); bgm.setVolume(oldBgm); Time.timeScale = 1; }
                report.legacyBgmStarts = CanStartLegacy("event:/BGM/TunnelAmbient");
                report.legacyUiStarts = CanStartLegacy("event:/SE/FlashLightButtonSE");
                Assert.That(report.legacyBgmStarts && report.legacyUiStarts, "Existing BGM/UI events must remain usable.");
                binding.Runtime.NotifyWarp(); Assert.That(service.ActiveVoiceCount, Is.Zero);
                // Use the real foot-contact use case; no extra playback delay may appear after preparation.
                yield return new WaitForSecondsRealtime(.1f);
                Assert.That(binding.Runtime.Footsteps.Trigger().IsValid);
                yield return new WaitForSecondsRealtime(.1f);
                report.footsteps = binding.Runtime.Footsteps.EmittedCount; report.misses = binding.Runtime.Footsteps.MissedCount;
                Assert.That(report.footsteps, Is.GreaterThan(0));
                Assert.That(Find<GenerateTunnelLifetimeScope>(scene).Regenerate(42));
                AssertCorridorConnections(scope.Container.Resolve<GenerateTunnelPresenter>().CurrentMap, binding.Runtime.World);
                Assert.That(binding.BuildCount, Is.EqualTo(2));
                Assert.That(service.ActiveVoiceCount, Is.Zero);
                yield return null;
                report.callbackErrors = service.CallbackErrorCount;
                Assert.That(report.callbackErrors, Is.Zero);
                SceneManager.SetActiveScene(originalScene);
                yield return SceneManager.UnloadSceneAsync(scene); scene = default;
                Assert.That(service.ActiveVoiceCount, Is.Zero);
                Assert.That(service.Play(request).Status, Is.EqualTo(SpatialAudioPlayStatus.Disposed));
                Assert.That(report.errors, Is.Empty, string.Join("\n", report.errors));
                report.passed = true;
            }
            finally
            {
                Time.timeScale = 1;
                UnityEngine.Application.logMessageReceived -= OnLog;
                LogAssert.ignoreFailingMessages = false;
                Directory.CreateDirectory("Artifacts/CustomSpatialAudio/Integration");
                File.WriteAllText("Artifacts/CustomSpatialAudio/Integration/main-scene-report.json", JsonUtility.ToJson(report, true));
                if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
        }
        static T Find<T>(Scene scene) where T : Component
        { foreach (var root in scene.GetRootGameObjects()) { var value = root.GetComponentInChildren<T>(); if (value) return value; } return null; }
        static float Peak(DSP meter)
        { Assert.That(meter.getMeteringInfo(IntPtr.Zero, out DSP_METERING_INFO info), Is.EqualTo(RESULT.OK)); return Math.Max(info.peaklevel[0], info.peaklevel[1]); }
        static bool CanStartLegacy(string path)
        {
            var instance = RuntimeManager.CreateInstance(path);
            try { return instance.isValid() && instance.start() == RESULT.OK; }
            finally { instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); instance.release(); }
        }
        static void AssertCorridorConnections(TunnelMapDto map, GeneratedAcousticWorld world)
        {
            foreach (var c in map.NormalCorridors)
            {
                int cell = world.FindRoom(new AcousticVector3(c.CenterX, c.CenterY + 1, c.CenterZ)); int links = 0;
                Assert.That(cell, Is.GreaterThanOrEqualTo(0));
                for (int i = 0; i < world.Graph.PortalCount; i++)
                    if (world.Graph.GetPortal(i).RoomAId == cell || world.Graph.GetPortal(i).RoomBId == cell) links++;
                Assert.That(links, Is.EqualTo(2), "Both ends of corridor " + c.ConnectionIndex + " must be physically connected.");
            }
        }
    }
}
#endif
