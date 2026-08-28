using NUnit.Framework;
using Shinzui.View;
using UnityEngine;

namespace Shinzui.Tests
{
    public sealed class SpiderWebTests
    {
        [Test]
        public void ManualAnchors_CreateOneInteractionColliderPerWebSection()
        {
            var webObject = new GameObject("Spider Web Test");
            try
            {
                var web = webObject.AddComponent<SpiderWeb>();

                Assert.That(webObject.GetComponent<SpiderWebInteractable>(), Is.Not.Null);
                Assert.That(webObject.GetComponent<SpiderWebBurnVfx>(), Is.Not.Null);
                Assert.That(
                    webObject.GetComponent<SpiderWebInteractable>().InteractableId,
                    Is.EqualTo("spider_web"));
                Vector3[] positions =
                {
                    new(-3.0f, -1.0f, 0.0f),
                    new(-1.5f, 2.0f, 0.0f),
                    new(1.0f, 2.5f, 0.0f),
                    new(3.0f, 0.5f, 0.0f),
                    new(1.5f, -2.0f, 0.0f)
                };

                foreach (Vector3 position in positions)
                {
                    var anchor = new GameObject("Anchor");
                    anchor.transform.SetParent(webObject.transform);
                    anchor.transform.localPosition = position;
                    web.AddManualAnchor(anchor.transform);
                }

                Assert.That(web.ManualAnchors.Count, Is.EqualTo(positions.Length));
                Assert.That(web.UsesManualAnchors, Is.True);
                Assert.That(
                    web.InteractionColliderCount,
                    Is.EqualTo(positions.Length));
                Assert.That(webObject.GetComponent<BoxCollider>().enabled, Is.False);

                foreach (MeshCollider trigger in
                         webObject.GetComponents<MeshCollider>())
                {
                    Assert.That(trigger.isTrigger, Is.True);
                    Assert.That(trigger.convex, Is.True);
                    Assert.That(trigger.sharedMesh, Is.Not.Null);
                }
            }
            finally
            {
                Object.DestroyImmediate(webObject);
            }
        }

        [Test]
        public void BurnVfx_Play_CreatesOnlyConfiguredParticleSystems()
        {
            var vfxObject = new GameObject("Spider Web Burn VFX Test");
            try
            {
                var burnVfx = vfxObject.AddComponent<SpiderWebBurnVfx>();

                burnVfx.Play(5.5f, 4.0f);

                Assert.That(
                    vfxObject.GetComponentsInChildren<ParticleSystem>(true).Length,
                    Is.EqualTo(3));
                Assert.That(vfxObject.transform.Find("Ember VFX Graph"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(vfxObject);
            }
        }
    }
}
