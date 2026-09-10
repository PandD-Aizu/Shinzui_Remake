using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FMODUnity;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Application.SpatialAudio;
using Shinzui.Application.UseCases.Tunnel;
using Shinzui.AudioProbe.Infrastructure;
using Shinzui.DI.TunnelAcoustics;
using Shinzui.Domain.DomainServices.Tunnel;
using Shinzui.Infrastructure.TunnelAcoustics;
using Shinzui.Presentation.GenerateTunnel;
using Shinzui.View.GenerateTunnel;
using SteamAudio;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Shinzui.TunnelAcoustics.Tests
{
    public sealed class TunnelAcousticsProbe : MonoBehaviour
    {
        public TunnelAudioConfiguration Configuration;
        public GameObject TunnelTemplate, CorridorTemplate;
        public Transform Listener;
        public static bool Completed { get; private set; }
        public static bool Passed { get; private set; }
        [Serializable] public sealed class CheckResult { public string name; public bool passed; public string detail; }
        [Serializable] sealed class Report { public bool passed; public string environment; public List<CheckResult> checks; public int[] triangleCounts; public int[] seeds; }
        readonly List<CheckResult> checks = new List<CheckResult>();
        readonly List<int> triangleCounts = new List<int>();
        readonly int[] seeds = {2777,42,1234};
        TunnelAudioBinding binding;
        SteamAudioSource raySource;
        Transform feet;
        ProbeCapture capture;
        bool failed;
        float started;
        string output;
        void Awake()
        {
            Completed=Passed=false; started=Time.realtimeSinceStartup;
            output=Argument("-tunnelAudioOutput") ?? Path.Combine(UnityEngine.Application.persistentDataPath,"TunnelAcousticsProbe");
            Directory.CreateDirectory(output); UnityEngine.Application.logMessageReceived+=OnLog;
        }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2);
            capture=new ProbeCapture(RuntimeManager.CoreSystem);
            var root=new GameObject("MapRoot");
            var view=root.AddComponent<TunnelMapView>(); view.SetBuildTemplates(TunnelTemplate,CorridorTemplate);
            binding=TunnelAudioBinding.Attach(view,Configuration);
            feet=new GameObject("Test Foot Position").transform;
            binding.Runtime.BindPlayer(Listener,feet);
            var rayObject=new GameObject("Acoustic Direct Ray Probe");
            raySource=rayObject.AddComponent<SteamAudioSource>(); raySource.occlusion=true;
            raySource.reflections=false;
            Check("binding_is_idempotent",binding==TunnelAudioBinding.Attach(view,Configuration));
            for (int i=0;i<seeds.Length;i++)
            {
                var presenter=new GenerateTunnelPresenter(new GenerateTunnelUseCase(new TunnelLayoutGenerator()),view);
                Check("generate_seed_"+seeds[i],presenter.ExecuteGeneration(new TunnelGenerationRequestDto { Seed=seeds[i],TunnelCount=4,SmallRoomCount=1 }));
                yield return WaitFor(()=>binding.BuildCount==i+1,10);
                var mesh=AcousticGeometryCollector.Collect(view.GeometryRoot,Configuration.MeshLibrary);
                triangleCounts.Add(mesh.Triangles.Length/3);
                Check("geometry_registered_"+seeds[i],binding.Runtime.Geometry.TriangleCount>100 && binding.Runtime.Geometry.NativeObjectCount==1,
                    "triangles="+binding.Runtime.Geometry.TriangleCount);
                Check("single_geometry_root_"+seeds[i],root.transform.childCount>=1 && binding.BuildCount==i+1);
                int a=mesh.Triangles[0],b=mesh.Triangles[1],c=mesh.Triangles[2];
                Vector3 va=V(mesh.Vertices[a]),vb=V(mesh.Vertices[b]),vc=V(mesh.Vertices[c]);
                Vector3 center=(va+vb+vc)/3, normal=Vector3.Cross(vb-va,vc-va).normalized;
                raySource.transform.position=center+normal*.1f; Listener.position=center-normal*.1f;
                yield return new WaitForSecondsRealtime(.35f);
                Check("native_surface_blocks_ray_"+seeds[i],Occlusion<.1f,"occlusion="+Occlusion);
                // Cross the actual imported corridor mouth, where a bounding-box proxy would add a wall.
                Transform passage=null;
                foreach(var candidate in view.GeometryRoot.GetComponentsInChildren<Transform>())
                    if(candidate.name.StartsWith("Corridor ") && candidate.Find("Walkable Floor Collider")) { passage=candidate; break; }
                Check("generated_passage_found_"+seeds[i],passage);
                if(passage)
                {
                    float halfLength=passage.Find("Walkable Floor Collider").localScale.z*.5f;
                    Listener.position=passage.TransformPoint(new Vector3(0,1,-halfLength-.2f));
                    raySource.transform.position=passage.TransformPoint(new Vector3(0,1,-halfLength+.2f));
                    yield return new WaitForSecondsRealtime(.35f);
                    Check("actual_corridor_opening_passes_sound_"+seeds[i],Occlusion>.9f,"occlusion="+Occlusion);
                }
                binding.Runtime.Clear();
                yield return new WaitForSecondsRealtime(.3f);
                Check("clear_removes_old_faces_"+seeds[i],Occlusion>.9f && binding.Runtime.Geometry.NativeObjectCount==0,"occlusion="+Occlusion);
            }

            // A door is an independent rigid acoustic object, never part of the static aggregate.
            var door=Box("Moving metal door",new Vector3(10000,2,0),new Vector3(6,4,.2f));
            door.AddComponent<AcousticSurface>().Kind=AcousticSurfaceKind.Metal;
            var moving=door.AddComponent<MovingAcousticGeometry>(); moving.Library=Configuration.MeshLibrary;
            Listener.position=new Vector3(10000,2,-2); raySource.transform.position=new Vector3(10000,2,2);
            yield return new WaitForSecondsRealtime(.4f);
            Check("closed_door_occludes",Occlusion<.1f);
            Check("moving_door_excluded_from_static_mesh",AcousticGeometryCollector.Collect(door.transform,Configuration.MeshLibrary).Triangles.Length==0);
            door.transform.position+=Vector3.right*10;
            yield return new WaitForSecondsRealtime(.3f);
            Check("open_door_passes_sound",Occlusion>.9f);
            door.transform.position-=Vector3.right*10;
            yield return new WaitForSecondsRealtime(.3f);
            moving.enabled=false;
            yield return new WaitForSecondsRealtime(.3f);
            Check("disabled_door_unregisters",Occlusion>.9f);
            moving.enabled=true;
            yield return new WaitForSecondsRealtime(.3f);
            Check("reenabled_door_registers_once",Occlusion<.1f && binding.Runtime.Geometry.NativeObjectCount==1);
            UnityEngine.Object.Destroy(door);
            yield return new WaitForSecondsRealtime(.3f);
            Check("destroyed_door_leaves_no_mesh",Occlusion>.9f && binding.Runtime.Geometry.NativeObjectCount==0);

            var room=new GameObject("Latency test room");
            foreach (var spec in new[] {
                (new Vector3(20000,0,0),new Vector3(6,.2f,8)),(new Vector3(20000,3,0),new Vector3(6,.2f,8)),
                (new Vector3(19997,1.5f,0),new Vector3(.2f,3,8)),(new Vector3(20003,1.5f,0),new Vector3(.2f,3,8)),
                (new Vector3(20000,1.5f,-4),new Vector3(6,3,.2f)),(new Vector3(20000,1.5f,4),new Vector3(6,3,.2f)) })
                Box("Concrete shell",spec.Item1,spec.Item2).transform.SetParent(room.transform,true);
            Listener.position=new Vector3(20000,1.5f,-1.5f); feet.position=new Vector3(20000,1.5f,1.5f);
            binding.Runtime.Rebuild(room.transform);
            yield return WaitFor(()=>binding.Runtime.Footsteps.ReadyCount==2,6);
            Check("two_footstep_sources_prepared",binding.Runtime.Footsteps.ReadyCount==2);
            capture.Begin();
            var warm=binding.Runtime.Footsteps.Trigger();
            Check("prepared_trigger_starts_in_same_frame",warm.IsValid && binding.Runtime.Voices.GetState(warm)==SpatialVoiceState.Playing);
            yield return new WaitForSecondsRealtime(.8f);
            Check("prepared_footstep_audible",capture.Finish(Path.Combine(output,"prepared-step.wav"))>.001f);
            // Let that tail finish before measuring the cold request.
            yield return new WaitForSecondsRealtime(3.5f);
            capture.Begin();
            var cold=binding.Runtime.Voices.Play(new SpatialAudioRequest(Configuration.FootstepEventId,AudioPose.At(20000,1.5f,1.5f),SpatialAudioCategory.Footstep,96,.5f));
            Check("cold_request_uses_preparation",cold.Accepted && binding.Runtime.Voices.GetState(cold.Handle)==SpatialVoiceState.Preparing);
            yield return new WaitForSecondsRealtime(.8f);
            Check("cold_footstep_audible",capture.Finish(Path.Combine(output,"cold-step.wav"))>.001f);
            yield return new WaitForSecondsRealtime(3.5f);
            var old=binding.Runtime.Footsteps.Trigger();
            feet.position+=Vector3.right*30; Listener.position+=Vector3.right*30;
            binding.Runtime.NotifyWarp();
            Check("warp_cancels_old_footstep_and_reservations",old.IsValid && binding.Runtime.Voices.GetState(old)==SpatialVoiceState.Stopped && binding.Runtime.Footsteps.ReadyCount==0);
            yield return WaitFor(()=>binding.Runtime.Footsteps.ReadyCount==2,6);
            Check("warp_rewarms_at_destination",binding.Runtime.Footsteps.ReadyCount==2);
            var destination=binding.Runtime.Footsteps.Trigger();
            RuntimeManager.StudioSystem.flushCommands();
            Check("destination_footstep_has_no_velocity_spike",binding.Runtime.Voices.TryInspect(destination,out var info) && info.Velocity.sqrMagnitude<.001f && (info.Position-feet.position).sqrMagnitude<.01f);
            var contactPosition=feet.position;
            feet.position+=Vector3.right*.2f;
            binding.Runtime.Voices.Tick();RuntimeManager.StudioSystem.flushCommands();
            Check("emitted_tail_stays_at_contact",binding.Runtime.Voices.TryInspect(destination,out info) && (info.Position-contactPosition).sqrMagnitude<.01f);
            yield return WaitFor(()=>binding.Runtime.Footsteps.ReadyCount==2,6);
            var footsteps=binding.Runtime.Footsteps;
            int before=footsteps.EmittedCount;
            feet.position+=Vector3.right;footsteps.Tick(.35f,false);
            Check("airborne_motion_does_not_emit",footsteps.EmittedCount==before);
            feet.position+=Vector3.right*.8f;footsteps.Tick(.35f,true);
            feet.position+=Vector3.right*.9f;footsteps.Tick(.35f,true);
            Check("grounded_stride_emits_one_step",footsteps.EmittedCount==before+1);
            footsteps.Tick(.35f,true);
            Check("stationary_player_does_not_emit",footsteps.EmittedCount==before+1);

            var voices=binding.Runtime.Voices; var geometry=binding.Runtime.Geometry;
            binding.Runtime.Clear();
            UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(room); UnityEngine.Object.Destroy(feet.gameObject); UnityEngine.Object.Destroy(rayObject);
            yield return new WaitForSecondsRealtime(.4f);
            RuntimeManager.StudioSystem.flushCommands();
            Check("destroy_map_releases_voices",voices.ActiveVoiceCount==0 && voices.CreatedInstanceCount==voices.ReleasedInstanceCount);
            Check("destroy_map_releases_geometry",geometry.NativeObjectCount==0 && geometry.TriangleCount==0);
            Check("no_fmod_backend_errors",voices.FailureCount==0,voices.LastFailure);
            Finish();
        }
        float Occlusion=>raySource.GetOutputs(SimulationFlags.Direct).direct.occlusion;
        static Vector3 V(AudioVector p)=>new Vector3(p.X,p.Y,p.Z);
        static GameObject Box(string name,Vector3 position,Vector3 scale)
        { var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.position=position;o.transform.localScale=scale;return o; }
        IEnumerator WaitFor(Func<bool> condition,float seconds)
        { float until=Time.realtimeSinceStartup+seconds;while (!condition() && Time.realtimeSinceStartup<until) yield return null; }
        void Check(string name,bool passed,string detail=null)
        { checks.Add(new CheckResult {name=name,passed=passed,detail=detail});if(!passed)failed=true;Debug.Log("[TunnelAcoustics] "+name+"="+passed+" "+detail); }
        void OnLog(string message,string trace,LogType type)
        { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)failed=true; }
        void Update()
        { if(!Completed && Time.realtimeSinceStartup-started>180){failed=true;Debug.LogError("Tunnel acoustic test timeout.");Finish();} }
        void Finish()
        {
            if(Completed)return;capture?.Dispose();capture=null;Passed=!failed && checks.Count>=30;Completed=true;
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report {passed=Passed,environment=UnityEngine.Application.isEditor?"Editor CLI":"Windows IL2CPP",checks=checks,triangleCounts=triangleCounts.ToArray(),seeds=seeds},true));
            Debug.Log("[TunnelAcoustics] Completed="+Passed);
            if(!UnityEngine.Application.isEditor && Argument("-tunnelAudioOutput")!=null)UnityEngine.Application.Quit(Passed?0:1);
        }
        void OnDestroy(){UnityEngine.Application.logMessageReceived-=OnLog;capture?.Dispose();}
        static string Argument(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:null;}
    }
}
