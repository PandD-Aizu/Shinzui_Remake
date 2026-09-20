using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Shinzui.View;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shinzui.Tests
{
    public sealed class SpiderWebRenderingTests
    {
        private GameObject _webObject;
        private SpiderWeb _web;

        [SetUp]
        public void SetUp()
        {
            _webObject = new GameObject("Spider Web Rendering Test");
            _web = _webObject.AddComponent<SpiderWeb>();
            Set("seed", 1701);
            Set("radialCount", 32);
            Set("ringCount", 9);
            var anchors = new List<Transform>();
            for (int i = 0; i < 8; i++)
            {
                var anchor = new GameObject("Anchor " + i);
                anchor.transform.SetParent(_webObject.transform, false);
                float angle = i * Mathf.PI / 4.0f;
                anchor.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * 3.0f, Mathf.Sin(angle) * 2.0f, 0.0f);
                anchors.Add(anchor.transform);
            }
            Set("manualAnchors", anchors);
            _web.Regenerate();
            Render(null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_webObject);
        }

        [UnityTearDown]
        public IEnumerator ReturnToEditModeAfterRuntimeTest()
        {
            // NUnit assertions can abort the coroutine before its explicit exit.
            // This also runs on failure and leaves later EditMode tests in EditMode.
            if (Application.isPlaying)
            {
                yield return new ExitPlayMode();
            }
        }

        [Test]
        public void EightAnchors_KeepPhysicsAndCollidersWhileDrawingThirtyTwoSpokes()
        {
            Assert.That(_web.RenderRadialCount, Is.EqualTo(32));
            Assert.That(_web.NodeCount, Is.EqualTo(1 + 8 * 9));
            Assert.That(_web.InteractionColliderCount, Is.EqualTo(8));
            Array nodes = Get<Array>("_nodes");
            int fixedCount = 0;
            foreach (object node in nodes)
            {
                if (Member<float>(node, "InverseMass") == 0.0f)
                {
                    fixedCount++;
                }
            }
            Assert.That(fixedCount, Is.EqualTo(8));

            // Every interpolated tip participates in the unbroken perimeter graph.
            Array strands = Get<Array>("_renderStrands");
            var supportedTips = new HashSet<int>();
            foreach (object strand in strands)
            {
                int a = Member<int>(strand, "A");
                int b = Member<int>(strand, "B");
                if (a % 10 == 9 && b % 10 == 9)
                {
                    Assert.That(Member<float>(strand, "EndFraction"), Is.EqualTo(1.0f));
                    supportedTips.Add(a);
                    supportedTips.Add(b);
                }
            }
            Assert.That(supportedTips.Count, Is.EqualTo(32));
            Vector3[] points = Get<Vector3[]>("_renderPositions");
            for (int radial = 0; radial < 32; radial++)
            {
                int physical = radial / 4;
                int next = (physical + 1) % 8;
                Vector3 a = Member<Vector3>(nodes.GetValue(1 + physical * 9 + 8), "Position");
                Vector3 b = Member<Vector3>(nodes.GetValue(1 + next * 9 + 8), "Position");
                Assert.That(Vector3.Distance(points[radial * 10 + 9],
                    Vector3.Lerp(a, b, radial % 4 / 4.0f)), Is.LessThan(0.00001f));
            }
        }

        [Test]
        public void RenderingSettings_DoNotChangeSeededSimulation()
        {
            Array originalNodes = (Array)Get<Array>("_nodes").Clone();
            Array originalConstraints = (Array)Get<Array>("_constraints").Clone();
            int colliderCount = _web.InteractionColliderCount;
            Set("radialCount", 25);
            Set("captureThreadWidthRatio", 0.41f);
            Set("captureThreadSag", 0.04f);
            Set("captureCurveSegments", 3);
            Set("ringPhaseVariation", 0.3f);
            _web.Regenerate();
            CollectionAssert.AreEqual(originalNodes, Get<Array>("_nodes"));
            CollectionAssert.AreEqual(originalConstraints, Get<Array>("_constraints"));
            Assert.That(_web.InteractionColliderCount, Is.EqualTo(colliderCount));
            Assert.That(_web.RenderRadialCount, Is.EqualTo(25));
        }

        [Test]
        public void Curves_MeetSharedSpokeKnotsAndHaveRealSag()
        {
            Vector3[] positions = Get<Vector3[]>("_renderPositions");
            Vector3[] vertices = Get<Vector3[]>("_vertices");
            bool foundCurvedStrand = false;
            foreach (object strand in Get<Array>("_renderStrands"))
            {
                int first = Member<int>(strand, "FirstVertex");
                int segments = Member<int>(strand, "Segments");
                Vector3 a = positions[Member<int>(strand, "A")];
                Vector3 b = positions[Member<int>(strand, "B")];
                Assert.That(Vector3.Distance((vertices[first] + vertices[first + 1]) * 0.5f, a),
                    Is.LessThan(0.00001f));
                if (Member<float>(strand, "EndFraction") < 1.0f)
                {
                    continue;
                }
                int end = first + segments * 2;
                Assert.That(Vector3.Distance((vertices[end] + vertices[end + 1]) * 0.5f, b),
                    Is.LessThan(0.00001f));
                if (segments > 1)
                {
                    int middle = first + (segments / 2) * 2;
                    Vector3 curved = (vertices[middle] + vertices[middle + 1]) * 0.5f;
                    Vector3 straight = Vector3.Lerp(a, b, (segments / 2) / (float)segments);
                    foundCurvedStrand |= Vector3.Distance(curved, straight) > 0.0001f;
                }
            }
            Assert.That(foundCurvedStrand, Is.True);
        }

        [Test]
        public void Regenerate_WithSameSeedReproducesRenderGeometry()
        {
            Vector3[] vertices = (Vector3[])Get<Vector3[]>("_vertices").Clone();
            Color[] colors = (Color[])Get<Color[]>("_colors").Clone();
            _web.Regenerate();
            Render(null);
            CollectionAssert.AreEqual(vertices, Get<Vector3[]>("_vertices"));
            CollectionAssert.AreEqual(colors, Get<Color[]>("_colors"));
        }

        [Test]
        public void Bounds_IncludeDistantAnchorsAndAllCameraFacingVertices()
        {
            foreach (Transform anchor in _web.ManualAnchors)
            {
                anchor.localPosition += new Vector3(40.0f, 12.0f, 6.0f);
            }
            _webObject.transform.localScale = new Vector3(2.0f, 0.75f, 1.25f);
            _web.Regenerate();
            var cameraObject = new GameObject("Bounds Camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(70.0f, 8.0f, -20.0f);
                camera.transform.LookAt(_webObject.transform.TransformPoint(new Vector3(40, 12, 6)));
                Render(camera);
                Mesh mesh = _webObject.GetComponent<MeshFilter>().sharedMesh;
                foreach (Vector3 vertex in Get<Vector3[]>("_vertices"))
                {
                    Assert.That(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x), Is.False);
                    Assert.That(float.IsNaN(vertex.y) || float.IsInfinity(vertex.y), Is.False);
                    Assert.That(float.IsNaN(vertex.z) || float.IsInfinity(vertex.z), Is.False);
                    Assert.That(mesh.bounds.Contains(vertex), Is.True);
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void SubpixelWidth_ConservesCoverageAndReusesBuffersForEachCamera()
        {
            Set("threadWidth", 0.0005f);
            _web.Regenerate();
            var cameraObject = new GameObject("Width Camera");
            var target = new RenderTexture(512, 512, 0);
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = target;
                camera.orthographic = true;
                camera.orthographicSize = 4.0f;
                camera.transform.position = Vector3.back * 10.0f;
                Render(camera);
                Vector3[] vertices = Get<Vector3[]>("_vertices");
                Color[] colors = Get<Color[]>("_colors");
                float coveredWidth = Vector3.Distance(vertices[0], vertices[1]) * colors[0].a;
                Assert.That(colors[0].a, Is.GreaterThan(0.0f).And.LessThan(1.0f));
                camera.orthographicSize = 12.0f;
                camera.transform.position = new Vector3(-4, 0, -10);
                camera.transform.LookAt(_webObject.transform);
                Render(camera);
                Assert.That(Get<Vector3[]>("_vertices"), Is.SameAs(vertices));
                Assert.That(Get<Color[]>("_colors"), Is.SameAs(colors));
                Assert.That(Vector3.Distance(vertices[0], vertices[1]) * colors[0].a,
                    Is.EqualTo(coveredWidth).Within(0.000001f));
                foreach (Vector4 tangent in Get<Vector4[]>("_tangents"))
                {
                    Assert.That(new Vector3(tangent.x, tangent.y, tangent.z).magnitude,
                        Is.EqualTo(1.0f).Within(0.00001f));
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void DisableEnableAndReset_RestoreRenderingWithoutAddingColliders()
        {
            Mesh oldMesh = _webObject.GetComponent<MeshFilter>().sharedMesh;
            _web.enabled = false;
            Assert.That(oldMesh == null, Is.True);
            _web.enabled = true;
            Mesh newMesh = _webObject.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(newMesh, Is.Not.Null);
            Assert.That(newMesh.vertexCount, Is.GreaterThan(0));
            Assert.That(_web.InteractionColliderCount, Is.EqualTo(8));
            _web.ResetSimulation();
            Assert.That(_webObject.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(newMesh));
            Assert.That(_web.IsBurning, Is.False);
        }

        [Test]
        public void OffscreenCamera_SkipsMeshUploadButDetectsNodesMovingIntoView()
        {
            var cameraObject = new GameObject("Offscreen Camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(1000, 0, -10);
                camera.transform.rotation = Quaternion.identity;
                camera.fieldOfView = 60;
                Vector3[] vertices = Get<Vector3[]>("_vertices");
                Vector3[] original = (Vector3[])vertices.Clone();
                InvokeWeb("OnBeginCameraRendering", default(UnityEngine.Rendering.ScriptableRenderContext), camera);
                CollectionAssert.AreEqual(original, vertices, "An offscreen web must not rebuild its mesh.");

                // The renderer still has its old bounds at the origin. Simulated
                // nodes entering the view must nevertheless be detected and drawn.
                Array nodes = Get<Array>("_nodes");
                for (int i = 0; i < nodes.Length; i++)
                {
                    object node = nodes.GetValue(i);
                    node.GetType().GetField("Position").SetValue(node,
                        Member<Vector3>(node, "Position") + Vector3.right * 1000);
                    nodes.SetValue(node, i);
                }
                InvokeWeb("OnBeginCameraRendering", default(UnityEngine.Rendering.ScriptableRenderContext), camera);
                Assert.That(Vector3.Distance((vertices[0] + vertices[1]) * 0.5f,
                    new Vector3(1000, 0, 0)), Is.LessThan(0.0001f));
                Assert.That(Get<Vector3[]>("_vertices"), Is.SameAs(vertices));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeContactReleaseAndBurn_PreserveAnchorsAndCleanUpRendering()
        {
            // Do not carry Unity object references across the Play Mode domain reload.
            Object.DestroyImmediate(_webObject);
            _webObject = null;
            _web = null;
            yield return new EnterPlayMode();

            GameObject restoredFixture = GameObject.Find("Spider Web Rendering Test");
            if (restoredFixture != null)
            {
                Object.DestroyImmediate(restoredFixture);
            }
            SetUp();
            GameObject playerObject = null;
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 1.0f;
                Set("simulateOffscreen", true);
                Set("burnFadeDuration", 1.0f);
                SpiderWebBurnVfx vfx = _webObject.GetComponent<SpiderWebBurnVfx>();
                typeof(SpiderWebBurnVfx).GetField("duration", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(vfx, 1.4f);

                playerObject = new GameObject("Spider Web Runtime Test Player");
                playerObject.transform.position = new Vector3(0.0f, 0.0f, 0.35f);
                CapsuleCollider playerCollider = playerObject.AddComponent<CapsuleCollider>();
                playerCollider.height = 1.2f;
                playerCollider.radius = 0.2f;
                PlayerView player = playerObject.AddComponent<PlayerView>();
                typeof(PlayerView).GetField("playerCollider", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(player, playerCollider);
                Physics.SyncTransforms();

                // Manual section callbacks exercise the actual contact-counting logic
                // without depending on Rigidbody setup or broad-phase timing.
                InvokeWeb("OnTriggerEnter", playerCollider);
                InvokeWeb("OnTriggerEnter", playerCollider);
                InvokeWeb("FixedUpdate");
                IDictionary modifiers = (IDictionary)typeof(PlayerView)
                    .GetField("_motionModifiers", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(player);
                Dictionary<PlayerView, int> contacts = Get<Dictionary<PlayerView, int>>("_playerContactCounts");
                Assert.That(contacts[player], Is.EqualTo(2));
                Assert.That(modifiers.Count, Is.EqualTo(1), "Overlapping sections must share one movement modifier.");
                foreach (object modifier in modifiers.Values)
                {
                    Assert.That(Member<float>(modifier, "HorizontalSpeedMultiplier"), Is.LessThan(1.0f));
                    Assert.That(Member<Vector3>(modifier, "AdditiveVelocity").z, Is.LessThan(0.0f));
                }

                InvokeWeb("OnTriggerExit", playerCollider);
                Assert.That(contacts[player], Is.EqualTo(1));
                Assert.That(modifiers.Count, Is.EqualTo(1), "Leaving one section must retain the remaining contact.");
                InvokeWeb("OnTriggerExit", playerCollider);
                Assert.That(contacts.Count, Is.Zero);
                Assert.That(modifiers.Count, Is.Zero, "Leaving the final section must release the player.");
                InvokeWeb("FixedUpdate");

                int fixedCount = 0;
                foreach (object node in Get<Array>("_nodes"))
                {
                    if (Member<float>(node, "InverseMass") != 0.0f)
                    {
                        continue;
                    }
                    fixedCount++;
                    Assert.That(Vector3.Distance(Member<Vector3>(node, "Position"),
                        Member<Vector3>(node, "RestPosition")), Is.LessThan(0.000001f));
                }
                Assert.That(fixedCount, Is.EqualTo(8));
                Assert.That(_web.RenderRadialCount, Is.EqualTo(32));

                InvokeWeb("OnTriggerEnter", playerCollider);
                InvokeWeb("FixedUpdate");
                Assert.That(modifiers.Count, Is.EqualTo(1));
                Mesh mesh = _webObject.GetComponent<MeshFilter>().sharedMesh;
                MeshRenderer renderer = _webObject.GetComponent<MeshRenderer>();
                Material originalMaterial = renderer.sharedMaterial;
                Assert.That(originalMaterial.HasProperty("_BaseColor"), Is.True);
                float originalAlpha = originalMaterial.GetColor("_BaseColor").a;

                _web.Burn();
                Assert.That(_web.IsBurning, Is.True);
                Assert.That(_web.InteractionColliderCount, Is.Zero);
                Assert.That(contacts.Count, Is.Zero);
                Assert.That(modifiers.Count, Is.Zero, "Burning must release a currently trapped player immediately.");
                Material burnMaterial = renderer.sharedMaterial;
                Assert.That(burnMaterial, Is.Not.SameAs(originalMaterial));
                _web.Burn();
                Assert.That(renderer.sharedMaterial, Is.SameAs(burnMaterial));
                Assert.That(_webObject.GetComponentsInChildren<ParticleSystem>().Length, Is.EqualTo(3));

                // Remove the isolated player before Start can discover Cinemachine
                // cameras in an unrelated open scene. Release was asserted above.
                Object.DestroyImmediate(playerObject);
                playerObject = null;

                float firstAlpha = burnMaterial.GetColor("_BaseColor").a;
                yield return new WaitForSeconds(0.12f);
                Assert.That(burnMaterial.GetColor("_BaseColor").a, Is.LessThan(firstAlpha));
                Assert.That(originalMaterial.GetColor("_BaseColor").a, Is.EqualTo(originalAlpha));

                float deadline = Time.realtimeSinceStartup + 5.0f;
                while (_webObject != null && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
                Assert.That(_webObject == null, Is.True, "Burn completion must destroy the web after residual VFX.");
                yield return null; // Runtime mesh and material destruction is deferred.
                Assert.That(mesh == null, Is.True);
                Assert.That(burnMaterial == null, Is.True);
                Assert.That(modifiers.Count, Is.Zero);
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                if (playerObject != null)
                {
                    Object.DestroyImmediate(playerObject);
                }
                if (_webObject != null)
                {
                    Object.DestroyImmediate(_webObject);
                }
            }

            yield return new ExitPlayMode();
        }

        private void InvokeWeb(string name, params object[] arguments)
        {
            typeof(SpiderWeb).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_web, arguments);
        }

        private void Set(string name, object value)
        {
            typeof(SpiderWeb).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_web, value);
        }

        private T Get<T>(string name)
        {
            return (T)typeof(SpiderWeb).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_web);
        }

        private static T Member<T>(object value, string name)
        {
            return (T)value.GetType().GetField(name).GetValue(value);
        }

        private void Render(Camera camera)
        {
            typeof(SpiderWeb).GetMethod("UpdateMeshForCamera", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_web, new object[] { camera });
        }
    }
}
