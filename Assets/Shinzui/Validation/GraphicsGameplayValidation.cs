using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases;
using Shinzui.Application.UseCases.Enemy;
using Shinzui.DI;
using Shinzui.DI.GenerateTunnel;
using Shinzui.Domain.Settings;
using Shinzui.Infrastructure.Repositories;
using Shinzui.Infrastructure.Services;
using Shinzui.View;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using VContainer;

/// <summary>Opt-in production gameplay measurement with real input, simulation, autofocus and enemies</summary>
public sealed class GraphicsGameplayValidation : MonoBehaviour
{
    private Camera _camera;
    private PlayerView _player;
    private Gamepad _gamepad;
    private IInputService _input;
    private PlayerDeathUseCase _death;
    private EnemyDirectorUseCase _director;
    private readonly List<object> _samples = new();
    private readonly List<object> _observations = new();
    private readonly List<object> _warps = new();
    private readonly Dictionary<string, int> _warnings = new();
    private readonly Dictionary<string, int> _errors = new();
    private readonly FrameTiming[] _timing = new FrameTiming[1];
    private double _started;
    private int _renderedFrames;
    private bool _quitting;

    /// <summary>Enable only in an explicitly requested development Player run</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Debug.isDebugBuild || !Environment.GetCommandLineArgs().Contains("--graphics-gameplay-validation")) return;
        var host = new GameObject("Production gameplay validation");
        DontDestroyOnLoad(host);
        host.AddComponent<GraphicsGameplayValidation>();
    }

    /// <summary>Read an explicit diagnostic argument</summary>
    /// <param name="key">Argument token</param>
    /// <param name="fallback">Default value</param>
    /// <returns>Argument value or fallback</returns>
    private static string Argument(string key, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }

    /// <summary>Load production gameplay, reload saved Ultra defaults and measure normal simulation without teleporting or disabling systems</summary>
    private IEnumerator Start()
    {
        string folder = Argument("--graphics-output", Path.Combine(UnityEngine.Application.dataPath, "../GraphicsEvidence"));
        string label = Argument("--graphics-label", "player-normal-ultra");
        int seed = int.Parse(Argument("--graphics-gameplay-seed", "2777"));
        Directory.CreateDirectory(folder);
        UnityEngine.Application.logMessageReceived += ObserveLog;
        GenerateTunnelLifetimeScope.RuntimeSeedOverride = 2777;
        yield return Addressables.LoadSceneAsync("Assets/Shinzui/Scenes/StageTemp.unity");
        double deadline = Time.realtimeSinceStartupAsDouble + 30;
        while ((Camera.main == null || FindAnyObjectByType<PlayerLifetimeScope>()?.Container == null) && Time.realtimeSinceStartupAsDouble < deadline)
            yield return null;
        _camera = Camera.main;
        _player = FindAnyObjectByType<PlayerView>();
        var scope = FindAnyObjectByType<PlayerLifetimeScope>();
        if (_camera == null || _player == null || scope?.Container == null)
        {
            File.WriteAllText(Path.Combine(folder, label + "-failure.txt"), "Production player/camera did not become ready");
            UnityEngine.Application.Quit(2);
            yield break;
        }
        _input = scope.Container.Resolve<IInputService>();
        _death = scope.Container.Resolve<PlayerDeathUseCase>();
        _director = scope.Container.Resolve<EnemyDirectorUseCase>();
        bool regenerated = seed != 2777 && FindAnyObjectByType<GenerateTunnelLifetimeScope>().Regenerate(seed);
        var saved = new GameSettings();
        saved.ResetToDefault();
        saved.Graphics.SetQualityPreset(GraphicsQualityPreset.Ultra);
        var repository = new FileSettingsRepository(Path.Combine(folder, label + "-settings.json"));
        repository.Save(saved);
        var reloaded = repository.Load();
        reloaded.Graphics.Validate();
        var applier = new UnitySettingsApplier();
        // Initialize the native swap chain before restoring the exact saved fullscreen mode
        var initialWindow = reloaded.Graphics.Clone();
        initialWindow.ScreenMode = 1;
        applier.ApplyGraphics(initialWindow);
        yield return null;
        yield return null;
        applier.ApplyGraphics(reloaded.Graphics);
        // Supply ordinary input without changing the production actions or focus policy
        InputSystem.RegisterLayout("{\"name\":\"GraphicsValidationGamepad\",\"extend\":\"Gamepad\",\"canRunInBackground\":true}");
        _gamepad = (Gamepad)InputSystem.AddDevice("GraphicsValidationGamepad");
        RenderPipelineManager.endCameraRendering += CountFrame;
        _player.Warped += ObserveWarp;
        // Applying a different quality asset recreates HDRP and its volume manager asynchronously
        deadline = Time.realtimeSinceStartupAsDouble + 20;
        while ((!VolumeManager.instance.isInitialized || _renderedFrames < 3) && Time.realtimeSinceStartupAsDouble < deadline)
            yield return null;
        if (!VolumeManager.instance.isInitialized || _renderedFrames < 3)
        {
            File.WriteAllText(Path.Combine(folder, label + "-failure.txt"), "Production rendering did not become ready after the display/pipeline transition");
            UnityEngine.Application.Quit(3);
            yield break;
        }
        _started = Time.realtimeSinceStartupAsDouble;
        int lastRendered = 0;
        double nextObservation = 0;
        float distance = 0;
        Vector3 lastPosition = _player.transform.position;
        bool wasDead = false;
        while (Time.realtimeSinceStartupAsDouble - _started < 45)
        {
            double elapsed = Time.realtimeSinceStartupAsDouble - _started;
            bool dead = _death.IsDead.CurrentValue;
            // Warm up normally, then walk down the tunnel off-center with brief ordinary sprints
            Vector3 desired = new Vector3(Mathf.Clamp((1.6f - _player.transform.position.x) * .7f, -.6f, .6f), 0, 1).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(_camera.transform.right, Vector3.up).normalized;
            var movement = elapsed >= 5 && !dead ? new Vector2(Vector3.Dot(desired, right), Vector3.Dot(desired, forward)) : Vector2.zero;
            var state = new GamepadState { leftStick = movement, leftTrigger = elapsed >= 5 && elapsed % 10 < 3 ? 1 : 0 };
            if (elapsed >= 5 && elapsed < 5.1) state = state.WithButton(GamepadButton.North);
            InputSystem.QueueStateEvent(_gamepad, state);
            yield return null;
            FrameTimingManager.CaptureFrameTimings();
            uint timings = FrameTimingManager.GetLatestTimings(1, _timing);
            Vector3 position = _player.transform.position;
            float step = Vector3.Distance(position, lastPosition);
            if (step < 5) distance += step;
            lastPosition = position;
            if (elapsed >= 5 && _renderedFrames > 120 && _renderedFrames != lastRendered && !dead)
                _samples.Add(new { frame = Time.frameCount, seconds = elapsed, wallMs = Time.unscaledDeltaTime * 1000,
                    cpuMs = timings > 0 ? _timing[0].cpuFrameTime : 0, gpuMs = timings > 0 ? _timing[0].gpuFrameTime : 0 });
            lastRendered = _renderedFrames;
            if (elapsed >= nextObservation)
            {
                Observe(elapsed);
                nextObservation = elapsed + 1;
            }
            if (dead) { wasDead = true; break; }
        }
        InputSystem.QueueStateEvent(_gamepad, new GamepadState());
        Observe(Time.realtimeSinceStartupAsDouble - _started);
        var hd = HDCamera.GetOrCreate(_camera);
        var dof = hd.volumeStack.GetComponent<DepthOfField>();
        var brain = _camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        File.WriteAllText(Path.Combine(folder, label + ".json"), JsonConvert.SerializeObject(new
        {
            gpu = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceType.ToString(), width = Screen.width, height = Screen.height,
            refreshHz = Screen.currentResolution.refreshRateRatio.value, mode = Screen.fullScreenMode.ToString(),
            editor = false, developmentBuild = Debug.isDebugBuild, settings = reloaded.Graphics, settingsSavedAndReloaded = true,
            isolatedSettingsFile = true, seed, regenerated, timeScale = Time.timeScale, frozenGameplay = false,
            dofMode = dof.focusMode.value.ToString(), dofActive = dof.IsActive(), focusDistance = dof.focusDistance.value,
            cinemachineEnabled = brain != null && brain.enabled, autofocusComponents = FindObjectsByType<DynamicPostProcessView>().Count(v => v.isActiveAndEnabled),
            pipelineRT = (RenderPipelineManager.currentPipeline as HDRenderPipeline)?.rayTracingSupported,
            rtFrame = hd.frameSettings.IsEnabled(FrameSettingsField.RayTracing), capability = HdrpGraphicsRuntime.CapabilityReport(),
            renderedFrames = _renderedFrames, validRenderWorkload = _renderedFrames > 120 && _samples.Count >= 300,
            durationSeconds = Time.realtimeSinceStartupAsDouble - _started, warmupSeconds = 5, wasDead, walkedDistanceMetres = distance,
            inputMethod = "Virtual gamepad through production InputActions; collision, stamina, enemies, death and camera logic unchanged",
            samples = _samples, observations = _observations, warps = _warps, warnings = _warnings, errors = _errors
        }, Formatting.Indented));
        // Screenshot encoding occurs after the final performance sample
        yield return new WaitForEndOfFrame();
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(folder, label + ".png"), texture.EncodeToPNG());
        Destroy(texture);
        UnityEngine.Application.Quit(_samples.Count >= 300 ? 0 : 3);
    }

    /// <summary>Observe production state at low frequency without modifying it</summary>
    /// <param name="elapsed">Elapsed wall seconds</param>
    private void Observe(double elapsed)
    {
        if (!VolumeManager.instance.isInitialized || _renderedFrames == 0) return;
        var hd = HDCamera.GetOrCreate(_camera);
        var dof = hd.volumeStack.GetComponent<DepthOfField>();
        var enemies = FindObjectsByType<EnemyView>();
        _observations.Add(new { seconds = elapsed, scaledTime = Time.time, timeScale = Time.timeScale,
            position = _player.transform.position.ToString("F3"), cameraPosition = _camera.transform.position.ToString("F3"),
            velocity = _player.CurrentVelocity.magnitude, moveInput = _input.MoveInput.ToString("F3"), dead = _death.IsDead.CurrentValue,
            dofMode = dof.focusMode.value.ToString(), dofActive = dof.IsActive(), focusDistance = dof.focusDistance.value,
            directorPhase = _director.Phase.ToString(), pressure = _director.Pressure,
            enemies = enemies.Select(e => { var a = e.ResolveAgent(); return new { e.name, active = e.isActiveAndEnabled,
                position = e.EnemyPosition.ToString("F3"), agentEnabled = a != null && a.enabled,
                onNavMesh = a != null && a.enabled && a.isOnNavMesh,
                speed = a != null && a.enabled && a.isOnNavMesh ? a.velocity.magnitude : 0 }; }).ToArray() });
    }

    /// <summary>Count actual rendered production frames</summary>
    /// <param name="context">Render context</param>
    /// <param name="camera">Rendered camera</param>
    private void CountFrame(ScriptableRenderContext context, Camera camera) { if (camera == _camera) _renderedFrames++; }

    /// <summary>Record production portal crossings without invoking Warp</summary>
    /// <param name="offset">Observed production warp offset</param>
    private void ObserveWarp(Vector3 offset) => _warps.Add(new { seconds = Time.realtimeSinceStartupAsDouble - _started, offset = offset.ToString("F3") });

    /// <summary>Count runtime diagnostics without flooding the evidence file</summary>
    /// <param name="message">Runtime diagnostic</param>
    /// <param name="stack">Original stack</param>
    /// <param name="type">Severity</param>
    private void ObserveLog(string message, string stack, LogType type)
    {
        var destination = type == LogType.Warning ? _warnings : type == LogType.Error || type == LogType.Exception || type == LogType.Assert ? _errors : null;
        if (destination == null) return;
        if (destination.TryGetValue(message, out int count)) destination[message] = count + 1;
        else if (destination.Count < 30) destination[message] = 1;
    }

    /// <summary>Leave device disposal to InputSystem once application teardown begins</summary>
    private void OnApplicationQuit() => _quitting = true;

    /// <summary>Remove diagnostic callbacks and dispose the device only while the input system is live</summary>
    private void OnDestroy()
    {
        RenderPipelineManager.endCameraRendering -= CountFrame;
        UnityEngine.Application.logMessageReceived -= ObserveLog;
        if (_player != null) _player.Warped -= ObserveWarp;
        if (!_quitting && _gamepad != null && _gamepad.added) InputSystem.RemoveDevice(_gamepad);
    }
}
