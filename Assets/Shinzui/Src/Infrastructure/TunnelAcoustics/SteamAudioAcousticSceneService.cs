using System;
using Shinzui.Application.SpatialAudio;
using SteamAudio;

namespace Shinzui.Infrastructure.TunnelAcoustics
{
    public sealed class SteamAudioAcousticSceneService : IAcousticSceneService
    {
        StaticMesh mesh;
        readonly Scene scene;
        bool disposed;
        public int Revision { get; private set; }
        public int TriangleCount { get; private set; }
        public int NativeObjectCount => scene.GetNumObjects();
        public SteamAudioAcousticSceneService()
        {
            // Initialize on the main thread, after the Unity scene has loaded.
            _ = SteamAudioManager.Singleton;
            scene = SteamAudioManager.CurrentScene;
            if (scene == null || scene.Get() == IntPtr.Zero) throw new InvalidOperationException("Steam Audio scene is not ready.");
        }
        public void Replace(AcousticMeshData geometry)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SteamAudioAcousticSceneService));
            Validate(geometry);
            StaticMesh replacement = geometry.Triangles.Length == 0 ? null : CreateMesh(scene, geometry);
            Clear();
            mesh = replacement;
            if (mesh != null) { mesh.AddToScene(scene); TriangleCount = geometry.Triangles.Length/3; }
            SteamAudioManager.ScheduleCommitScene(); // Manager commits only while its simulation thread is idle.
            Revision++;
        }
        public void Clear()
        {
            if (mesh == null) return;
            if (scene.Get() != IntPtr.Zero) { mesh.RemoveFromScene(scene); SteamAudioManager.ScheduleCommitScene(); }
            mesh.Release(); mesh = null; TriangleCount = 0;
        }
        public void Dispose() { if (!disposed) { Clear(); disposed = true; } }
        internal static StaticMesh CreateMesh(Scene scene, AcousticMeshData geometry)
        {
            Validate(geometry);
            var vertices = new SteamAudio.Vector3[geometry.Vertices.Length];
            var triangles = new Triangle[geometry.Triangles.Length/3];
            var materials = new int[triangles.Length];
            for (int i = 0; i < vertices.Length; i++)
            { var p = geometry.Vertices[i]; vertices[i] = new SteamAudio.Vector3 { x=p.X,y=p.Y,z=-p.Z }; }
            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = new Triangle { index0=geometry.Triangles[3*i],index1=geometry.Triangles[3*i+1],index2=geometry.Triangles[3*i+2] };
                materials[i] = (int)geometry.Surfaces[i];
            }
            return new StaticMesh(SteamAudioManager.Context, scene, vertices, triangles, materials, new[] {
                Material(.05f,.08f,.12f,.2f,.015f,.002f,.001f),
                Material(.03f,.04f,.05f,.05f,.03f,.01f,.003f),
                Material(.12f,.2f,.3f,.3f,.08f,.04f,.01f) });
        }
        static SteamAudio.Material Material(float a,float b,float c,float s,float t1,float t2,float t3) =>
            new SteamAudio.Material { absorptionLow=a,absorptionMid=b,absorptionHigh=c,scattering=s,
                transmissionLow=t1,transmissionMid=t2,transmissionHigh=t3 };
        static void Validate(AcousticMeshData data)
        {
            if (data == null || data.Vertices == null || data.Triangles == null || data.Surfaces == null ||
                data.Triangles.Length%3 != 0 || data.Surfaces.Length != data.Triangles.Length/3)
                throw new ArgumentException("Invalid acoustic mesh arrays.");
            foreach (var point in data.Vertices) if (!point.IsFinite) throw new ArgumentException("Non-finite acoustic vertex.");
            foreach (int index in data.Triangles) if (index < 0 || index >= data.Vertices.Length) throw new ArgumentException("Invalid acoustic triangle index.");
            foreach (var material in data.Surfaces) if ((int)material < 0 || (int)material > 2) throw new ArgumentException("Invalid acoustic material.");
        }
    }
}
