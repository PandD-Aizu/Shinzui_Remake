using System.Collections.Generic;
using NUnit.Framework;
using Shinzui.View;
using UnityEngine;

namespace Shinzui.Tests.Title
{
    public sealed class PortalRenderTextureCacheTests
    {
        private readonly List<RenderTexture> _owned = new();

        /// <summary>Start with an empty retained cache</summary>
        [SetUp] public void SetUp() => PortalRenderTextureCache.Clear();

        /// <summary>Release cached and checked-out test resources even after assertion failure</summary>
        [TearDown] public void TearDown()
        {
            PortalRenderTextureCache.Clear();
            foreach (var texture in _owned)
                if (texture != null) { texture.Release(); Object.DestroyImmediate(texture); }
            _owned.Clear();
        }

        /// <summary>Reuse compatible targets without aliasing two active owners or substituting a different format</summary>
        [Test] public void ReusePreservesFormatAndExclusiveOwnership()
        {
            var original = Acquire(64, RenderTextureFormat.ARGBHalf);
            PortalRenderTextureCache.Release(original);
            var reused = Acquire(64, RenderTextureFormat.ARGBHalf);
            var simultaneous = Acquire(64, RenderTextureFormat.ARGBHalf);
            var differentFormat = Acquire(64, RenderTextureFormat.ARGBFloat);
            Assert.That(reused, Is.SameAs(original));
            Assert.That(simultaneous, Is.Not.SameAs(reused));
            Assert.That(differentFormat.graphicsFormat, Is.Not.EqualTo(reused.graphicsFormat));
            Assert.That(reused.IsCreated(), Is.True);
        }

        /// <summary>Evict oldest entries rather than exceed the retained pixel-storage budget</summary>
        [Test] public void RetainedStorageIsBounded()
        {
            var oldest = Acquire(2048, RenderTextureFormat.ARGBHalf);
            var second = Acquire(2048, RenderTextureFormat.ARGBHalf);
            var third = Acquire(2048, RenderTextureFormat.ARGBHalf);
            PortalRenderTextureCache.Release(oldest);
            PortalRenderTextureCache.Release(second);
            PortalRenderTextureCache.Release(third);
            Assert.That(PortalRenderTextureCache.RetainedBytes, Is.LessThanOrEqualTo(128L * 1024 * 1024));
            Assert.That(oldest == null, Is.True);
        }

        /// <summary>Clearing retained storage never destroys a target currently owned by a camera</summary>
        [Test] public void ClearPreservesCheckedOutTarget()
        {
            var active = Acquire(64, RenderTextureFormat.ARGBHalf);
            var retained = Acquire(32, RenderTextureFormat.ARGBHalf);
            PortalRenderTextureCache.Release(retained);
            PortalRenderTextureCache.Clear();
            Assert.That(active.IsCreated(), Is.True);
            Assert.That(retained == null, Is.True);
            Assert.That(PortalRenderTextureCache.RetainedBytes, Is.Zero);
        }

        /// <summary>Track all allocated test targets for cleanup</summary>
        /// <param name="size">Square target size</param>
        /// <param name="format">Color format</param>
        /// <returns>Exclusive target</returns>
        private RenderTexture Acquire(int size, RenderTextureFormat format)
        {
            var texture = PortalRenderTextureCache.Acquire(size, size, format, "PortalCacheTest");
            _owned.Add(texture);
            return texture;
        }
    }
}
