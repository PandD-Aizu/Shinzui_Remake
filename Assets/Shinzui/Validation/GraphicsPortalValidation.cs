using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using VContainer;
using Shinzui.DI.GenerateTunnel;
using Shinzui.View;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>Opt-in native-player crossing and regeneration checks through the production presenter</summary>
public static partial class GraphicsPortalValidation
{
    /// <summary>Move the real player and camera through a portal, observing the actual warp event rather than invoking it</summary>
    /// <param name="camera">Production gameplay camera</param>
    /// <param name="target">Validation output target</param>
    /// <param name="folder">Evidence directory</param>
    /// <param name="label">Run name</param>
    /// <returns>Coroutine for rendered movement and regenerated scene evidence</returns>
    public static IEnumerator Run(Camera camera, RenderTexture target, string folder, string label)
    {
        var checks = new Dictionary<string, bool>();
        var movementFramesMs = new List<float>();
        string initialGraphics = GraphicsSnapshot(camera);
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerView>();
        var gate = TunnelGateView.ActiveGates.Where(g => g.TargetGate != null && g.PortalRT != null)
            .OrderBy(g => Vector3.Distance(g.transform.position, camera.transform.position)).FirstOrDefault();
        checks["productionPlayerAndVisibleGate"] = player != null && gate != null;
        int warps = 0;
        Vector3 observedOffset = Vector3.zero;
        Vector3 expectedOffset = gate != null ? gate.TargetGate.transform.position - gate.transform.position : Vector3.zero;
        if (player != null && gate != null)
        {
            // Let the production cooldown elapse, then freeze enemy/input simulation for deterministic movement.
            Time.timeScale = 1;
            yield return new WaitForSeconds(.35f);
            Time.timeScale = 0;
            Vector3 center = gate.Collider.bounds.center;
            center.y = camera.transform.position.y;
            Vector3 direction = center - camera.transform.position;
            direction.y = 0;
            direction.Normalize();
            camera.transform.rotation = Quaternion.LookRotation(direction);
            MoveView(player, camera, center - direction * 1.5f);
            for (int i = 0; i < 8; i++) yield return null;
            Capture(target, Path.Combine(folder, label + "-before-crossing.png"));
            // Keep synchronous PNG readback/compression out of the movement frame samples
            yield return null;
            yield return null;
            Action<Vector3> onWarp = offset => { warps++; observedOffset = offset; };
            player.Warped += onWarp;
            for (int i = 0; i < 24 && warps == 0; i++)
            {
                MoveView(player, camera, camera.transform.position + direction * .15f);
                yield return null;
                movementFramesMs.Add(Time.unscaledDeltaTime * 1000);
            }
            player.Warped -= onWarp;
            checks["productionWarpEventExactlyOnce"] = warps == 1;
            checks["warpOffsetMatchesLinkedGate"] = Vector3.Distance(observedOffset, expectedOffset) < .05f;
            for (int i = 0; i < 20; i++) yield return null;
            Capture(target, Path.Combine(folder, label + "-after-crossing.png"));
            checks["cameraAndTargetSurviveCrossing"] = Camera.main == camera && camera.targetTexture == target;
            checks["portalCamerasFiniteAfterCrossing"] = FiniteCameras();
            checks["graphicsPreservedAfterCrossing"] = GraphicsSnapshot(camera) == initialGraphics;

            // Exercise off-axis projections and crop resizing at the destination without changing authored state.
            camera.transform.rotation = Quaternion.AngleAxis(20, Vector3.up) * camera.transform.rotation;
            for (int i = 0; i < 20; i++) yield return null;
            Capture(target, Path.Combine(folder, label + "-angled.png"));
            checks["portalCamerasFiniteOffAxis"] = FiniteCameras();
        }
        var scope = UnityEngine.Object.FindAnyObjectByType<GenerateTunnelLifetimeScope>();
        checks["regenerationAccepted"] = scope != null && scope.Regenerate(9182);
        for (int i = 0; i < 30; i++) yield return null;
        checks["sixteenGatesAfterRegeneration"] = TunnelGateView.ActiveGates.Count == 16;
        checks["cameraPreservedAfterRegeneration"] = Camera.main == camera;
        checks["hdrpCameraAfterRegeneration"] = camera != null && camera.GetComponent<HDAdditionalCameraData>() != null;
        checks["portalCamerasFiniteAfterRegeneration"] = FiniteCameras();
        checks["graphicsPreservedAfterRegeneration"] = GraphicsSnapshot(camera) == initialGraphics;
        Capture(target, Path.Combine(folder, label + "-regenerated.png"));
        bool passed = checks.Values.All(value => value);
        File.WriteAllText(Path.Combine(folder, label + "-crossing.json"), JsonConvert.SerializeObject(new
        {
            passed, editor = UnityEngine.Application.isEditor, seed = 2777, regeneratedSeed = 9182,
            initialGraphics, finalGraphics = GraphicsSnapshot(camera),
            warps, observedOffset = observedOffset.ToString("F4"), expectedOffset = expectedOffset.ToString("F4"), checks,
            movementFramesMs, movementScope = "Short scripted crossing, includes changing render-target allocation; not a traversal benchmark"
        }, Formatting.Indented));
        if (!UnityEngine.Application.isEditor) UnityEngine.Application.Quit(passed ? 0 : 1);
    }

