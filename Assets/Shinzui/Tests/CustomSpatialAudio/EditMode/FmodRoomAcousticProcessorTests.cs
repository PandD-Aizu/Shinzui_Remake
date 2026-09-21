using System;
using System.Runtime.InteropServices;
using FMOD;
using NUnit.Framework;
using Shinzui.Application.CustomSpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio.Dsp;
using static Shinzui.Infrastructure.CustomSpatialAudio.FmodRoomAcousticProcessor;

namespace Shinzui.Tests.CustomSpatialAudio
{
    public sealed class FmodRoomAcousticProcessorTests
    {
        [Test]
        public void NativeGroupMutesAndPausesStoredTailThenCanBeRecreated()
        {
            Check(Factory.System_Create(out FMOD.System system));
            Sound sound = default;
            FmodRoomAcousticProcessor effect = null;
            bool initialized = false;
            try
            {
                Check(system.setOutput(OUTPUTTYPE.NOSOUND_NRT));
                Check(system.setSoftwareFormat(48000, SPEAKERMODE.STEREO, 0));
                Check(system.setDSPBufferSize(512, 4));
                Check(system.init(32, INITFLAGS.NORMAL, IntPtr.Zero));
                initialized = true;
                Check(system.getMasterChannelGroup(out ChannelGroup master));
                Check(master.getDSP(CHANNELCONTROL_DSP_INDEX.FADER, out DSP meter));
                Check(meter.setMeteringEnabled(false, true));
                var wall = new AcousticWall(new AcousticBandValues(.05f, .08f, .12f));
                var room = new RectangularAcousticRoom(new AcousticVector3(-3,0,-4), new AcousticVector3(3,3,4), wall);
                var response = RectangularRoomAcoustics.Calculate(room, new AcousticVector3(1,1.5f,1),
                    new AcousticVector3(0,1.5f,-1), new AcousticVector3(1,0,0));
                var parameters = SpatialDspParameters.Create(response, 48000);
                var info = new CREATESOUNDEXINFO { cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(),
                    length = 4, numchannels = 1, defaultfrequency = 48000, format = SOUND_FORMAT.PCMFLOAT };
                Check(system.createSound(BitConverter.GetBytes(.5f), MODE.OPENMEMORY | MODE.OPENRAW | MODE._2D,
                    ref info, out sound));
                for (int repetition = 0; repetition < 2; repetition++)
                {
                    effect = new FmodRoomAcousticProcessor(system, master, parameters);
                    Check(system.playSound(sound, effect.Group, false, out _));
                    Step(system, 12);
                    Assert.That(OutputPeak(meter), Is.GreaterThan(1e-7f), "Stored tail must reach the master.");
                    effect.SetVolume(0);
                    Step(system, 8);
                    Assert.That(OutputPeak(meter), Is.LessThan(1e-9f), "Group fader must mute existing tail.");
                    effect.SetVolume(1);
                    Step(system, 8);
                    Assert.That(OutputPeak(meter), Is.GreaterThan(1e-8f), "Unmuting must recover surviving tail.");
                    effect.SetPaused(true);
                    Step(system, 4);
                    int pausedCount = effect.CallbackCount;
                    Step(system, 8);
                    Assert.That(effect.CallbackCount, Is.EqualTo(pausedCount));
                    Assert.That(OutputPeak(meter), Is.LessThan(1e-9f), "Paused group must produce no master output.");
                    effect.SetPaused(false);
                    Step(system, 4);
                    Assert.That(effect.CallbackCount, Is.GreaterThan(pausedCount));
                    Assert.That(effect.CallbackError, Is.EqualTo(RESULT.OK));
                    effect.Dispose();
                    effect.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => effect.SetVolume(1));
                    effect = null;
                }
            }
            finally
            {
                effect?.Dispose();
                if (sound.hasHandle()) Check(sound.release());
                if (initialized) Check(system.close());
                Check(system.release());
            }
        }

        static void Step(FMOD.System system, int blocks)
        { for (int i = 0; i < blocks; i++) Check(system.update()); }

        static float OutputPeak(DSP meter)
        {
            Check(meter.getMeteringInfo(IntPtr.Zero, out DSP_METERING_INFO output));
            float peak = 0;
            for (int i = 0; i < output.numchannels; i++) peak = Math.Max(peak, output.peaklevel[i]);
            return peak;
        }
    }
}
