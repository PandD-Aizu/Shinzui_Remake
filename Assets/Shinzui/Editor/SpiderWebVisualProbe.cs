using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Shinzui.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Shinzui.Editor
{
    /// <summary>Repeatable GPU captures of the stage's web. Creates an unsaved probe scene.</summary>
    public static class SpiderWebVisualProbe
    {
        private const string Stage = "Assets/Shinzui/Scenes/LatestStageGenerateTemp.unity";
        private const string Output = "app_build/SpiderWeb/results";
        private const int ImageWidth = 1600;
        private const int ImageHeight = 1200;

        public static void ApplyStageAppearance()
        {
            foreach (string scenePath in new[] { Stage, "Assets/Shinzui/Scenes/StageTemp.unity" })
            {
                var scene = EditorSceneManager.OpenScene(scenePath);
                foreach (var web in Object.FindObjectsByType<SpiderWeb>(FindObjectsSortMode.None))
                {
                    var settings = new SerializedObject(web);
                    settings.FindProperty("captureThreadWidthRatio").floatValue = 0.55f;
                    settings.FindProperty("captureThreadSag").floatValue = 0.025f;
                    settings.FindProperty("captureCurveSegments").intValue = 4;
                    settings.FindProperty("minimumThreadPixels").floatValue = 1.5f;
                    settings.FindProperty("ringPhaseVariation").floatValue = 0.18f;
                    // The older sandbox scene used 12 mm ribbons. Keep the newer
                    // stage's authored 0.5 mm silk and make the old sandbox fine too.
                    var width = settings.FindProperty("threadWidth");
                    if (width.floatValue > 0.003f) width.floatValue = 0.0015f;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    web.Regenerate();
                    EditorUtility.SetDirty(web);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        public static string CaptureStage()
        {
            EditorSceneManager.OpenScene(Stage);
            var web = Object.FindAnyObjectByType<SpiderWeb>();
            web.Regenerate();
            Bounds bounds = web.GetComponent<MeshRenderer>().bounds;
            float radius = bounds.extents.magnitude;
            var camera = new GameObject("Spider Web Review Camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.024f, 0.026f);
            camera.fieldOfView = 45;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 150;
            Vector3 viewOffset = new Vector3(0, 0.35f, -1).normalized;
            camera.transform.position = bounds.center + viewOffset * Mathf.Min(3.5f, radius * 1.6f);
            camera.transform.LookAt(bounds.center);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var light = new GameObject("Spider Web Review Flashlight").AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = radius * 8;
            light.spotAngle = 60;
            light.innerSpotAngle = 40;
            light.intensity = 8;
            light.shadows = LightShadows.Soft;
            light.transform.position = camera.transform.position + camera.transform.right * 0.3f;
            light.transform.LookAt(bounds.center);
            var images = new List<string>();
            CaptureImage(web, camera, "stage-after", images);
            return images[0];
        }

        [Serializable]
        private sealed class CaptureReport
        {
            public string label;
            public string unity;
            public string gpu;
            public int nodes;
            public int constraints;
            public int colliders;
            public int vertices;
            public int triangles;
            public float threadWidth;
            public Vector3[] anchorPositions;
            public string[] images;
            public string[] shaderErrors;
            public double meshUpdateMilliseconds;
            public long meshUpdateAllocatedBytes;
        }

        public static string Capture(string label)
        {
            if (UnityEngine.Application.isPlaying)
                throw new InvalidOperationException("Capture requires Edit mode.");
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(Stage);
            var source = Object.FindAnyObjectByType<SpiderWeb>();
            if (!source) throw new InvalidOperationException("Stage web missing.");
            string config = EditorJsonUtility.ToJson(source);
            var anchors = new Vector3[source.ManualAnchors.Count];
            for (int i = 0; i < anchors.Length; i++)
                anchors[i] = source.transform.InverseTransformPoint(source.ManualAnchors[i].position);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var webObject = new GameObject("Spider Web Visual Probe");
            webObject.SetActive(false);
            var web = webObject.AddComponent<SpiderWeb>();
            EditorJsonUtility.FromJsonOverwrite(config, web);
            var serialized = new SerializedObject(web);
            serialized.FindProperty("manualAnchors").ClearArray();
            serialized.FindProperty("attachmentSurface").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (Vector3 position in anchors)
            {
                var anchor = new GameObject("Probe Anchor");
                anchor.transform.SetParent(webObject.transform, false);
                anchor.transform.localPosition = position;
                web.AddManualAnchor(anchor.transform);
            }
            webObject.SetActive(true);
            web.Regenerate();

            Bounds bounds = new Bounds(anchors[0], Vector3.zero);
            foreach (Vector3 point in anchors) bounds.Encapsulate(point);
            Vector3 center = bounds.center;
            float extent = Mathf.Max(bounds.size.y, bounds.size.x / (ImageWidth / (float)ImageHeight));
            float distance = extent * 1.85f;

            var camera = new GameObject("Probe Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.024f, 0.026f);
            camera.fieldOfView = 36;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 100;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;

            var key = new GameObject("Probe Flashlight").AddComponent<Light>();
            key.type = LightType.Spot;
            key.range = 25;
            key.spotAngle = 68;
            key.innerSpotAngle = 40;
            key.intensity = 20;
            key.color = new Color(0.92f, 0.96f, 1f);
            key.shadows = LightShadows.Soft;
            var sun = new GameObject("Probe Directional").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.6f;
            sun.transform.rotation = Quaternion.Euler(35, -25, 0);
            sun.enabled = false;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.025f, 0.032f, 0.035f);
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.035f;
            var images = new List<string>();
            string previousPipelinePath = AssetDatabase.GetAssetPath(QualitySettings.renderPipeline);
            var pipelineClones = new List<Object>();
            try
            {
                foreach (string quality in new[] { "High", "Midium", "Low" })
                {
                    var asset = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                        "Assets/Shinzui/GraphicSettings/Quality" + quality + ".asset"));
                    asset.hideFlags = HideFlags.HideAndDontSave;
                    var assetData = new SerializedObject(asset);
                    var rendererArray = assetData.FindProperty("m_RendererDataList");
                    var renderer = Object.Instantiate((UniversalRendererData)rendererArray.GetArrayElementAtIndex(0).objectReferenceValue);
                    renderer.hideFlags = HideFlags.HideAndDontSave;
                    renderer.rendererFeatures.Clear();
                    rendererArray.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                    assetData.ApplyModifiedPropertiesWithoutUndo();
                    pipelineClones.Add(asset);
                    pipelineClones.Add(renderer);
                    QualitySettings.renderPipeline = asset;

                    camera.transform.position = center + new Vector3(0, 0, -distance);
                    camera.transform.LookAt(center);
                    key.transform.position = center + new Vector3(-extent * 0.42f, extent * 0.22f, -extent * 0.8f);
                    key.transform.LookAt(center);
                    key.enabled = true;
                    sun.enabled = false;
                    RenderSettings.fog = false;
                    CaptureImage(web, camera, label + "-" + quality + "-flash-left", images);

                    if (quality != "High") continue;
                    key.transform.position = center + new Vector3(extent * 0.42f, -extent * 0.1f, -extent * 0.8f);
                    key.transform.LookAt(center);
                    CaptureImage(web, camera, label + "-High-flash-right", images);
                    key.enabled = false;
                    CaptureImage(web, camera, label + "-High-dark", images);
                    sun.enabled = true;
                    CaptureImage(web, camera, label + "-High-directional", images);
                    sun.enabled = false;
                    key.enabled = true;
                    camera.transform.position = center + new Vector3(extent * 0.9f, 0, -distance * 0.8f);
                    camera.transform.LookAt(center);
                    CaptureImage(web, camera, label + "-High-oblique", images);
                    camera.transform.position = center + new Vector3(0, 0, -distance * 0.48f);
                    camera.transform.LookAt(center);
                    CaptureImage(web, camera, label + "-High-close", images);
                    camera.transform.position = center + new Vector3(0, 0, -distance * 1.7f);
                    camera.transform.LookAt(center);
                    RenderSettings.fog = true;
                    CaptureImage(web, camera, label + "-High-far-fog", images);
                }

                Mesh mesh = web.GetComponent<MeshFilter>().sharedMesh;
                var errors = new List<string>();
                foreach (var message in ShaderUtil.GetShaderMessages(Shader.Find("Shinzui/SpiderWeb")))
                    if (message.severity.ToString() == "Error") errors.Add(message.message);
                MethodInfo update = typeof(SpiderWeb).GetMethod("UpdateMeshGeometry", BindingFlags.NonPublic | BindingFlags.Instance);
                var updateMesh = (Action)Delegate.CreateDelegate(typeof(Action), web, update);
                for (int i = 0; i < 30; i++) updateMesh();
                var timer = new System.Diagnostics.Stopwatch();
                long allocationStart = GC.GetAllocatedBytesForCurrentThread();
                timer.Start();
                for (int i = 0; i < 300; i++) updateMesh();
                timer.Stop();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
                var report = new CaptureReport
                {
                    label = label, unity = UnityEngine.Application.unityVersion, gpu = SystemInfo.graphicsDeviceName,
                    nodes = web.NodeCount, constraints = web.ThreadCount, colliders = web.InteractionColliderCount,
                    vertices = mesh.vertexCount, triangles = mesh.triangles.Length / 3,
                    threadWidth = new SerializedObject(web).FindProperty("threadWidth").floatValue,
                    anchorPositions = anchors,
                    images = images.ToArray(), shaderErrors = errors.ToArray(),
                    meshUpdateMilliseconds = timer.Elapsed.TotalMilliseconds / 300.0,
                    meshUpdateAllocatedBytes = allocated
                };
                string json = JsonUtility.ToJson(report, true);
                File.WriteAllText(Path.Combine(Output, label + "-report.json"), json);
                if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
                return json;
            }
            finally
            {
                // A scene switch may unload a persistent asset held only by a local
                // C# reference. Reload by path rather than restoring a destroyed object.
                QualitySettings.renderPipeline = string.IsNullOrEmpty(previousPipelinePath)
                    ? null : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(previousPipelinePath);
                foreach (Object clone in pipelineClones) Object.DestroyImmediate(clone);
            }
        }

        private static void CaptureImage(SpiderWeb web, Camera camera, string name, List<string> images)
        {
            var target = new RenderTexture(ImageWidth, ImageHeight, 24, RenderTextureFormat.ARGB32)
            { hideFlags = HideFlags.HideAndDontSave };
            target.Create();
            Texture2D texture = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                typeof(SpiderWeb).GetMethod("UpdateMeshGeometry", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(web, null);
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                texture = new Texture2D(ImageWidth, ImageHeight, TextureFormat.RGB24, false)
                { hideFlags = HideFlags.HideAndDontSave };
                texture.ReadPixels(new Rect(0, 0, ImageWidth, ImageHeight), 0, 0);
                texture.Apply();
                string path = Path.Combine(Output, name + ".png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                images.Add(path);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                if (texture != null) Object.DestroyImmediate(texture);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
