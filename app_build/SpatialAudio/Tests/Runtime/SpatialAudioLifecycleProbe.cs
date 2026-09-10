using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FMODUnity;
using Shinzui.Application.SpatialAudio;
using Shinzui.AudioProbe.Infrastructure;
using Shinzui.DI.SpatialAudio;
using Shinzui.Infrastructure.SpatialAudio;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Shinzui.SpatialAudio.Tests
{
    public sealed class SpatialAudioLifecycleProbe : MonoBehaviour
    {
        public SpatialAudioCatalog Catalog;
        public static bool Completed { get; private set; }
        public static bool Passed { get; private set; }
        [Serializable] public sealed class CheckResult { public string name; public bool passed; public string detail; }
        [Serializable] sealed class Report
        {
            public bool passed;
            public string environment, unityVersion;
            public int createdInstances, releasedInstances;
            public List<CheckResult> checks;
        }
        readonly List<CheckResult> checks = new List<CheckResult>();
        FmodSpatialAudioService service;
        SpatialAudioLifetimeScope scope;
        ProbeCapture capture;
        bool failed;
        string output;
        float started;

        void Awake()
        {
            Completed = Passed = false;
            output = Argument("-spatialAudioOutput") ?? Path.Combine(UnityEngine.Application.persistentDataPath, "SpatialAudioProbe");
            Directory.CreateDirectory(output);
            UnityEngine.Application.logMessageReceived += OnLog;
            started = Time.realtimeSinceStartup;
        }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2);
            capture = new ProbeCapture(RuntimeManager.CoreSystem);
            var ownedScene = SceneManager.CreateScene("Spatial Audio Scope Lifetime Test");
            var scopeObject = new GameObject("Spatial Audio Test Scope");
            scopeObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(scopeObject, ownedScene);
            scope = scopeObject.AddComponent<SpatialAudioLifetimeScope>();
            scope.Catalog = Catalog; scope.MaxVoices = 3;
            scopeObject.SetActive(true);
            yield return null;
            service = scope.Container.Resolve<FmodSpatialAudioService>();
            var api = scope.Container.Resolve<ISpatialAudioService>();
            Check("di_resolves_same_service", ReferenceEquals(api, service));
            Check("unknown_event_rejected", api.Play(Request("missing")).Status == SpatialAudioPlayStatus.UnknownEvent);
            Check("invalid_pose_rejected", api.Play(new SpatialAudioRequest("pulse", default)).Status == SpatialAudioPlayStatus.InvalidRequest);
            Check("nonfinite_volume_rejected", api.Play(new SpatialAudioRequest("pulse", AudioPose.At(0,1.5f,1.5f), volume: float.NaN)).Status == SpatialAudioPlayStatus.InvalidRequest);

            capture.Begin();
            var first = api.Play(Request("pulse", -1));
            var second = api.Play(Request("pulse", 1));
            Check("same_event_has_distinct_handles", first.Accepted && second.Accepted && !first.Handle.Equals(second.Handle) && api.ActiveVoiceCount == 2);
            yield return Ready(first.Handle);
            bool inspectedFirst = service.TryInspect(first.Handle, out var firstInfo);
            bool inspectedSecond = service.TryInspect(second.Handle, out var secondInfo);
            Check("independent_native_instances", inspectedFirst && inspectedSecond && firstInfo.NativeInstance != secondInfo.NativeInstance);
            Check("two_positions_and_spatializers", firstInfo.SpatializerReady && secondInfo.SpatializerReady &&
                Near(firstInfo.Position, new Vector3(-1,1.5f,1.5f)) && Near(secondInfo.Position, new Vector3(1,1.5f,1.5f)));
            yield return new WaitForSecondsRealtime(2);
            Check("short_sound_retains_tail_ownership", api.GetState(first.Handle) == SpatialVoiceState.Playing && api.ActiveVoiceCount == 2);
            yield return new WaitForSecondsRealtime(4.5f);
            float peak = capture.Finish(Path.Combine(output, "two-voices-tail.wav"));
            Check("recorded_two_voices", peak > .001f && !capture.Overflowed, "peak=" + peak);
            // FMOD may keep the event alive beyond its timeline while the Spatializer drains its tail.
            yield return Stopped(first.Handle);
            yield return Stopped(second.Handle);
            Check("natural_completion_releases_instances", api.ActiveVoiceCount == 0 && service.CreatedInstanceCount == service.ReleasedInstanceCount);

            var target = new GameObject("Tracked Audio Target");
            target.transform.position = new Vector3(-1,1.5f,1.5f);
            var following = api.Play(Request("loop", -1), new TransformAudioPoseSource(target.transform, true));
            yield return Ready(following.Handle);
            service.TryInspect(following.Handle, out var reuseInfo);
            Check("pool_object_reused", reuseInfo.PoolObject == firstInfo.PoolObject && service.PoolSize == 2);
            Check("stale_handle_cannot_control_reused_voice", !api.Stop(first.Handle) && !api.UpdatePose(first.Handle, AudioPose.At(20,0,0)) && !api.SetVolume(first.Handle, 0));
            capture.Begin();
            yield return new WaitForSecondsRealtime(.5f);
            bool sawVelocity = false;
            for (int frame = 0; frame < 30; frame++)
            {
                target.transform.position += new Vector3(.05f,0,0);
                yield return null;
                if (service.TryInspect(following.Handle, out var motion) && motion.Velocity.x > .01f) sawVelocity = true;
            }
            RuntimeManager.StudioSystem.flushCommands();
            service.TryInspect(following.Handle, out var moved);
            Check("transform_follow_updates_native_position", Near(moved.Position, target.transform.position));
            Check("velocity_reaches_fmod", sawVelocity);
            api.SetPaused(following.Handle, true);
            yield return new WaitForSecondsRealtime(.15f);
            service.TryInspect(following.Handle, out var pausedAt);
            yield return new WaitForSecondsRealtime(.4f);
            service.TryInspect(following.Handle, out var stillPaused);
            Check("pause_freezes_timeline", api.GetState(following.Handle) == SpatialVoiceState.Paused && Math.Abs(pausedAt.TimelineMilliseconds - stillPaused.TimelineMilliseconds) < 30);
            api.SetPaused(following.Handle, false);
            yield return new WaitForSecondsRealtime(.35f);
            service.TryInspect(following.Handle, out var resumed);
            Check("resume_advances_timeline", api.GetState(following.Handle) == SpatialVoiceState.Playing && resumed.TimelineMilliseconds != stillPaused.TimelineMilliseconds);
            yield return new WaitForSecondsRealtime(2.2f);
            Check("authored_loop_remains_alive", api.GetState(following.Handle) == SpatialVoiceState.Playing);
            Check("moving_loop_is_audible", capture.Finish(Path.Combine(output, "moving-loop.wav")) > .001f && !capture.Overflowed);
            UnityEngine.Object.Destroy(target);
            yield return new WaitForSecondsRealtime(.2f);
            Check("destroyed_follow_target_releases_voice", api.GetState(following.Handle) == SpatialVoiceState.Stopped && api.ActiveVoiceCount == 0);

            var low = api.Play(Request("loop", -1, 10));
            var middle = api.Play(Request("loop", 0, 20));
            var high = api.Play(Request("loop", 1, 30));
            Check("voice_limit_rejects_equal_priority", api.Play(Request("loop", 0, 10)).Status == SpatialAudioPlayStatus.VoiceLimit && api.ActiveVoiceCount == 3);
            var replacement = api.Play(Request("loop", 0, 40));
            Check("higher_priority_replaces_lowest", replacement.Accepted && api.ActiveVoiceCount == 3 && api.GetState(low.Handle) == SpatialVoiceState.Stopped &&
                api.GetState(middle.Handle) != SpatialVoiceState.Stopped && api.GetState(high.Handle) != SpatialVoiceState.Stopped);
            yield return Ready(replacement.Handle);
            Check("voice_limit_includes_paused_voice", api.SetPaused(middle.Handle, true) && api.Play(Request("loop", 0, 20)).Status == SpatialAudioPlayStatus.VoiceLimit);
            Check("invalid_update_preserves_voice", !api.UpdatePose(high.Handle, default) && !api.SetVolume(high.Handle, float.PositiveInfinity) && api.GetState(high.Handle) != SpatialVoiceState.Stopped);
            api.Stop(replacement.Handle, SpatialAudioStopMode.AllowFadeOut);
            Check("fadeout_keeps_handle_while_stopping", api.GetState(replacement.Handle) == SpatialVoiceState.Stopping);
            yield return Stopped(replacement.Handle);
            Check("fadeout_releases_when_fmod_stops", api.GetState(replacement.Handle) == SpatialVoiceState.Stopped);
            api.StopAll(SpatialAudioStopMode.Immediate);
            Check("immediate_stop_clears_all", api.ActiveVoiceCount == 0);

            var caller = new SpatialAudioUseCase(api);
            var owned = caller.Play(Request("loop"));
            var unrelated = api.Play(Request("loop", 1));
            caller.Dispose(); caller.Dispose();
            Check("usecase_disposes_only_owned_voices", api.GetState(owned.Handle) == SpatialVoiceState.Stopped && api.GetState(unrelated.Handle) != SpatialVoiceState.Stopped);
            Check("disposed_usecase_rejects_play", caller.Play(Request("pulse")).Status == SpatialAudioPlayStatus.Disposed);
            api.StopAll(SpatialAudioStopMode.Immediate);
            for (int i = 0; i < 30; i++)
            {
                var burst = api.Play(Request("pulse"));
                Check("repeat_play_stop_" + i, burst.Accepted && api.Stop(burst.Handle, SpatialAudioStopMode.Immediate));
                yield return null;
            }
            Check("pool_is_bounded_after_repeated_play", service.PoolSize == 3 && api.ActiveVoiceCount == 0 && service.CreatedInstanceCount == service.ReleasedInstanceCount);
            var finalVoice = api.Play(Request("loop"));
            yield return Ready(finalVoice.Handle);
            var pendingVoice = api.Play(Request("pulse"));
            Check("scene_contains_playing_and_preparing", api.GetState(finalVoice.Handle) == SpatialVoiceState.Playing && api.GetState(pendingVoice.Handle) == SpatialVoiceState.Preparing);
            yield return SceneManager.UnloadSceneAsync(ownedScene);
            yield return new WaitForSecondsRealtime(.3f);
            RuntimeManager.StudioSystem.flushCommands();
            Check("scene_unload_disposes_service", api.ActiveVoiceCount == 0 && api.Play(Request("pulse")).Status == SpatialAudioPlayStatus.Disposed);
            Check("scene_unload_destroys_pool", service.PoolSize == 0 && UnityEngine.Object.FindObjectsByType<ManagedSpatialEmitter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0);
            bool noInstances = true;
            foreach (var entry in Catalog.Entries)
            {
                var description = RuntimeManager.GetEventDescription(entry.Event);
                noInstances &= description.getInstanceCount(out int count) == FMOD.RESULT.OK && count == 0;
            }
            Check("fmod_has_no_remaining_event_instances", noInstances);
            Check("all_owned_instances_released", service.CreatedInstanceCount == service.ReleasedInstanceCount);
            Check("no_backend_failures", service.FailureCount == 0, service.LastFailure);
            service.Dispose();
            Finish();
        }

        IEnumerator Ready(SpatialVoiceHandle handle)
        {
            float deadline = Time.realtimeSinceStartup + 6;
            while (service.GetState(handle) == SpatialVoiceState.Preparing && Time.realtimeSinceStartup < deadline) yield return null;
            Check("voice_ready_" + checks.Count, service.GetState(handle) == SpatialVoiceState.Playing);
        }
        IEnumerator Stopped(SpatialVoiceHandle handle)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (service.GetState(handle) != SpatialVoiceState.Stopped && Time.realtimeSinceStartup < deadline) yield return null;
            Check("voice_stopped_" + checks.Count, service.GetState(handle) == SpatialVoiceState.Stopped);
        }
        static SpatialAudioRequest Request(string id, float x = 0, int priority = 128) =>
            new SpatialAudioRequest(id, AudioPose.At(x,1.5f,1.5f), SpatialAudioCategory.World, priority);
        static bool Near(Vector3 a, Vector3 b) => (a-b).sqrMagnitude < .02f;
        void Check(string name, bool passed, string detail = null)
        {
            checks.Add(new CheckResult { name = name, passed = passed, detail = detail });
            if (!passed) failed = true;
            Debug.Log("[SpatialAudioProbe] " + name + "=" + passed + " " + detail);
        }
        void Update()
        {
            if (!Completed && Time.realtimeSinceStartup - started > 100)
            { failed = true; Debug.LogError("[SpatialAudioProbe] Timed out."); Finish(); }
        }
        void OnLog(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true; }
        void Finish()
        {
            if (Completed) return;
            capture?.Dispose(); capture = null;
            Passed = !failed && checks.Count >= 60;
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(new Report {
                passed = Passed, environment = UnityEngine.Application.isEditor ? "Editor Play Mode (CLI)" : "Windows IL2CPP Player",
                unityVersion = UnityEngine.Application.unityVersion, checks = checks,
                createdInstances = service?.CreatedInstanceCount ?? 0, releasedInstances = service?.ReleasedInstanceCount ?? 0 }, true));
            Completed = true;
            Debug.Log("[SpatialAudioProbe] Completed=" + Passed);
            if (!UnityEngine.Application.isEditor && Argument("-spatialAudioOutput") != null) UnityEngine.Application.Quit(Passed ? 0 : 1);
        }
        void OnDestroy() { UnityEngine.Application.logMessageReceived -= OnLog; capture?.Dispose(); }
        static string Argument(string key)
        { var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key); return i >= 0 && i+1 < args.Length ? args[i+1] : null; }
    }
}
