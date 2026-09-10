using SteamAudio;
using UnityEngine;
using Matrix4x4 = UnityEngine.Matrix4x4;

namespace Shinzui.Infrastructure.TunnelAcoustics
{
    /// <summary>Rigid door/panel acoustic geometry. Topology changes require a new registration.</summary>
    public sealed class MovingAcousticGeometry : MonoBehaviour
    {
        public AcousticMeshLibrary Library;
        SteamAudio.Scene parent, subScene;
        StaticMesh mesh;
        InstancedMesh instance;
        Matrix4x4 previous;
        bool added;
        void Start()
        {
            parent = SteamAudioManager.CurrentScene;
            subScene = new SteamAudio.Scene(SteamAudioManager.Context, SceneType.Default, null, null, null, null);
            mesh = SteamAudioAcousticSceneService.CreateMesh(subScene, AcousticGeometryCollector.Collect(transform, Library, true, true));
            mesh.AddToScene(subScene); subScene.Commit();
            instance = new InstancedMesh(parent, subScene, transform);
            instance.AddToScene(parent); added = true;
            previous = transform.localToWorldMatrix;
            SteamAudioManager.ScheduleCommitScene();
        }
        void OnEnable()
        {
            if (instance != null && !added) { instance.AddToScene(parent); added = true; SteamAudioManager.ScheduleCommitScene(); }
        }
        void LateUpdate()
        {
            if (instance != null && transform.localToWorldMatrix != previous)
            { instance.UpdateTransform(parent, transform); previous = transform.localToWorldMatrix; SteamAudioManager.ScheduleCommitScene(); }
        }
        void OnDisable()
        {
            if (instance != null && added && parent.Get() != System.IntPtr.Zero)
            { instance.RemoveFromScene(parent); added = false; SteamAudioManager.ScheduleCommitScene(); }
        }
        void OnDestroy()
        { OnDisable(); instance?.Release(); mesh?.Release(); subScene?.Release(); instance=null; mesh=null; subScene=null; }
    }
}
