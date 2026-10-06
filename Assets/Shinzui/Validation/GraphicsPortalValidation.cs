using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Shinzui.DI.GenerateTunnel;
using Shinzui.View;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>Opt-in native-player crossing and regeneration checks through the production presenter</summary>
public static class GraphicsPortalValidation
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
        Capture(target, Path.Combine(folder, label + "-regenerated.png"));
        bool passed = checks.Values.All(value => value);
        File.WriteAllText(Path.Combine(folder, label + "-crossing.json"), JsonConvert.SerializeObject(new
        {
            passed, editor = UnityEngine.Application.isEditor, seed = 2777, regeneratedSeed = 9182,
            warps, observedOffset = observedOffset.ToString("F4"), expectedOffset = expectedOffset.ToString("F4"), checks,
            movementFramesMs, movementScope = "Short scripted crossing, includes changing render-target allocation; not a traversal benchmark"
        }, Formatting.Indented));
        if (!UnityEngine.Application.isEditor) UnityEngine.Application.Quit(passed ? 0 : 1);
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
