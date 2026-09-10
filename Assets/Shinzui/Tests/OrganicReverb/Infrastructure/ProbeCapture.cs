using System;
using System.IO;
using System.Runtime.InteropServices;
using FMOD;

namespace Shinzui.AudioProbe.Infrastructure
{
    // A bounded, pass-through capture at the FMOD master output. No file I/O on the mixer thread.
    public sealed class ProbeCapture : IDisposable
    {
        readonly object gate = new object();
        readonly float[] scratch = new float[32768];
        readonly float[] samples;
        static readonly DSP_READ_CALLBACK callback = Read;
        static ProbeCapture active;
        ChannelGroup master;
        DSP tap;
        int count;
        int channels = 2;
        bool recording;
        public bool Overflowed { get; private set; }
        public int SampleRate { get; }

        public ProbeCapture(FMOD.System system)
        {
            Check(system.getSoftwareFormat(out int rate, out _, out _));
            SampleRate = rate;
            samples = new float[rate * 8 * 8];
            if (active != null) throw new InvalidOperationException("Only one probe capture can run.");
            active = this;
            var desc = new DSP_DESCRIPTION {
                name = new byte[32], version = 0x10000,
                numinputbuffers = 1, numoutputbuffers = 1, read = callback
            };
            System.Text.Encoding.ASCII.GetBytes("Organic Reverb Capture").CopyTo(desc.name, 0);
            Check(system.getMasterChannelGroup(out master));
            Check(system.createDSP(ref desc, out tap));
            Check(tap.setChannelFormat(0, 2, SPEAKERMODE.STEREO));
            Check(master.addDSP(CHANNELCONTROL_DSP_INDEX.HEAD, tap));
        }

        public void Begin()
        {
            lock (gate) { count = 0; Overflowed = false; recording = true; }
        }

        [AOT.MonoPInvokeCallback(typeof(DSP_READ_CALLBACK))]
        static RESULT Read(ref DSP_STATE state, IntPtr input, IntPtr output, uint length, int inChannels, ref int outChannels)
        {
            return active == null ? RESULT.ERR_DSP_DONTPROCESS : active.Copy(input, output, length, inChannels, outChannels);
        }

        RESULT Copy(IntPtr input, IntPtr output, uint length, int inChannels, int outChannels)
        {
            int size = checked((int)length * inChannels);
            if (size > scratch.Length || inChannels != outChannels) return RESULT.ERR_DSP_FORMAT;
            Marshal.Copy(input, scratch, 0, size);
            Marshal.Copy(scratch, 0, output, size);
            lock (gate)
            {
                if (recording)
                {
                    channels = inChannels;
                    int available = Math.Min(size, samples.Length - count);
                    Array.Copy(scratch, 0, samples, count, available);
                    count += available;
                    Overflowed |= available != size;
                }
            }
            return RESULT.OK;
        }

        public float Finish(string path)
        {
            lock (gate) { recording = false; }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            float peak = 0;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)channels);
                writer.Write(SampleRate); writer.Write(SampleRate * channels * 2);
                writer.Write((short)(channels * 2)); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    peak = Math.Max(peak, Math.Abs(samples[i]));
                    writer.Write((short)(Math.Max(-1f, Math.Min(1f, samples[i])) * 32767));
                }
            }
            return peak;
        }

        public void Dispose()
        {
            lock (gate) { recording = false; }
            if (tap.hasHandle()) { master.removeDSP(tap); tap.release(); tap.clearHandle(); }
            active = null;
            GC.KeepAlive(callback);
        }

        public static void Check(RESULT result)
        {
            if (result != RESULT.OK) throw new InvalidOperationException("FMOD: " + result);
        }
    }
}
