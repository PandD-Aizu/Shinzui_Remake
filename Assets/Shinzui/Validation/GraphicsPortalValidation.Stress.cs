using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Shinzui.View;
using UnityEngine;
using VContainer;

/// <summary>Frame-by-frame visual regression evidence, separate from production performance measurements</summary>
public static partial class GraphicsPortalValidation
{
    /// <summary>Capture near-plane intersections, recursive views, screen edges, fast turns and production crossings in both directions</summary>
    /// <param name="camera">Production camera with diagnostic autofocus already disabled</param>
    /// <param name="target">Full-resolution screenshot target</param>
    /// <param name="folder">Local evidence directory</param>
    /// <param name="label">Run identifier</param>
    /// <returns>Rendered diagnostic sequence, with no FPS claims from screenshot frames</returns>
    public static IEnumerator CaptureStress(Camera camera, RenderTexture target, string folder, string label)
    {
        var frames = new List<object>();
        var checks = new Dictionary<string, bool>();
        var errors = new List<string>();
        Application.LogCallback onLog = (message, stack, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
        };
        Application.logMessageReceived += onLog;
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerView>();
        var loop = UnityEngine.Object.FindAnyObjectByType<Shinzui.DI.PlayerLifetimeScope>().Container
            .Resolve<IReadOnlyList<VContainer.Unity.IInitializable>>().OfType<Shinzui.Presentation.TunnelLoopPresenter>().Single();
        var flashlight = UnityEngine.Object.FindAnyObjectByType<Shinzui.View.Flashlight.FlashlightView>();
        var light = flashlight != null ? flashlight.GetComponentInChildren<Light>(true) : null;
        if (flashlight != null) { flashlight.SetLightActive(true); flashlight.enabled = false; }
        for (int i = 0; i < 90; i++) yield return null;
        var gate = TunnelGateView.ActiveGates.Where(g => g.TargetGate != null && g.PortalRT != null)
            .OrderBy(g => Vector3.Distance(g.Collider.bounds.center, camera.transform.position)).First();
        Vector3 direction = gate.Collider.bounds.center - camera.transform.position;
        direction.y = 0;
        direction.Normalize();
        float height = camera.transform.position.y;
        Vector3 center = gate.Collider.bounds.center;
        center.y = height;
        Vector3 reverseCenter = gate.TargetGate.Collider.bounds.center;
        reverseCenter.y = height;
        Quaternion forward = Quaternion.LookRotation(direction);
        Quaternion reverse = Quaternion.LookRotation(-direction);
        string originalGraphics = GraphicsSnapshot(camera);
        int index = 0;

        // Warm each stationary pose before inspecting both near-plane sides and recursive surfaces
        var poses = new[]
        {
            new { name="near-forward", position=center-direction*.35f, rotation=forward },
            new { name="near-reverse", position=reverseCenter+direction*.35f, rotation=reverse },
            new { name="recursive-forward", position=center-direction*5, rotation=forward },
            new { name="recursive-reverse", position=reverseCenter+direction*5, rotation=reverse }
        };
        foreach (var pose in poses)
        {
            camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            loop.Initialize();
            AlignStressLight(light, camera);
            for (int i = 0; i < 45; i++) yield return null;
            yield return new WaitForEndOfFrame();
            RecordStressFrame(camera, target, folder, label, pose.name, index++, frames);
            checks[pose.name] = FiniteCameras() && TunnelGateView.ActiveGates.Any(g => g.HdrpActiveCameraCount >= 2);
        }

        // Keep consecutive output frames: a stable endpoint alone cannot expose a one-frame dropout
        foreach (bool backward in new[] { false, true })
        {
            Vector3 origin = backward ? reverseCenter : center;
            Vector3 heading = backward ? -direction : direction;
            Quaternion facing = Quaternion.LookRotation(heading);
            camera.transform.SetPositionAndRotation(origin-heading*2, facing);
            loop.Initialize();
            for (int i=0;i<30;i++) yield return null;
            foreach (float yaw in new float[] { 0, 40, 60, 75, 85, 100, 85, 75, 60, 40, 0, -40, -60, -75, -85, -100, -85, -75, -60, -40, 0 })
            {
                camera.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * facing;
                loop.Initialize();
                AlignStressLight(light, camera);
                yield return null;
                yield return new WaitForEndOfFrame();
                RecordStressFrame(camera,target,folder,label,backward ? "edge-reverse" : "edge-forward",index++,frames);
            }
            camera.transform.position = origin-heading*.35f;
            foreach (float yaw in new float[] { 0, 90, 180, 270, 360, 180, 0 })
            {
                camera.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * facing;
                loop.Initialize();
                AlignStressLight(light,camera);
                yield return null;
                yield return new WaitForEndOfFrame();
                RecordStressFrame(camera,target,folder,label,backward ? "turn-reverse" : "turn-forward",index++,frames);
            }
        }

        // Observe actual production warp callbacks; do not call Warp or replace its collision/cooldown logic
        foreach (bool backward in new[] { false, true })
        {
            Time.timeScale=1;
            yield return new WaitForSeconds(.4f);
            Time.timeScale=0;
            var source = backward ? gate.TargetGate : gate;
            Vector3 heading=backward ? -direction : direction;
            Vector3 start=source.Collider.bounds.center;
            start.y=height;
            camera.transform.rotation=Quaternion.LookRotation(heading);
            MoveView(player,camera,start-heading*.8f);
            loop.Initialize();
            int warps=0;
            Action<Vector3> observe=offset=>warps++;
            player.Warped+=observe;
            for(int step=0;step<20;step++)
            {
                MoveView(player,camera,camera.transform.position+heading*.1f);
                AlignStressLight(light,camera);
                yield return null;
                yield return new WaitForEndOfFrame();
                RecordStressFrame(camera,target,folder,label,backward ? "cross-reverse" : "cross-forward",index++,frames);
            }
            player.Warped-=observe;
            checks[backward ? "reverseProductionWarp" : "forwardProductionWarp"]=warps==1;
        }
        checks["graphicsPreserved"]=originalGraphics==GraphicsSnapshot(camera);
        checks["finiteFinalCameras"]=FiniteCameras();
        checks["noCapturedErrors"]=errors.Count==0;
        Application.logMessageReceived-=onLog;
        bool passed=checks.Values.All(value=>value);
        File.WriteAllText(Path.Combine(folder,label+"-stress.json"),JsonConvert.SerializeObject(new
        {
            passed,checks,errors,frames,nearPlaneCull=TunnelGateView.HdrpNearPlaneCullEnabled,
            cropQuantum=TunnelGateView.HdrpCropQuantum,
            scope="Full-resolution native screenshots; scripted frozen camera poses and real warp callbacks; screenshot frames are not performance measurements"
        },Formatting.Indented));
        Application.Quit(passed ? 0 : 1);
    }