    /// <summary>Capture reproducible portal fog views and preset transitions in an opt-in development player</summary>
    /// <param name="camera">Production gameplay camera</param>
    /// <param name="target">Validation output target</param>
    /// <param name="folder">Evidence directory</param>
    /// <param name="label">Run name</param>
    /// <returns>Coroutine containing matched rendered views and effective HDRP state</returns>
    public static IEnumerator CaptureViews(Camera camera, RenderTexture target, string folder, string label)
    {
        var cases = new[]
        {
            new { name = "oblique", preset = 3, fog = -1, position = new Vector3(3, 2.2228f, 12), rotation = new Vector3(5, 25, 0) },
            new { name = "near", preset = 3, fog = -1, position = new Vector3(0, 2.2228f, 19.5f), rotation = new Vector3(0, 20, 0) },
            new { name = "reverse", preset = 3, fog = -1, position = new Vector3(3, 2.2228f, -12), rotation = new Vector3(5, 155, 0) },
            new { name = "fog-off", preset = 3, fog = 0, position = new Vector3(3, 2.2228f, 12), rotation = new Vector3(5, 25, 0) },
            new { name = "low", preset = 0, fog = -1, position = new Vector3(3, 2.2228f, 12), rotation = new Vector3(5, 25, 0) },
            new { name = "medium", preset = 1, fog = -1, position = new Vector3(3, 2.2228f, 12), rotation = new Vector3(5, 25, 0) },
            new { name = "high", preset = 2, fog = -1, position = new Vector3(3, 2.2228f, 12), rotation = new Vector3(5, 25, 0) },
            new { name = "ultra-restored", preset = 3, fog = -1, position = new Vector3(3, 2.2228f, 12), rotation = new Vector3(5, 25, 0) }
        }.Concat(UnityEngine.Object.FindObjectsByType<Transform>()
            .Where(t => t.name.StartsWith("Small Room ") && !t.name.StartsWith("Small Room Connection"))
            .OrderBy(t => t.name).Take(2).SelectMany(room => Enumerable.Range(0, 4).Select(i => new
            { name = "room-" + room.name.Replace(" ", "-") + "-" + i, preset = i, fog = i == 0 ? 0 : -1,
                position = room.position + Vector3.up * 2.2228f, rotation = room.eulerAngles + new Vector3(5, i % 2 * 180, 0) }))).ToArray();
        var results = new List<object>();
        bool passed = true;
        var flashlight = UnityEngine.Object.FindAnyObjectByType<Shinzui.View.Flashlight.FlashlightView>();
        var light = flashlight != null ? flashlight.GetComponentInChildren<Light>(true) : null;
        if (flashlight != null) { flashlight.SetLightActive(true); flashlight.enabled = false; }
        var loop = UnityEngine.Object.FindAnyObjectByType<Shinzui.DI.PlayerLifetimeScope>().Container
            .Resolve<IReadOnlyList<VContainer.Unity.IInitializable>>().OfType<Shinzui.Presentation.TunnelLoopPresenter>().Single();
        foreach (var item in cases)
        {
            var settings = new Shinzui.Domain.Settings.GraphicsSettings();
            settings.SetQualityPreset((Shinzui.Domain.Settings.GraphicsQualityPreset)item.preset);
            if (item.fog >= 0) { settings.VolumeLightQuality = item.fog; settings.QualityPreset = Shinzui.Domain.Settings.GraphicsQualityPreset.Custom; }
            if (item.name.StartsWith("room-"))
            {
                settings.ImageQualityScale = new[] { .5f, .625f, .75f, .85f }[item.preset];
                settings.QualityPreset = Shinzui.Domain.Settings.GraphicsQualityPreset.Custom;
                if (flashlight != null) flashlight.SetLightActive(item.preset % 2 == 0);
            }
            settings.EnableDepthOfField = false;
            settings.EnableVSync = false;
            settings.FrameRateLimit = 60;
            settings.ScreenMode = 1;
            new Shinzui.Infrastructure.Services.UnitySettingsApplier().ApplyGraphics(settings);
            camera.transform.SetPositionAndRotation(item.position, Quaternion.Euler(item.rotation));
            // A diagnostic camera teleport must not be interpreted as a swept gameplay portal crossing
            loop.Initialize();
            if (light != null) light.transform.SetPositionAndRotation(camera.transform.position + camera.transform.right * .2f, camera.transform.rotation);
            for (int i = 0; i < 180; i++) yield return null;
            var hd = HDCamera.GetOrCreate(camera);
            var fog = hd.volumeStack.GetComponent<UnityEngine.Rendering.HighDefinition.Fog>();
            bool valid = FiniteCameras() && Vector3.Distance(camera.transform.position, item.position) < .01f
                && Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(item.rotation)) < .1f
                && fog.enabled.value == (settings.VolumeLightQuality > 0)
                && Math.Abs(hd.actualWidth - target.width * settings.ImageQualityScale) <= 2
                && Math.Abs(hd.actualHeight - target.height * settings.ImageQualityScale) <= 2;
            passed &= valid;
            Capture(target, Path.Combine(folder, label + "-" + item.name + ".png"));
            results.Add(new { item.name, valid, settings, position = camera.transform.position.ToString("F4"), rotation = camera.transform.eulerAngles.ToString("F4"),
                internalWidth = hd.actualWidth, internalHeight = hd.actualHeight, fogEnabled = fog.enabled.value,
                volumetricFog = fog.enableVolumetricFog.value, fogQuality = fog.quality.value, graphics = GraphicsSnapshot(camera),
                portalCameras = Camera.allCameras.Where(c => c.name.Contains("Portal Camera")).Select(c => new { c.name, c.nearClipPlane, c.pixelWidth, c.pixelHeight }).ToArray() });
        }
        var roomChecks = new List<object>();
        foreach (var room in UnityEngine.Object.FindObjectsByType<Transform>().Where(t => t.Find("Walkable Room Floor") != null))
        {
            var floor = room.Find("Walkable Room Floor");
            Vector3 origin = room.TransformPoint(new Vector3(0, floor.localPosition.y + floor.localScale.y * .5f + 1, 0));
            int mask = 1 << LayerMask.NameToLayer("Stage");
            bool ceiling = Physics.Raycast(origin, room.up, 30, mask, QueryTriggerInteraction.Ignore);
            bool left = Physics.Raycast(origin, -room.right, floor.localScale.x * .5f + 1, mask, QueryTriggerInteraction.Ignore);
            bool right = Physics.Raycast(origin, room.right, floor.localScale.x * .5f + 1, mask, QueryTriggerInteraction.Ignore);
            bool forwardOpen = !Physics.Raycast(origin, room.forward, floor.localScale.z * .5f + .5f, mask, QueryTriggerInteraction.Ignore);
            bool reverseOpen = !Physics.Raycast(origin, -room.forward, floor.localScale.z * .5f + .5f, mask, QueryTriggerInteraction.Ignore);
            var path = new UnityEngine.AI.NavMeshPath();
            bool navigation = UnityEngine.AI.NavMesh.SamplePosition(origin + room.forward * (floor.localScale.z * .5f + 1), out var a, 2, UnityEngine.AI.NavMesh.AllAreas)
                && UnityEngine.AI.NavMesh.SamplePosition(origin - room.forward * (floor.localScale.z * .5f + 1), out var b, 2, UnityEngine.AI.NavMesh.AllAreas)
                && UnityEngine.AI.NavMesh.CalculatePath(a.position, b.position, UnityEngine.AI.NavMesh.AllAreas, path)
                && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
            bool valid = ceiling && left && right && forwardOpen && reverseOpen && navigation;
            passed &= valid;
            roomChecks.Add(new { room.name, valid, ceiling, left, right, forwardOpen, reverseOpen, navigation });
        }
        File.WriteAllText(Path.Combine(folder, label + "-views.json"), JsonConvert.SerializeObject(new
        { passed, editor = UnityEngine.Application.isEditor, seed = GenerateTunnelLifetimeScope.RuntimeSeedOverride, diagnosticFlashlightAligned = light != null, roomChecks, results }, Formatting.Indented));
        if (!UnityEngine.Application.isEditor) UnityEngine.Application.Quit(passed ? 0 : 1);
    }


    /// <summary>Capture effective main-camera settings to detect lifecycle resets</summary>
    /// <param name="camera">Production camera</param>
    /// <returns>Stable internal dimensions and configured rendering features</returns>
    private static string GraphicsSnapshot(Camera camera)
    {
        if (camera == null) return "missing camera";
        var hd = HDCamera.GetOrCreate(camera);
        var data = camera.GetComponent<HDAdditionalCameraData>();
        return $"{hd.actualWidth}x{hd.actualHeight};AA={data?.antialiasing};" +
            $"RT={hd.frameSettings.IsEnabled(FrameSettingsField.RayTracing)};" +
            $"AO={hd.frameSettings.IsEnabled(FrameSettingsField.SSAO)};" +
            $"Shadows={hd.frameSettings.IsEnabled(FrameSettingsField.ShadowMaps)};" +
            $"PortalScale={Shinzui.Infrastructure.Services.HdrpGraphicsRuntime.PortalScale}";
    }
    /// <summary>Move the test viewpoint without invoking the warp API or bypassing the production crossing presenter</summary>
    /// <param name="player">Real player</param>
    /// <param name="camera">Gameplay camera</param>
    /// <param name="position">Next camera position</param>
    private static void MoveView(PlayerView player, Camera camera, Vector3 position)
    {
        Vector3 delta = position - camera.transform.position;
        var controller = player.GetComponent<CharacterController>();
        bool enabled = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        bool parented = camera.transform.IsChildOf(player.transform);
        player.transform.position += delta;
        if (!parented) camera.transform.position += delta;
        if (controller != null) controller.enabled = enabled;
        Physics.SyncTransforms();
    }

    /// <summary>Detect invalid projection matrices and missing output buffers after camera lifecycle changes</summary>
    /// <returns>Whether every active portal camera has a finite projection and live texture</returns>
    private static bool FiniteCameras() => Camera.allCameras.Where(c => c.name.Contains("Portal Camera")).All(c =>
        c.targetTexture != null && c.targetTexture.IsCreated() && Enumerable.Range(0, 16).All(i =>
            !float.IsNaN(c.projectionMatrix[i]) && !float.IsInfinity(c.projectionMatrix[i])));

    /// <summary>Save the unmodified camera output for manual visual inspection</summary>
    /// <param name="target">Rendered image</param>
    /// <param name="path">PNG path</param>
    private static void Capture(RenderTexture target, string path)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous;
        UnityEngine.Object.Destroy(image);
    }
}
