using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace Shinzui.View
{
    /// <summary>Reuse HDRP portal targets on the main thread within a conservative retained-memory budget</summary>
    public static class PortalRenderTextureCache
    {
        private const long BudgetBytes = 128L * 1024 * 1024;
        private const int MaximumTextures = 32;
        private static readonly List<RenderTexture> Textures = new(MaximumTextures);
        private static readonly ProfilerMarker AllocationMarker = new(ProfilerCategory.Render, "Shinzui.PortalTextureAllocate");
        private static bool _quitting;
        public static long RetainedBytes { get; private set; }
        public static int Allocations { get; private set; }
        public static int Hits { get; private set; }

        /// <summary>Reset counters and release retained targets between play sessions</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Clear();
            _quitting = false;
            Allocations = Hits = 0;
            Application.quitting -= OnQuit;
            Application.quitting += OnQuit;
        }

        /// <summary>Acquire an exclusive target with the same format and dimensions as an uncached allocation</summary>
        /// <param name="width">Target width</param>
        /// <param name="height">Target height</param>
        /// <param name="format">Requested HDR color format</param>
        /// <param name="name">Diagnostic resource name</param>
        /// <returns>Created target owned by the caller until returned</returns>
        public static RenderTexture Acquire(int width, int height, RenderTextureFormat format, string name)
        {
            var descriptor = new RenderTextureDescriptor(width, height, format, 24) { msaaSamples = 1, useMipMap = false };
            for (int i = Textures.Count - 1; i >= 0; i--)
            {
                var candidate = Textures[i];
                if (candidate.width != width || candidate.height != height || candidate.graphicsFormat != descriptor.graphicsFormat) continue;
                Textures.RemoveAt(i);
                RetainedBytes -= EstimateBytes(candidate);
                if (!candidate.IsCreated()) { DestroyTarget(candidate); continue; }
                candidate.name = name;
                Hits++;
                return candidate;
            }
            using (AllocationMarker.Auto())
            {
                var target = new RenderTexture(descriptor) { name = name };
                target.Create();
                Allocations++;
                return target;
            }
        }

        /// <summary>Return a detached target, evicting oldest entries before the retained budget is exceeded</summary>
        /// <param name="target">Target no longer referenced by a rendering camera</param>
        public static void Release(RenderTexture target)
        {
            if (target == null) return;
            long bytes = EstimateBytes(target);
            if (_quitting || !target.IsCreated() || bytes > BudgetBytes) { DestroyTarget(target); return; }
            while (Textures.Count > 0 && (Textures.Count >= MaximumTextures || RetainedBytes + bytes > BudgetBytes))
            {
                var oldest = Textures[0];
                Textures.RemoveAt(0);
                RetainedBytes -= EstimateBytes(oldest);
                DestroyTarget(oldest);
            }
            Textures.Add(target);
            RetainedBytes += bytes;
        }

        /// <summary>Release all retained targets without affecting checked-out targets</summary>
        public static void Clear()
        {
            foreach (var target in Textures) DestroyTarget(target);
            Textures.Clear();
            RetainedBytes = 0;
        }

        /// <summary>Account for the actual color format and four depth bytes per pixel</summary>
        /// <param name="target">Single-sampled target without mipmaps</param>
        /// <returns>Conservative pixel-storage estimate, excluding driver metadata</returns>
        private static long EstimateBytes(RenderTexture target) => (long)target.width * target.height * (UnityEngine.Experimental.Rendering.GraphicsFormatUtility.GetBlockSize(target.graphicsFormat) + 4);

        /// <summary>Release native storage and destroy its Unity wrapper</summary>
        /// <param name="target">Unowned target</param>
        private static void DestroyTarget(RenderTexture target)
        {
            if (target == null) return;
            target.Release();
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        /// <summary>Prevent late component teardown from retaining targets after shutdown begins</summary>
        private static void OnQuit() { _quitting = true; Clear(); }
    }
}