    /// <summary>Keep the existing flashlight aligned with each diagnostic camera pose</summary>
    /// <param name="light">Existing flashlight source</param>
    /// <param name="camera">Diagnostic viewpoint</param>
    private static void AlignStressLight(Light light,Camera camera)
    {
        if(light!=null) light.transform.SetPositionAndRotation(camera.transform.position+camera.transform.right*.2f,camera.transform.rotation);
    }

    /// <summary>Save one rendered frame and the corresponding camera/projection state</summary>
    /// <param name="camera">Production camera</param>
    /// <param name="target">Rendered image</param>
    /// <param name="folder">Local directory</param>
    /// <param name="label">Run name</param>
    /// <param name="scenario">Sequence identifier</param>
    /// <param name="index">Ordered frame number</param>
    /// <param name="frames">Evidence records</param>
    private static void RecordStressFrame(Camera camera,RenderTexture target,string folder,string label,string scenario,int index,List<object> frames)
    {
        string file=$"{label}-{index:D3}-{scenario}.png";
        Capture(target,Path.Combine(folder,file));
        frames.Add(new {index,scenario,file,position=camera.transform.position.ToString("F4"),rotation=camera.transform.eulerAngles.ToString("F4"),finite=FiniteCameras(),
            activePortalCameras=TunnelGateView.ActiveGates.Sum(g=>g.HdrpActiveCameraCount)});
    }
}
