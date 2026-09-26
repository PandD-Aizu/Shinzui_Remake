using System.IO;
using Shinzui.Infrastructure.Animation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Editor.SpiderDeity
{
    /// <summary>Captures the saved preview through the real Unity rig and renderer.</summary>
    public static class SpiderDeityIkCapture
    {
        public static void Capture()
        {
            EditorSceneManager.OpenScene(SpiderDeityIkAssetBuilder.PreviewScenePath);
            var motion = Object.FindAnyObjectByType<SpiderLegIkPreviewMotion>();
            motion.enabled = false;
            var root = motion.gameObject;
            root.GetComponent<SpiderDeityIkRig>().InitializeRig();
            var animator = root.GetComponent<Animator>();
            animator.Rebind();
            animator.Update(0f);
            var rig = root.GetComponent<RigBuilder>();
            rig.Build();
            rig.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var constraints = root.GetComponentsInChildren<ChainIKConstraint>();
            for (int i = 0; i < constraints.Length; i++)
                if (i % 2 == 0) constraints[i].data.target.position += new Vector3(0, 0.06f, 0.02f);
            animator.Update(1f / 60f);
            rig.Evaluate(1f / 60f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.43f);
            var camera = Camera.main;
            var target = new RenderTexture(1000, 1000, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var image = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
            try
            {
                target.Create();
                camera.targetTexture = target;
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Artifacts/SpiderDeityIK");
                File.WriteAllBytes("Artifacts/SpiderDeityIK/unity_ik_preview.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                rig.Clear();
                Object.DestroyImmediate(image);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
