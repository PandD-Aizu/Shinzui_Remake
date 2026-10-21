using System.Reflection;
using NUnit.Framework;
using Shinzui.View;
using UnityEngine;

namespace Shinzui.Tests.Title
{
    public sealed class PortalNearPlaneTests
    {
        /// <summary>Keep visible and intersecting portal bounds while rejecting only wholly clipped bounds</summary>
        /// <param name="yaw">Camera yaw, checking the world-space plane calculation</param>
        [TestCase(0f)]
        [TestCase(45f)]
        public void NearPlaneCullingPreservesCrossings(float yaw)
        {
            var host = new GameObject("Portal near plane test");
            try
            {
                var camera = host.AddComponent<Camera>();
                camera.nearClipPlane = .1f;
                camera.transform.SetPositionAndRotation(new Vector3(3, 2, -5), Quaternion.Euler(15, yaw, 0));
                Plane near = GeometryUtility.CalculateFrustumPlanes(camera)[4];
                var method = typeof(TunnelGateView).GetMethod("IsWhollyBehindNearPlane", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(method, Is.Not.Null);
                Vector3 position = camera.transform.position;
                Vector3 forward = camera.transform.forward;
                Assert.That((bool)method.Invoke(null, new object[] { new Bounds(position - forward, Vector3.one * .1f), near }), Is.True);
                Assert.That((bool)method.Invoke(null, new object[] { new Bounds(position + forward, Vector3.one * .1f), near }), Is.False);
                Assert.That((bool)method.Invoke(null, new object[] { new Bounds(position + forward * .1f, Vector3.one * .2f), near }), Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
