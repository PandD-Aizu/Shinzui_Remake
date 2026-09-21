using System;
using FMOD;
using NUnit.Framework;
using Shinzui.Application.SpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio;
using UnityEngine;
using static Shinzui.Infrastructure.CustomSpatialAudio.FmodRoomAcousticProcessor;

namespace Shinzui.Tests.CustomSpatialAudio
{
    public sealed class CustomSpatialAudioServiceTests
    {
        FMOD.System system;
        CustomSpatialAudioConfiguration config;
        CustomSpatialAudioService service;
        DSP meter;
        bool initialized;

        [SetUp]
        public void SetUp()
        {
            Check(Factory.System_Create(out system));
            Check(system.setOutput(OUTPUTTYPE.NOSOUND_NRT));
            Check(system.setSoftwareFormat(48000, SPEAKERMODE.STEREO, 0));
            Check(system.setDSPBufferSize(512, 4));
            Check(system.init(64, INITFLAGS.NORMAL, IntPtr.Zero)); initialized = true;
            Check(system.getMasterChannelGroup(out var master));
            Check(master.getDSP(CHANNELCONTROL_DSP_INDEX.FADER, out meter));
            Check(meter.setMeteringEnabled(false, true));
            config = ScriptableObject.CreateInstance<CustomSpatialAudioConfiguration>(); config.MaxVoices = 4;
            config.Sounds = new[] { new CustomSpatialAudioConfiguration.DrySound {
                Id = "step", Path = "CustomSpatialAudio/Audio/FootstepPrototype.wav" } };
            service = new CustomSpatialAudioService(system, master, config, UnityEngine.Application.streamingAssetsPath);
            service.SetWorld(new GeneratedAcousticWorld(GeneratedAcousticWorldTests.TwoRooms()));
            service.SetListener(AudioPose.At(0, 1.5f, 0));
        }
        [TearDown]
        public void TearDown()
        {
            service?.Dispose();
            if (initialized) Check(system.close());
            if (system.hasHandle()) Check(system.release());
            if (config) UnityEngine.Object.DestroyImmediate(config);
        }
        SpatialAudioRequest Request(int priority = 128) => new SpatialAudioRequest("step", AudioPose.At(1, 1, 0), priority: priority);
        void Step(int blocks) { for (int i = 0; i < blocks; i++) { service.Tick(512f / 48000); Check(system.update()); } }
        float Peak()
        { Check(meter.getMeteringInfo(IntPtr.Zero, out DSP_METERING_INFO info)); return Math.Max(info.peaklevel[0], info.peaklevel[1]); }

        [Test]
        public void PreparedVoicesAreSilentThenDetachAndProduceAudibleTail()
        {
            var target = new PoseSource { Pose = AudioPose.At(1, 1, 0) };
            var result = service.Prepare(Request(), target);
            Assert.That(result.Accepted); Step(20);
            Assert.That(service.GetState(result.Handle), Is.EqualTo(SpatialVoiceState.Ready));
            Assert.That(Peak(), Is.Zero);
            Assert.That(service.StartPrepared(result.Handle)); Step(8);
            Assert.That(Peak(), Is.GreaterThan(1e-5f));
            target.Pose = AudioPose.At(8, 1, 0); Step(20);
            service.TryInspect(result.Handle, out _, out var emitted);
            Assert.That(emitted.Position.X, Is.EqualTo(1));
            Assert.That(Peak(), Is.GreaterThan(1e-8f), "Tail must survive the 0.2 second dry sound.");
            Step(600); Assert.That(service.ActiveVoiceCount, Is.Zero);
            Assert.That(service.CallbackErrorCount, Is.Zero);
        }
        [Test]
        public void MuteAndPauseAffectTailAndFreezeExpiry()
        {
            var voice = service.Play(Request()).Handle; Step(24);
            Assert.That(service.SetVolume(voice, 0)); Step(4); Assert.That(Peak(), Is.LessThan(1e-9f));
            service.SetVolume(voice, 1); Step(4); Assert.That(Peak(), Is.GreaterThan(1e-8f));
            service.SetSuspended(true); Step(600);
            Assert.That(service.GetState(voice), Is.EqualTo(SpatialVoiceState.Paused));
            Assert.That(Peak(), Is.LessThan(1e-9f));
            service.SetSuspended(false); Step(4); Assert.That(Peak(), Is.GreaterThan(1e-8f));
        }
        [Test]
        public void NewContactCanRecycleAnOldTailAtTheVoiceLimit()
        {
            var first = service.Play(Request()).Handle;
            for (int i = 1; i < 4; i++) service.Play(Request());
            Step(25);
            Assert.That(service.GetState(first), Is.EqualTo(SpatialVoiceState.Stopping));
            Assert.That(service.Prepare(Request()).Accepted);
            Assert.That(service.GetState(first), Is.EqualTo(SpatialVoiceState.Stopped));
            Assert.That(service.ProcessorCount, Is.EqualTo(4));
        }
        [Test]
        public void PoolBoundPriorityAndReuseDoNotLeakOldTailOrHandles()
        {
            var first = service.Play(Request(1)).Handle;
            for (int i = 1; i < 4; i++) Assert.That(service.Play(Request(1)).Accepted);
            Assert.That(service.Play(Request(1)).Status, Is.EqualTo(SpatialAudioPlayStatus.VoiceLimit));
            Assert.That(service.Play(Request(2)).Accepted);
            Assert.That(service.GetState(first), Is.EqualTo(SpatialVoiceState.Stopped));
            Step(8); service.StopAll(SpatialAudioStopMode.Immediate); Step(4);
            Assert.That(Peak(), Is.LessThan(1e-9f));
            var ready = service.Prepare(Request()).Handle; Step(8);
            Assert.That(Peak(), Is.LessThan(1e-9f));
            Assert.That(service.StartPrepared(ready)); Step(4);
            Assert.That(Peak(), Is.GreaterThan(1e-5f));
            Assert.That(service.ProcessorCount, Is.EqualTo(4));
            service.SetWorld(null); Assert.That(service.ActiveVoiceCount, Is.Zero);
            Assert.That(service.Play(Request()).Status, Is.EqualTo(SpatialAudioPlayStatus.BackendUnavailable));
        }
        sealed class PoseSource : IAudioPoseSource
        { public AudioPose Pose; public bool TryGetPose(out AudioPose pose) { pose = Pose; return true; } }
    }
}
