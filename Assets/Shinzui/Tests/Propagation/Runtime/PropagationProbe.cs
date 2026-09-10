using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FMODUnity;
using Shinzui.Application.SpatialAudio;
using Shinzui.Infrastructure.SpatialAudio;
using Shinzui.Infrastructure.TunnelAcoustics;
using Shinzui.AudioProbe.Infrastructure;
using SteamAudio;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Shinzui.Propagation.Tests
{
    public sealed class PropagationProbe : MonoBehaviour
    {
        public SpatialAudioCatalog Catalog;
        public Transform Listener;
        public static bool Completed { get; private set; }
        public static bool Passed { get; private set; }
        [Serializable] public sealed class Check { public string name; public bool passed; }
        [Serializable] public sealed class Sample
        {
            public string name;
            public float peak, occlusion, transmissionLow, transmissionMid, transmissionHigh;
            public float[] boundaryOcclusion;
        }
        [Serializable] sealed class Report { public bool passed; public string environment; public List<Check> checks; public List<Sample> samples; }
        readonly List<Check> checks = new List<Check>();
        readonly List<Sample> samples = new List<Sample>();
        SteamAudioAcousticSceneService geometry;
        FmodSpatialAudioService voices;
        SpatialVoiceHandle voice;
        SteamAudioSource source;
        ProbeCapture capture;
        GameObject room;
        string output;
        bool failed;
        float started;

        void Awake()
        {
            Completed=Passed=false;started=Time.realtimeSinceStartup;
            output=Argument("-propagationOutput") ?? Path.Combine(UnityEngine.Application.persistentDataPath,"PropagationProbe");
            Directory.CreateDirectory(output);UnityEngine.Application.logMessageReceived+=OnLog;
        }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2);
            capture=new ProbeCapture(RuntimeManager.CoreSystem);geometry=new SteamAudioAcousticSceneService();
            Listener.SetPositionAndRotation(new Vector3(0,1.5f,0),Quaternion.identity);
            SteamAudioManager.NotifyAudioListenerChangedTo(Listener);
            bool rejected=false;
            try
            {
                using(var invalid=new FmodSpatialAudioService(new SpatialAudioRuntimeOptions {Owner=transform,Catalog=Catalog,MaxVoices=1,
                    Propagation=new SpatialPropagationSettings {OcclusionSamples=SteamAudioSettings.Singleton.maxOcclusionSamples+1}})) { }
            }
            catch(ArgumentException){rejected=true;}
            Assert("native_occlusion_budget_enforced",rejected);
            yield return Tone("distance-2m",new Vector3(0,1.5f,2),Direct());
            yield return Tone("distance-4m",new Vector3(0,1.5f,4),Direct());
            yield return Tone("distance-8m",new Vector3(0,1.5f,8),Direct());
            BeginRoom();Box("Ground near foot contact",Vector3.zero,new Vector3(20,.2f,20));Register();
            yield return Prepare(new Vector3(0,.15f,2),Direct(),"tone",SpatialAudioCategory.Footstep);
            float groundOcclusion=source.GetOutputs(SimulationFlags.Direct).direct.occlusion;
            Debug.Log("[Propagation] Foot contact direct fraction="+groundOcclusion);
            Assert("foot_contact_is_not_self_occluded_by_floor",groundOcclusion>.95f);
            EndVoice();ClearRoom();yield return null;

            foreach(var material in new[]{AcousticSurfaceKind.Concrete,AcousticSurfaceKind.Metal,AcousticSurfaceKind.Wood})
            {
                BeginRoom();Box("Partition",new Vector3(0,2,1),new Vector3(20,4,.2f),material);Register();
                yield return Tone("wall-"+material.ToString().ToLowerInvariant(),new Vector3(0,1.5f,2),Direct());
                Assert("wall_occluded_"+material,sourceOcclusion<.01f);
                if(material==AcousticSurfaceKind.Concrete)
                { var noTransmission=Direct();noTransmission.Transmission=false;yield return Tone("wall-no-transmission",new Vector3(0,1.5f,2),noTransmission); }
                ClearRoom();yield return null;
            }

            // Stable room, open/closed doorway and isolated direct/reflection outputs.
            Listener.position=new Vector3(0,1.5f,-3);
            BeginRoom();Shell();Partition(0);Register();
            yield return Pulse("room-direct",new Vector3(0,1.5f,3),Direct());
            yield return Pulse("room-reflections",new Vector3(0,1.5f,3),Reflected());
            yield return Pulse("room-mix",new Vector3(0,1.5f,3),new SpatialPropagationSettings());
            var door=Box("Closed wood door",new Vector3(0,1,0),new Vector3(1.5f,2,.2f),AcousticSurfaceKind.Wood);
            Register();yield return Pulse("room-closed-door",new Vector3(0,1.5f,3),new SpatialPropagationSettings());
            Assert("closed_door_blocks_direct_path",sourceOcclusion<.01f);
            ClearRoom();yield return null;

            foreach(int sign in new[]{-1,1})
            {
                BeginRoom();Shell();Partition(sign*3);Register();
                yield return Pulse(sign<0?"opening-left":"opening-right",new Vector3(0,1.5f,3),Reflected());
                Assert("offset_opening_blocks_straight_path_"+sign,sourceOcclusion<.01f);
                ClearRoom();yield return null;
            }

            BeginRoom();Bend();Register();Listener.position=new Vector3(0,1.5f,-4);
            yield return Pulse("bend-direct",new Vector3(4,1.5f,0),Direct());
            yield return Pulse("bend-reflections",new Vector3(4,1.5f,0),Reflected());
            yield return Pulse("bend-mix",new Vector3(4,1.5f,0),new SpatialPropagationSettings());
            Assert("bend_blocks_straight_path",sourceOcclusion<.01f);
            ClearRoom();yield return null;

            // Move a native rigid door across the line of sight with no zone or preset switches.
            Listener.position=new Vector3(0,1.5f,0);
            yield return Boundary(false);
            yield return Boundary(true);
            yield return WalkThroughDoorway();
            Assert("all_native_geometry_released",geometry.NativeObjectCount==0);
            geometry.Dispose();geometry=null;Finish();
        }
        float sourceOcclusion;
        static SpatialPropagationSettings Direct()=>new SpatialPropagationSettings {Reflections=false,ReflectionGain=0};
        static SpatialPropagationSettings Reflected()=>new SpatialPropagationSettings {DirectGain=0,ReflectionGain=1};
        IEnumerator Prepare(Vector3 position,SpatialPropagationSettings settings,string id,SpatialAudioCategory category=SpatialAudioCategory.World)
        {
            voices=new FmodSpatialAudioService(new SpatialAudioRuntimeOptions {Owner=transform,Catalog=Catalog,MaxVoices=1,Propagation=settings});
            var request=voices.Prepare(new SpatialAudioRequest(id,AudioPose.At(position.x,position.y,position.z),category,128,.7f));
            Assert("accepted_"+samples.Count,request.Accepted);voice=request.Handle;
            float limit=Time.realtimeSinceStartup+8;
            while(voices.GetState(voice)!=SpatialVoiceState.Ready && Time.realtimeSinceStartup<limit)yield return null;
            Assert("ready_"+samples.Count,voices.GetState(voice)==SpatialVoiceState.Ready);
            if(!voices.TryInspect(voice,out var info))throw new InvalidOperationException("Missing prepared source.");
            source=info.PoolObject.GetComponent<SteamAudioSource>();
            yield return new WaitForSecondsRealtime(1);
        }
        IEnumerator Tone(string name,Vector3 position,SpatialPropagationSettings settings)
        {
            yield return Prepare(position,settings,"tone");Assert("started_"+name,voices.StartPrepared(voice));
            yield return new WaitForSecondsRealtime(.3f);
            capture.Begin();yield return new WaitForSecondsRealtime(1.2f);Save(name);EndVoice();yield return new WaitForSecondsRealtime(.2f);
        }
        IEnumerator Pulse(string name,Vector3 position,SpatialPropagationSettings settings)
        {
            yield return Prepare(position,settings,"pulse");capture.Begin();Assert("started_"+name,voices.StartPrepared(voice));
            yield return new WaitForSecondsRealtime(4.2f);Save(name);EndVoice();yield return new WaitForSecondsRealtime(.2f);
        }
        IEnumerator Boundary(bool volumetric)
        {
            var movingDoor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            movingDoor.name="Moving door boundary";movingDoor.transform.position=new Vector3(0,1.5f,1);
            movingDoor.transform.localScale=new Vector3(2,3,.2f);
            movingDoor.AddComponent<AcousticSurface>().Kind=AcousticSurfaceKind.Wood;
            movingDoor.AddComponent<MovingAcousticGeometry>();
            var settings=Direct();settings.VolumetricOcclusion=volumetric;
            yield return Prepare(new Vector3(0,1.5f,2),settings,"tone");voices.StartPrepared(voice);
            yield return new WaitForSecondsRealtime(.3f);
            capture.Begin();var values=new List<float>();float begin=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-begin<4.5f)
            {
                float elapsed=Time.realtimeSinceStartup-begin;
                movingDoor.transform.position=new Vector3(Mathf.Clamp01((elapsed-.5f)/3)*2,1.5f,1);
                values.Add(source.GetOutputs(SimulationFlags.Direct).direct.occlusion);yield return null;
            }
            Save(volumetric?"boundary-volumetric":"boundary-raycast",values.ToArray());
            if(volumetric)Assert("boundary_contains_partial_occlusion",values.Exists(v=>v>.05f&&v<.95f));
            EndVoice();Destroy(movingDoor);yield return new WaitForSecondsRealtime(.3f);
        }
        IEnumerator WalkThroughDoorway()
        {
            BeginRoom();Shell();Partition(0);Register();
            var start=new Vector3(-2,1.5f,-3);var entrance=new Vector3(0,1.5f,0);var end=new Vector3(1,1.5f,3);
            Listener.position=start;
            yield return Prepare(new Vector3(3,1.5f,3),new SpatialPropagationSettings(),"tone");
            Assert("doorway_walk_started",voices.StartPrepared(voice));yield return new WaitForSecondsRealtime(.3f);
            capture.Begin();float begin=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-begin<5.5f)
            {
                float t=Mathf.Clamp01((Time.realtimeSinceStartup-begin-.5f)/4.5f);
                Listener.position=t<.5f?Vector3.Lerp(start,entrance,t*2):Vector3.Lerp(entrance,end,(t-.5f)*2);
                yield return null;
            }
            Save("walk-through-doorway");EndVoice();ClearRoom();yield return new WaitForSecondsRealtime(.3f);
        }
        void Save(string name,float[] values=null)
        {
            var direct=source.GetOutputs(SimulationFlags.Direct).direct;
            sourceOcclusion=direct.occlusion;
            samples.Add(new Sample {name=name,peak=capture.Finish(Path.Combine(output,name+".wav")),occlusion=direct.occlusion,
                transmissionLow=direct.transmissionLow,transmissionMid=direct.transmissionMid,transmissionHigh=direct.transmissionHigh,boundaryOcclusion=values});
            Assert("capture_within_capacity_"+name,!capture.Overflowed);
        }
        void EndVoice()
        {
            voices.Dispose();Assert("voices_released_"+samples.Count,voices.CreatedInstanceCount==voices.ReleasedInstanceCount&&voices.ActiveVoiceCount==0);
            Assert("backend_clean_"+samples.Count,voices.FailureCount==0);voices=null;source=null;
        }
        void BeginRoom(){room=new GameObject("Propagation test geometry");}
        GameObject Box(string name,Vector3 position,Vector3 scale,AcousticSurfaceKind kind=AcousticSurfaceKind.Concrete)
        {
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(room.transform,false);
            box.transform.position=position;box.transform.localScale=scale;box.AddComponent<AcousticSurface>().Kind=kind;return box;
        }
        void Register(){geometry.Replace(AcousticGeometryCollector.Collect(room.transform,null));}
        void ClearRoom(){geometry.Clear();Destroy(room);room=null;}
        void Shell()
        {
            Box("Floor",new Vector3(0,0,0),new Vector3(10,.2f,12));Box("Ceiling",new Vector3(0,3,0),new Vector3(10,.2f,12));
            Box("Left",new Vector3(-5,1.5f,0),new Vector3(.2f,3,12));Box("Right",new Vector3(5,1.5f,0),new Vector3(.2f,3,12));
            Box("Back",new Vector3(0,1.5f,-6),new Vector3(10,3,.2f));Box("Front",new Vector3(0,1.5f,6),new Vector3(10,3,.2f));
        }
        void Partition(float opening)
        {
            float left=opening-.75f,right=opening+.75f;
            Box("Partition left",new Vector3((-5+left)*.5f,1.5f,0),new Vector3(left+5,3,.2f));
            Box("Partition right",new Vector3((right+5)*.5f,1.5f,0),new Vector3(5-right,3,.2f));
            Box("Lintel",new Vector3(opening,2.5f,0),new Vector3(1.5f,1,.2f));
        }
        void Bend()
        {
            foreach(float y in new[]{0f,3f})
            {Box("Vertical shell",new Vector3(0,y,-2),new Vector3(2,.2f,6));Box("Horizontal shell",new Vector3(3,y,0),new Vector3(4,.2f,2));}
            Box("Left outer",new Vector3(-1,1.5f,-2),new Vector3(.2f,3,6));
            Box("Front outer",new Vector3(2,1.5f,1),new Vector3(6,3,.2f));
            Box("Back end",new Vector3(0,1.5f,-5),new Vector3(2,3,.2f));Box("Right end",new Vector3(5,1.5f,0),new Vector3(.2f,3,2));
            Box("Inner vertical",new Vector3(1,1.5f,-3),new Vector3(.2f,3,4));Box("Inner horizontal",new Vector3(3,1.5f,-1),new Vector3(4,3,.2f));
        }
        void Update()
        {
            voices?.Tick();
            if(!Completed && Time.realtimeSinceStartup-started>240){failed=true;Debug.LogError("Propagation probe timed out.");Finish();}
        }
        void Assert(string name,bool success){checks.Add(new Check{name=name,passed=success});if(!success)failed=true;Debug.Log("[Propagation] "+name+"="+success);}
        void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)failed=true;}
        void Finish()
        {
            if(Completed)return;voices?.Dispose();voices=null;geometry?.Dispose();geometry=null;capture?.Dispose();capture=null;
            Passed=!failed&&samples.Count==19;Completed=true;
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report {passed=Passed,environment=UnityEngine.Application.isEditor?"Editor CLI":"Windows IL2CPP",checks=checks,samples=samples},true));
            Debug.Log("[Propagation] Completed="+Passed);
            if(!UnityEngine.Application.isEditor&&Argument("-propagationOutput")!=null)UnityEngine.Application.Quit(Passed?0:1);
        }
        void OnDestroy(){UnityEngine.Application.logMessageReceived-=OnLog;voices?.Dispose();geometry?.Dispose();capture?.Dispose();}
        static string Argument(string key){var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,key);return index>=0&&index+1<args.Length?args[index+1]:null;}
    }
}
