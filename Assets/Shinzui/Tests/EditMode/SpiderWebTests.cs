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
    }
}
