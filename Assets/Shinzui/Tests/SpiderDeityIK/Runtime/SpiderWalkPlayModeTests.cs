#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.IO;
using NUnit.Framework;
using Shinzui.Infrastructure.Animation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Tests.SpiderDeityIK.Runtime
{
    public sealed class SpiderWalkPlayModeTests
    {
        private Scene scene;
        private SpiderProceduralWalk walk;
        private ChainIKConstraint[] legs;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            const string path = "Assets/Shinzui/Scenes/SpiderDeityWalkPreview.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(path);
            walk = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SpiderProceduralWalk>()).Single();
            walk.GetComponent<SpiderWalkPreviewMotion>().enabled = false;
            legs = walk.GetComponentsInChildren<ChainIKConstraint>();
            walk.ResetContacts();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlantedFeetStayInWorldSpaceWhileBodyMoves()
        {
            var positions = legs.Select(x => x.data.target.position).ToArray();
            walk.transform.position += Vector3.forward * 0.02f;
            yield return null;
            yield return null;
            Assert.That(walk.MovingFootCount, Is.Zero);
            for (int i = 0; i < legs.Length; i++)
            {
                Assert.That(Vector3.Distance(positions[i], legs[i].data.target.position), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(legs[i].data.tip.position, positions[i]), Is.LessThan(0.003f));
            }
        }

        [UnityTest]
        public IEnumerator WalkingAlternatesSupportsAndSettlesOnGround()
        {
            int maximum = 0;
            float end = Time.time + 3f;
            while (Time.time < end)
            {
                walk.transform.position += Vector3.forward * (Time.deltaTime * 0.09f);
                yield return null;
                maximum = Mathf.Max(maximum, walk.MovingFootCount);
                Assert.That(walk.MovingFootCount, Is.LessThanOrEqualTo(4));
                foreach (var leg in legs)
                    Assert.That(Vector3.Distance(leg.data.tip.position, leg.data.target.position), Is.LessThan(0.012f));
            }
            yield return new WaitForSeconds(1f);
            Assert.That(maximum, Is.GreaterThan(0));
            Assert.That(walk.CompletedSteps, Is.GreaterThanOrEqualTo(8));
            Assert.That(walk.MovingFootCount, Is.Zero);
            foreach (var leg in legs) Assert.That(leg.data.target.position.y, Is.EqualTo(-0.001f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator PreviewMovesTurnsAndRendersWithAlternatingFeet()
        {
            var preview = walk.GetComponent<SpiderWalkPreviewMotion>();
            preview.enabled = true;
            var initial = walk.transform.position;
            float end = Time.time + 7f;
            bool captured = false;
            while (Time.time < end)
            {
                yield return null;
                Assert.That(walk.MovingFootCount, Is.LessThanOrEqualTo(4));
                foreach (var leg in legs)
                    Assert.That(Vector3.Distance(leg.data.tip.position, leg.data.target.position), Is.LessThan(0.02f),
                        $"{leg.name} at {Time.time}, delta {Time.deltaTime}, swinging {walk.MovingFootCount}");
                if (!captured && walk.CompletedSteps >= 4 && walk.MovingFootCount > 0)
                {
                    captured = true;
                }
            }
            Assert.That(Vector3.Distance(initial, walk.transform.position), Is.GreaterThan(0.05f));
            Assert.That(walk.CompletedSteps, Is.GreaterThan(16));
            Assert.That(captured, Is.True);
            CapturePreview();
        }

        private void CapturePreview()
        {
            var camera = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Camera>()).Single();
            CaptureCamera(camera, "Artifacts/SpiderDeityIK/unity_walk_preview.png");
        }

        internal static void CaptureCamera(Camera camera, string outputPath)
        {
            var target = new RenderTexture(900, 900, 24);
            var image = new Texture2D(900, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            try
            {
                target.Create();
                camera.targetTexture = target;
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Artifacts/SpiderDeityIK");
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                Object.Destroy(image);
                target.Release();
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator MissingGroundDoesNotCreateFalseStepsAndTeleportResetsContacts()
        {
            foreach (var collider in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Collider>()))
                collider.enabled = false;
            Physics.SyncTransforms();
            var positions = legs.Select(x => x.data.target.position).ToArray();
            walk.transform.position += Vector3.forward * 0.1f;
            yield return new WaitForSeconds(0.4f);
            Assert.That(walk.MovingFootCount, Is.Zero);
            for (int i = 0; i < legs.Length; i++)
                Assert.That(Vector3.Distance(positions[i], legs[i].data.target.position), Is.LessThan(0.0001f));
            walk.transform.position += Vector3.forward * 2f;
            yield return null;
            Assert.That(walk.MovingFootCount, Is.Zero);
            foreach (var leg in legs)
                Assert.That(Vector3.Distance(leg.data.target.position, walk.transform.position), Is.LessThan(1.2f));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
