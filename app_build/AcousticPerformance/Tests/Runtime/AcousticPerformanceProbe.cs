using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using FMODUnity;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Application.SpatialAudio;
using Shinzui.Application.UseCases.Tunnel;
using Shinzui.AudioProbe.Infrastructure;
using Shinzui.DI.TunnelAcoustics;
using Shinzui.Domain.DomainServices.Tunnel;
using Shinzui.Infrastructure.SpatialAudio;
using Shinzui.Infrastructure.TunnelAcoustics;
using Shinzui.Presentation.GenerateTunnel;
using Shinzui.View.GenerateTunnel;
using SteamAudio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;
using Vector3 = UnityEngine.Vector3;
using Scene = UnityEngine.SceneManagement.Scene;

namespace Shinzui.AcousticPerformance.Tests
{
    // Deliberately isolated benchmark; never changes the serialized production Steam Audio asset.
    public sealed class AcousticPerformanceProbe : MonoBehaviour
    {
        public TunnelAudioConfiguration Configuration;
        public SpatialAudioCatalog Catalog;
        public GameObject TunnelTemplate, CorridorTemplate;
        public Transform Listener;
        public static bool Completed { get; private set; }
        public static bool Passed { get; private set; }
        [Serializable] public sealed class Check { public string name; public bool passed; }
        [Serializable] public sealed class Measurement
        {
            public string name; public int voices, triangles, virtualVoices, frames, outputStarvations;
            public float peak, prepareMs, frameP50, frameP95, frameP99, frameMax, dspMean, dspMax;
            public double processCorePercent, processPrivateMB, unityAllocatedMB, fmodAllocatedMB;
            public float[] frameTimes, dspPercent;
        }
        [Serializable] public sealed class MemorySample
        {
            public int cycle, triangles, materials, navMeshes; public double processPrivateMB, unityAllocatedMB, fmodAllocatedMB;
            public float generateMs;
        }
        [Serializable] sealed class Report
        {
            public bool passed; public string environment, quality, cpu, gpu, unityVersion;
            public int logicalCores, ramMB, rays, bounces, maxSources, sampleRate;
            public float updateInterval, irDuration; public List<Check> checks; public List<Measurement> measurements;
            public List<MemorySample> memory;
        }
        readonly List<Check> checks = new List<Check>();
        readonly List<Measurement> measurements = new List<Measurement>();
        readonly List<MemorySample> memory = new List<MemorySample>();
        readonly List<SpatialVoiceHandle> handles = new List<SpatialVoiceHandle>(32);
        ProbeCapture capture;
        TunnelAudioBinding binding;
        TunnelMapView view;
        TunnelAudioConfiguration config;
        GenerateTunnelPresenter presenter;
        Scene generatedScene;
        string output, quality;
        volatile bool failed;
        float started;
        Vector3 sourcePosition;
        int starvations;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void ConfigureBenchmarkBudget()
        {
            if (Argument("-acousticPerformanceOutput") == null) return;
            var settings=SteamAudioSettings.Singleton;
            settings.realTimeMaxSources=32;
            bool economy=Argument("-acousticQuality")=="economy";
            settings.realTimeRays=economy?4096:8192;
            settings.realTimeBounces=economy?32:64;
            settings.realTimeDuration=economy?1.5f:3f;
            settings.simulationUpdateInterval=economy?.15f:.1f;
        }
        void Awake()
        {
            Completed=Passed=false;started=Time.realtimeSinceStartup;
            output=Argument("-acousticPerformanceOutput") ?? Path.Combine(UnityEngine.Application.persistentDataPath,"AcousticPerformance");
            quality=Argument("-acousticQuality") ?? "full";
            Directory.CreateDirectory(output);UnityEngine.Application.logMessageReceivedThreaded+=OnLog;
            QualitySettings.vSyncCount=0;UnityEngine.Application.targetFrameRate=120;
        }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2);
            CheckResult("benchmark_native_capacity_32",SteamAudioSettings.Singleton.realTimeMaxSources==32);
            capture=new ProbeCapture(RuntimeManager.CoreSystem);
            config=Instantiate(Configuration);config.Events=Catalog;config.MaxVoices=32;
            generatedScene=SceneManager.CreateScene("Acoustic performance generated scene");
            var root=new GameObject("MapRoot");SceneManager.MoveGameObjectToScene(root,generatedScene);
            view=root.AddComponent<TunnelMapView>();view.SetBuildTemplates(TunnelTemplate,CorridorTemplate);
            binding=TunnelAudioBinding.Attach(view,config);
            presenter=new GenerateTunnelPresenter(new GenerateTunnelUseCase(new TunnelLayoutGenerator()),view);
            yield return Generate(2777);
            Transform corridor=null;
            foreach(var candidate in view.GeometryRoot.GetComponentsInChildren<Transform>())
                if(candidate.name.StartsWith("Corridor ") && candidate.Find("Walkable Floor Collider")){corridor=candidate;break;}
            if(!corridor)throw new InvalidOperationException("Actual generated corridor not found.");
            Listener.SetPositionAndRotation(corridor.TransformPoint(new Vector3(0,1,0)),corridor.rotation);
            sourcePosition=corridor.TransformPoint(new Vector3(0,1,2));
            SteamAudioManager.NotifyAudioListenerChangedTo(Listener);
            yield return new WaitForSecondsRealtime(2);
            yield return Measure(0);
            foreach(int count in new[]{1,8,12,16,32}) yield return Measure(count);
            yield return ProductionCapacity();
            yield return PauseRegression();
            yield return MixerRegression();
            // Exercise the real generator, collector, native registration and pooled voice cleanup.
            for(int cycle=0;cycle<30;cycle++)
            {
                float begin=Time.realtimeSinceStartup;
                yield return Generate(2777+cycle);
                float generateMs=(Time.realtimeSinceStartup-begin)*1000;
                yield return StartVoices(8);
                yield return new WaitForSecondsRealtime(.15f);
                binding.Runtime.Clear();
                yield return new WaitForSecondsRealtime(.3f);
                CheckResult("released_cycle_"+cycle,binding.Runtime.Geometry.NativeObjectCount==0 &&
                    binding.Runtime.Voices.ActiveVoiceCount==0 && binding.Runtime.Voices.CreatedInstanceCount==binding.Runtime.Voices.ReleasedInstanceCount);
                GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
                FMOD.Memory.GetStats(out int fmod,out _);
                int navMeshes=0;foreach(var data in Resources.FindObjectsOfTypeAll<UnityEngine.AI.NavMeshData>())if(data.name=="MapRoot")navMeshes++;
                memory.Add(new MemorySample{cycle=cycle,generateMs=generateMs,triangles=lastTriangles,materials=Resources.FindObjectsOfTypeAll<UnityEngine.Material>().Length,navMeshes=navMeshes,
                    processPrivateMB=PrivateMemoryMB(),unityAllocatedMB=Profiler.GetTotalAllocatedMemoryLong()/1048576.0,fmodAllocatedMB=fmod/1048576.0});
            }
            yield return Generate(42);yield return StartVoices(8);
            var service=binding.Runtime.Voices;var geometry=binding.Runtime.Geometry;var sceneGeometry=view.GeometryRoot;
            yield return SceneManager.UnloadSceneAsync(generatedScene);
            yield return new WaitForSecondsRealtime(1);
            CheckResult("additive_scene_unload_releases_all",service.ActiveVoiceCount==0 && service.CreatedInstanceCount==service.ReleasedInstanceCount && geometry.NativeObjectCount==0);
            CheckResult("additive_scene_unload_removes_generated_hierarchy",!sceneGeometry && !root);
            int remainingNavMeshes=0;foreach(var data in Resources.FindObjectsOfTypeAll<UnityEngine.AI.NavMeshData>())if(data.name=="MapRoot")remainingNavMeshes++;
            CheckResult("scene_unload_releases_owned_navmesh",remainingNavMeshes==0);
            yield return LegacyAudio("after-unload");
            Finish();
        }
        int lastTriangles;
        IEnumerator Generate(int seed)
        {
            int build=binding.BuildCount;
            presenter=new GenerateTunnelPresenter(new GenerateTunnelUseCase(new TunnelLayoutGenerator()),view);
            CheckResult("generate_"+seed,presenter.ExecuteGeneration(new TunnelGenerationRequestDto {Seed=seed,TunnelCount=4,SmallRoomCount=1}));
            float deadline=Time.realtimeSinceStartup+10;
            while(binding.BuildCount==build && Time.realtimeSinceStartup<deadline)yield return null;
            lastTriangles=binding.Runtime.Geometry.TriangleCount;
            CheckResult("registered_"+seed,binding.BuildCount==build+1 && lastTriangles>3000 && binding.Runtime.Geometry.NativeObjectCount==1);
        }
        IEnumerator StartVoices(int count)
        {
            var service=binding.Runtime.Voices;handles.Clear();
            for(int i=0;i<count;i++)
            {
                var p=sourcePosition+Listener.right*((i%4)*.08f);
                var result=service.Play(new SpatialAudioRequest("tone",AudioPose.At(p.x,p.y,p.z),SpatialAudioCategory.World,128,.5f/count));
                CheckResult("voice_accepted_"+count+"_"+i,result.Accepted);handles.Add(result.Handle);
            }
            float deadline=Time.realtimeSinceStartup+12;
            while(!AllPlaying() && Time.realtimeSinceStartup<deadline)yield return null;
            CheckResult("voices_playing_"+count,AllPlaying() && service.ActiveVoiceCount==count);
        }
        bool AllPlaying(){foreach(var h in handles)if(binding.Runtime.Voices.GetState(h)!=SpatialVoiceState.Playing)return false;return true;}
        IEnumerator Measure(int count)
        {
            float begin=Time.realtimeSinceStartup;
            yield return StartVoices(count);
            float prepareMs=(Time.realtimeSinceStartup-begin)*1000;
            yield return new WaitForSecondsRealtime(4);
            var frameTimes=new List<float>(2000);var dsp=new List<float>(2000);
            int starvationStart=System.Threading.Volatile.Read(ref starvations);
            double cpuStart=ProcessCpuSeconds();double start=Time.realtimeSinceStartupAsDouble,previous=start;
            capture.Begin();
            while(Time.realtimeSinceStartupAsDouble-start<8)
            {
                yield return null;
                double now=Time.realtimeSinceStartupAsDouble;frameTimes.Add((float)((now-previous)*1000));previous=now;
                RuntimeManager.CoreSystem.getCPUUsage(out var cpu);dsp.Add(cpu.dsp);
            }
            double elapsed=Time.realtimeSinceStartupAsDouble-start;
            double corePercent=(ProcessCpuSeconds()-cpuStart)/elapsed*100;
            string name="voices-"+count;
            float peak=capture.Finish(Path.Combine(output,name+".wav"));
            CheckResult("capture_"+name,!capture.Overflowed && peak<1 && (count==0 || peak>.0001f));
            int virtualCount=0;
            foreach(var handle in handles)
            {
                CheckResult("voice_survived_"+name,binding.Runtime.Voices.TryInspect(handle,out var info));
                var instance=new FMOD.Studio.EventInstance{handle=info.NativeInstance};instance.isVirtual(out bool isVirtual);if(isVirtual)virtualCount++;
            }
            CheckResult("no_virtualized_"+name,virtualCount==0);
            FMOD.Memory.GetStats(out int fmod,out _);
            var frames=frameTimes.ToArray();var sorted=(float[])frames.Clone();Array.Sort(sorted);
            var sample=new Measurement{name=name,voices=count,triangles=lastTriangles,virtualVoices=virtualCount,frames=frames.Length,outputStarvations=System.Threading.Volatile.Read(ref starvations)-starvationStart,
                peak=peak,prepareMs=prepareMs,frameP50=Percentile(sorted,.5f),frameP95=Percentile(sorted,.95f),frameP99=Percentile(sorted,.99f),frameMax=sorted[sorted.Length-1],
                frameTimes=frames,dspPercent=dsp.ToArray(),dspMean=Mean(dsp),dspMax=Max(dsp),processCorePercent=corePercent,
                processPrivateMB=PrivateMemoryMB(),unityAllocatedMB=Profiler.GetTotalAllocatedMemoryLong()/1048576.0,fmodAllocatedMB=fmod/1048576.0};
            measurements.Add(sample);
            Debug.Log("[AcousticPerformance] "+name+" p95="+sample.frameP95+" DSP="+sample.dspMean+" CPU(core%)="+corePercent);
            binding.Runtime.Voices.StopAll(SpatialAudioStopMode.Immediate);yield return new WaitForSecondsRealtime(.5f);
        }
        IEnumerator PauseRegression()
        {
            yield return StartVoices(2);var service=binding.Runtime.Voices;
            service.SetPaused(handles[1],true);
            Time.timeScale=0;yield return new WaitForSecondsRealtime(.3f);
            service.TryInspect(handles[0],out var before);
            capture.Begin();yield return new WaitForSecondsRealtime(1);float peak=capture.Finish(Path.Combine(output,"paused.wav"));
            service.TryInspect(handles[0],out var after);
            CheckResult("timescale_zero_freezes_timeline",before.TimelineMilliseconds==after.TimelineMilliseconds && service.GetState(handles[0])==SpatialVoiceState.Paused);
            CheckResult("paused_output_silent",peak<.0001f);
            var pending=service.Prepare(new SpatialAudioRequest("tone",AudioPose.At(sourcePosition.x,sourcePosition.y,sourcePosition.z)));
            yield return new WaitForSecondsRealtime(1);
            CheckResult("prepare_during_pause_ready",service.GetState(pending.Handle)==SpatialVoiceState.Ready);
            CheckResult("start_during_pause_stays_paused",service.StartPrepared(pending.Handle) && service.GetState(pending.Handle)==SpatialVoiceState.Paused);
            Time.timeScale=1;yield return new WaitForSecondsRealtime(.2f);
            CheckResult("resume_preserves_individual_pause",service.GetState(handles[0])==SpatialVoiceState.Playing && service.GetState(handles[1])==SpatialVoiceState.Paused && service.GetState(pending.Handle)==SpatialVoiceState.Playing);
            service.StopAll(SpatialAudioStopMode.Immediate);
        }
        IEnumerator MixerRegression()
        {
            yield return LegacyAudio("before-unload");
            // Exercise the actual settings applier + VCA service; use an in-memory repository so user settings are untouched.
            using(var vca=new Shinzui.Infrastructure.Services.FMODVCAService(new MemorySettings()))
            {
                var applier=new Shinzui.Infrastructure.Services.UnitySettingsApplier(vca);
                var settings=new Shinzui.Domain.Settings.AudioSettings {SystemVolume=1,BgmVolume=1,SeVolume=1};
                foreach(float volume in new[]{1f,.5f,0f})
                {
                    settings.SeVolume=volume;applier.ApplyAudio(settings);yield return new WaitForSecondsRealtime(.15f);
                    yield return RecordFoot("foot-se-"+volume.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture));
                }
                settings.SeVolume=1;settings.SystemVolume=0;applier.ApplyAudio(settings);yield return new WaitForSecondsRealtime(.15f);
                yield return RecordFoot("foot-master-muted");
                settings.SystemVolume=1;applier.ApplyAudio(settings);
                foreach(float volume in new[]{1f,0f})
                {
                    settings.BgmVolume=volume;applier.ApplyAudio(settings);yield return new WaitForSecondsRealtime(.15f);
                    yield return RecordLegacy("bgm-volume-"+(int)volume,"event:/BGM/TunnelAmbient");
                }
                yield return RecordFoot("foot-bgm-muted");
                settings.BgmVolume=1;
                foreach(float volume in new[]{1f,0f})
                {
                    settings.SeVolume=volume;applier.ApplyAudio(settings);yield return new WaitForSecondsRealtime(.15f);
                    yield return RecordLegacy("button-volume-"+(int)volume,"event:/SE/FlashLightButtonSE");
                }
                settings.SeVolume=1;applier.ApplyAudio(settings);
            }
            var bus=RuntimeManager.GetBus("bus:/WorldSE");bus.setMute(true);yield return new WaitForSecondsRealtime(.15f);
            yield return RecordFoot("foot-bus-muted");bus.setMute(false);
        }
        IEnumerator ProductionCapacity()
        {
            using(var service=new FmodSpatialAudioService(new SpatialAudioRuntimeOptions {Owner=transform,Catalog=Catalog,MaxVoices=Configuration.MaxVoices,Propagation=Configuration.Propagation}))
            {
                var request=new SpatialAudioRequest("tone",AudioPose.At(sourcePosition.x,sourcePosition.y,sourcePosition.z),SpatialAudioCategory.World,128,.01f);
                for(int i=0;i<Configuration.MaxVoices;i++)CheckResult("production_slot_"+i,service.Prepare(request).Accepted);
                var overflow=service.Prepare(request);
                CheckResult("production_cap_rejects_equal_priority_overflow",!overflow.Accepted && overflow.Status==SpatialAudioPlayStatus.VoiceLimit && service.ActiveVoiceCount==Configuration.MaxVoices);
                service.StopAll(SpatialAudioStopMode.Immediate);
                CheckResult("capacity_test_releases_instances",service.CreatedInstanceCount==service.ReleasedInstanceCount);
            }
            yield return new WaitForSecondsRealtime(.4f);
        }
        IEnumerator RecordLegacy(string name,string path)
        {
            var instance=RuntimeManager.CreateInstance(path);instance.set3DAttributes(RuntimeUtils.To3DAttributes(Listener.position));
            capture.Begin();instance.start();yield return new WaitForSecondsRealtime(2);
            float peak=capture.Finish(Path.Combine(output,name+".wav"));
            CheckResult(name+"_capture",!capture.Overflowed && peak<1);
            instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);instance.release();yield return new WaitForSecondsRealtime(.5f);
        }
        IEnumerator RecordFoot(string name)
        {
            using(var service=new FmodSpatialAudioService(new SpatialAudioRuntimeOptions {Owner=transform,Catalog=Configuration.Events,MaxVoices=1,
                Propagation=new SpatialPropagationSettings {Reflections=false}}))
            {
                var p=sourcePosition;
                var h=service.Prepare(new SpatialAudioRequest(Configuration.FootstepEventId,AudioPose.At(p.x,p.y,p.z),SpatialAudioCategory.Footstep)).Handle;
                float deadline=Time.realtimeSinceStartup+5;
                while(service.GetState(h)!=SpatialVoiceState.Ready && Time.realtimeSinceStartup<deadline){service.Tick();yield return null;}
                capture.Begin();CheckResult("foot_start_"+name,service.StartPrepared(h));
                float until=Time.realtimeSinceStartup+1.5f;
                while(Time.realtimeSinceStartup<until){service.Tick();yield return null;}
                float peak=capture.Finish(Path.Combine(output,name+".wav"));
                CheckResult(name+"_capture",!capture.Overflowed && peak<1);
            }
            yield return new WaitForSecondsRealtime(.2f);
        }
        IEnumerator LegacyAudio(string suffix)
        {
            // These are existing authored events, without adding spatial components to their playback path.
            foreach(var path in new[]{"event:/BGM/TunnelAmbient","event:/SE/FlashLightButtonSE"})
            {
                var instance=RuntimeManager.CreateInstance(path);instance.set3DAttributes(RuntimeUtils.To3DAttributes(Listener.position));instance.setVolume(.2f);instance.start();
                yield return new WaitForSecondsRealtime(.15f);
                instance.getPlaybackState(out var state);
                CheckResult("legacy_plays_"+path+suffix,state!=FMOD.Studio.PLAYBACK_STATE.STOPPED);
                bool spatial=false;
                if(instance.getChannelGroup(out var group)==FMOD.RESULT.OK && group.getNumDSPs(out int count)==FMOD.RESULT.OK)
                    for(int i=0;i<count;i++)if(group.getDSP(i,out var effect)==FMOD.RESULT.OK && effect.getInfo(out string name,out _,out _,out _,out _)==FMOD.RESULT.OK && name=="Steam Audio Spatializer")spatial=true;
                CheckResult("legacy_no_steam_spatializer_"+path+suffix,!spatial);
                instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);instance.release();
            }
        }
        sealed class MemorySettings : Shinzui.Application.Interfaces.IFMODSettingsRepository
        {
            public float MasterVolume{get;set;}=1;public float BgmVolume{get;set;}=1;public float SeVolume{get;set;}=1;
            public void SaveSettings(){} public async Cysharp.Threading.Tasks.UniTaskVoid LoadSettingsAsync(){await Cysharp.Threading.Tasks.UniTask.CompletedTask;}
        }
        void Update(){if(!Completed && Time.realtimeSinceStartup-started>360){failed=true;Debug.LogError("Acoustic performance probe timeout.");Finish();}}
        void CheckResult(string name,bool success){checks.Add(new Check{name=name,passed=success});if(!success){failed=true;Debug.LogWarning("[AcousticPerformance] Failed: "+name);}}
        void OnLog(string message,string trace,LogType type)
        {
            if(message.Contains("Starvation detected"))System.Threading.Interlocked.Increment(ref starvations);
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)failed=true;
        }
        void Finish()
        {
            if(Completed)return;Time.timeScale=1;
            CheckResult("no_backend_failures",!binding || binding.Runtime.Voices.FailureCount==0);
            Passed=!failed&&measurements.Count==6&&memory.Count==30;Completed=true;
            var s=SteamAudioSettings.Singleton;
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report {passed=Passed,environment=UnityEngine.Application.isEditor?"Editor CLI":"Windows IL2CPP Development",
                quality=quality,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,logicalCores=SystemInfo.processorCount,ramMB=SystemInfo.systemMemorySize,unityVersion=UnityEngine.Application.unityVersion,
                rays=s.realTimeRays,bounces=s.realTimeBounces,maxSources=s.realTimeMaxSources,updateInterval=s.simulationUpdateInterval,irDuration=s.realTimeDuration,sampleRate=capture.SampleRate,checks=checks,measurements=measurements,memory=memory},true));
            capture.Dispose();capture=null;Debug.Log("[AcousticPerformance] Completed="+Passed);
            if(!UnityEngine.Application.isEditor)UnityEngine.Application.Quit(Passed?0:1);
        }
        void OnDestroy(){UnityEngine.Application.logMessageReceivedThreaded-=OnLog;capture?.Dispose();if(config)Destroy(config);}
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetProcessTimes(IntPtr process,out long created,out long exited,out long kernel,out long user);
        static double ProcessCpuSeconds()
        {
            if(!GetProcessTimes(new IntPtr(-1),out _,out _,out long kernel,out long user))throw new InvalidOperationException("GetProcessTimes failed: "+Marshal.GetLastWin32Error());
            return (kernel+user)*1e-7;
        }
        // Unity's Mono Process.PrivateMemorySize64 returned 0 on this host. Read committed private bytes from Win32.
        [StructLayout(LayoutKind.Sequential)] struct ProcessMemory
        {
            public uint cb, pageFaults;
            public UIntPtr peakWorkingSet, workingSet, peakPagedPool, pagedPool, peakNonPagedPool, nonPagedPool, pagefile, peakPagefile, privateUsage;
        }
        [DllImport("psapi.dll",SetLastError=true)] static extern bool GetProcessMemoryInfo(IntPtr process,ref ProcessMemory memory,uint size);
        static double PrivateMemoryMB()
        {
            var m=new ProcessMemory {cb=(uint)Marshal.SizeOf<ProcessMemory>()};
            if(!GetProcessMemoryInfo(new IntPtr(-1),ref m,m.cb))throw new InvalidOperationException("GetProcessMemoryInfo failed: "+Marshal.GetLastWin32Error());
            return m.privateUsage.ToUInt64()/1048576.0;
        }
        static float Percentile(float[] values,float p)=>values[Math.Min(values.Length-1,(int)(values.Length*p))];
        static float Mean(List<float> values){float sum=0;foreach(float v in values)sum+=v;return sum/values.Count;}
        static float Max(List<float> values){float max=0;foreach(float v in values)max=Math.Max(max,v);return max;}
        static string Argument(string key){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null;}
    }
}
