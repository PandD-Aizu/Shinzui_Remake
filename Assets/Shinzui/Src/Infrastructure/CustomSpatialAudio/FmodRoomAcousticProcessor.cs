using System;
using System.Runtime.InteropServices;
using System.Threading;
using FMOD;
using Shinzui.Infrastructure.CustomSpatialAudio.Dsp;

namespace Shinzui.Infrastructure.CustomSpatialAudio
{
    /// <summary>
    /// Single-emitter processor: attach to a dedicated Core channel group containing dry mono sounds.
    /// The group owns the effect, so stopping a short sound does not cut its reflected/reverb tail.
    /// No Studio event, RuntimeManager, Steam Audio, or global mixer/settings mutation is involved.
    /// All public methods are main-thread only. Pause the group to pause the complete acoustic response.
    /// </summary>
    public sealed class FmodRoomAcousticProcessor : IDisposable
    {
        const int MaximumChannels = 8;
        const int MaximumFrames = 8192;
        // FMOD 2.03.14 plug-in API; this constant is not exposed by its C# wrapper.
        const uint PluginSdkVersion = 110;
        static readonly DSP_CREATE_CALLBACK CreateCallback = OnCreate;
        static readonly DSP_PROCESS_CALLBACK ProcessCallback = OnProcess;

        readonly FMOD.System system;
        readonly RectangularRoomDsp processor;
        readonly float[] input = new float[MaximumFrames * MaximumChannels];
        readonly float[] mono = new float[MaximumFrames];
        readonly float[] stereo = new float[MaximumFrames * 2];
        readonly float[] capture;
        DSP_DESCRIPTION description; // Root delegates and descriptor storage throughout native lifetime.
        GCHandle self;
        ChannelGroup group;
        DSP effect;
        SpatialDspParameters pendingParameters;
        int captureCount;
        int callbackCount;
        int callbackError;
        int captureOverflowed;
        bool capturing;
        bool effectAttached;
        bool disposed;

        public int SampleRate { get; }
        public int CallbackCount => Volatile.Read(ref callbackCount);
        public RESULT CallbackError => (RESULT)Volatile.Read(ref callbackError);
        public bool CaptureOverflowed => Volatile.Read(ref captureOverflowed) != 0;
        public ChannelGroup Group => group;

        public FmodRoomAcousticProcessor(FMOD.System system, ChannelGroup parent,
            SpatialDspParameters parameters, int captureSeconds = 0)
        {
            this.system = system;
            Check(system.getSoftwareFormat(out int sampleRate, out _, out _));
            SampleRate = sampleRate;
            if (captureSeconds < 0 || captureSeconds > 30) throw new ArgumentOutOfRangeException(nameof(captureSeconds));
            processor = new RectangularRoomDsp(sampleRate);
            if (!processor.ApplyParameters(parameters)) throw new ArgumentException("Invalid DSP parameters.", nameof(parameters));
            capture = new float[checked(sampleRate * captureSeconds * 2)];
            description = new DSP_DESCRIPTION
            {
                pluginsdkversion = PluginSdkVersion, name = new byte[32], version = 0x00010000,
                numinputbuffers = 1, numoutputbuffers = 1,
                create = CreateCallback, process = ProcessCallback
            };
            System.Text.Encoding.ASCII.GetBytes("Shinzui Rectangular Room").CopyTo(description.name, 0);
            try
            {
                self = GCHandle.Alloc(this);
                description.userdata = GCHandle.ToIntPtr(self);
                Check(system.createChannelGroup("Shinzui custom spatial voice", out group));
                Check(parent.addGroup(group));
                Check(system.createDSP(ref description, out effect));
                // FMOD 2.03 ignores the deprecated channel mask; channel count and
                // speaker mode carry the format without a warning for every emitter.
                Check(effect.setChannelFormat(0, 2, SPEAKERMODE.STEREO));
                // Input -> acoustic response -> group fader -> parent. The group fader
                // must attenuate stored tails as well as newly arriving dry samples.
                Check(group.addDSP(CHANNELCONTROL_DSP_INDEX.TAIL, effect));
                effectAttached = true;
            }
            catch
            {
                // Preserve the construction error, while releasing every resource acquired
                // so far. A failed native DSP release retains its callback GCHandle.
                try { ReleaseResources(); } catch { }
                throw;
            }
        }

        public void SetParameters(SpatialDspParameters parameters)
        {
            ThrowIfDisposed();
            // Immutable coefficients are published atomically; the audio thread applies them at a block boundary.
            if (parameters == null || parameters.SampleRate != SampleRate)
                throw new ArgumentException("Invalid DSP parameters.", nameof(parameters));
            Volatile.Write(ref pendingParameters, parameters);
        }

        public void SetPaused(bool paused) { ThrowIfDisposed(); Check(group.setPaused(paused)); }

        /// <summary>Recycle a voice without reallocating delay lines. Leaves the empty group paused.</summary>
        public void Reset(SpatialDspParameters parameters)
        {
            ThrowIfDisposed();
            if (parameters == null || parameters.SampleRate != SampleRate)
                throw new ArgumentException("Invalid DSP parameters.", nameof(parameters));
            Check(system.lockDSP());
            try
            {
                Check(group.setPaused(true));
                Check(group.stop());
                Interlocked.Exchange(ref pendingParameters, null);
                processor.ApplyParameters(parameters);
                processor.Reset(); // Commit the new parameters without crossfading from the previous voice.
            }
            finally { Check(system.unlockDSP()); }
        }

        public void SetVolume(float volume)
        {
            ThrowIfDisposed();
            if (float.IsNaN(volume) || volume < 0 || volume > 1) throw new ArgumentOutOfRangeException(nameof(volume));
            Check(group.setVolume(volume));
        }

        public void BeginCapture()
        {
            ThrowIfDisposed();
            if (capture.Length == 0) throw new InvalidOperationException("Capture was not configured.");
            Check(system.lockDSP());
            try { captureCount = 0; captureOverflowed = 0; capturing = true; }
            finally { Check(system.unlockDSP()); }
        }

        /// <summary>
        /// Copies a bounded pre-fader recording of the acoustic response. Group/Master volume
        /// changes are audible but are not reflected here. Allocation/copy occurs outside the mixer fence.
        /// </summary>
        public float[] FinishCapture()
        {
            ThrowIfDisposed();
            int count;
            Check(system.lockDSP());
            try
            {
                capturing = false;
                count = captureCount;
            }
            finally { Check(system.unlockDSP()); }
            // No writer remains, and control-thread calls are serialized by the caller.
            var result = new float[count];
            Array.Copy(capture, result, count);
            return result;
        }

        [AOT.MonoPInvokeCallback(typeof(DSP_CREATE_CALLBACK))]
        static RESULT OnCreate(ref DSP_STATE state)
        {
            // Called during Core createDSP, never during sample processing. Marshal delegates only here.
            try
            {
                RESULT result = state.functions.getuserdata(ref state, out IntPtr userData);
                if (result != RESULT.OK) return result;
                if (userData == IntPtr.Zero) return RESULT.ERR_INVALID_PARAM;
                state.plugindata = userData;
                return RESULT.OK;
            }
            catch { return RESULT.ERR_INTERNAL; } // Never unwind a managed exception through FMOD.
        }

        [AOT.MonoPInvokeCallback(typeof(DSP_PROCESS_CALLBACK))]
        static RESULT OnProcess(ref DSP_STATE state, uint length, ref DSP_BUFFER_ARRAY inputBuffers,
            ref DSP_BUFFER_ARRAY outputBuffers, bool inputsIdle, DSP_PROCESS_OPERATION operation)
        {
            if (state.plugindata == IntPtr.Zero) return RESULT.ERR_DSP_SILENCE;
            var owner = (FmodRoomAcousticProcessor)GCHandle.FromIntPtr(state.plugindata).Target;
            try
            {
                if (operation == DSP_PROCESS_OPERATION.PROCESS_QUERY)
                {
                    if (outputBuffers.numbuffers != 1 || outputBuffers.buffernumchannels == IntPtr.Zero ||
                        inputBuffers.numchannels < 0 || inputBuffers.numchannels > MaximumChannels)
                    {
                        Volatile.Write(ref owner.callbackError, (int)RESULT.ERR_DSP_FORMAT);
                        return RESULT.ERR_DSP_FORMAT;
                    }
                    // Negotiate stereo BEFORE FMOD allocates output. Setting outchannels in a
                    // READ callback is too late when an empty/mono group previously had one channel.
                    outputBuffers.numchannels = 2;
                    outputBuffers.speakermode = SPEAKERMODE.STEREO;
                    if (outputBuffers.bufferchannelmask != IntPtr.Zero)
                        Marshal.WriteInt32(outputBuffers.bufferchannelmask, (int)CHANNELMASK.STEREO);
                    // This single room processor intentionally stays active for its owner's
                    // lifetime, including empty input groups and the complete delayed tail.
                    return RESULT.OK;
                }
                if (operation == DSP_PROCESS_OPERATION.PROCESS_PERFORM)
                {
                    int inChannels = inputBuffers.numchannels;
                    if (length > int.MaxValue / (MaximumChannels * sizeof(float)) ||
                        inChannels < 0 || inChannels > MaximumChannels || outputBuffers.numchannels != 2 ||
                        outputBuffers.buffer == IntPtr.Zero)
                    {
                        Volatile.Write(ref owner.callbackError, (int)RESULT.ERR_DSP_FORMAT);
                        owner.ClearOutput(ref outputBuffers, length);
                        return RESULT.OK;
                    }
                    SpatialDspParameters parameters = Interlocked.Exchange(ref owner.pendingParameters, null);
                    if (parameters != null) owner.processor.ApplyParameters(parameters);
                    owner.Render(inputBuffers.buffer, outputBuffers.buffer, (int)length, inChannels, inputsIdle);
                    Interlocked.Increment(ref owner.callbackCount);
                }
                return RESULT.OK;
            }
            catch
            {
                // Fixed buffers and validated parameters make this an exceptional boundary,
                // not a normal code path. Keep exception/log formatting off the mixer thread.
                Volatile.Write(ref owner.callbackError, (int)RESULT.ERR_INTERNAL);
                if (operation == DSP_PROCESS_OPERATION.PROCESS_PERFORM)
                {
                    owner.ClearOutput(ref outputBuffers, length);
                    return RESULT.OK;
                }
                return RESULT.ERR_INTERNAL;
            }
        }

        void Render(IntPtr source, IntPtr destination, int frames, int inChannels, bool inputsIdle)
        {
            // Bounded scratch space also supports a device whose block exceeds MaximumFrames.
            for (int offset = 0; offset < frames;)
            {
                int blockFrames = Math.Min(MaximumFrames, frames - offset);
                if (!inputsIdle && inChannels > 0 && source != IntPtr.Zero)
                {
                    Marshal.Copy(IntPtr.Add(source, offset * inChannels * sizeof(float)), input, 0, blockFrames * inChannels);
                    for (int i = 0; i < blockFrames; i++)
                    {
                        float value = 0;
                        for (int channel = 0; channel < inChannels; channel++) value += input[i * inChannels + channel];
                        mono[i] = value / inChannels;
                    }
                }
                else Array.Clear(mono, 0, blockFrames);
                if (!processor.Process(mono, 0, stereo, 0, blockFrames))
                {
                    Array.Clear(stereo, 0, blockFrames * 2);
                    Volatile.Write(ref callbackError, (int)RESULT.ERR_DSP_FORMAT);
                }
                Marshal.Copy(stereo, 0, IntPtr.Add(destination, offset * 2 * sizeof(float)), blockFrames * 2);
                if (capturing)
                {
                    int count = Math.Min(blockFrames * 2, capture.Length - captureCount);
                    Array.Copy(stereo, 0, capture, captureCount, count);
                    captureCount += count;
                    if (count != blockFrames * 2) Volatile.Write(ref captureOverflowed, 1);
                }
                offset += blockFrames;
            }
        }

        void ClearOutput(ref DSP_BUFFER_ARRAY outputBuffers, uint frames)
        {
            int channels = outputBuffers.numchannels;
            IntPtr destination = outputBuffers.buffer;
            if (destination == IntPtr.Zero || channels <= 0 || channels > MaximumChannels ||
                frames > int.MaxValue / (MaximumChannels * sizeof(float))) return;
            int totalSamples = (int)frames * channels;
            Array.Clear(input, 0, Math.Min(totalSamples, input.Length));
            for (int offset = 0; offset < totalSamples;)
            {
                int count = Math.Min(input.Length, totalSamples - offset);
                Marshal.Copy(input, 0, IntPtr.Add(destination, offset * sizeof(float)), count);
                offset += count;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            Check(ReleaseResources());
            GC.KeepAlive(description);
        }

        RESULT ReleaseResources()
        {
            if (disposed) return RESULT.OK;
            if (!effect.hasHandle() && !group.hasHandle())
            {
                if (self.IsAllocated) self.Free();
                disposed = true;
                return RESULT.OK;
            }
            // The native graph is fenced before any callback-owned state can be freed.
            RESULT firstError = system.lockDSP();
            if (firstError != RESULT.OK) return firstError;
            try
            {
                capturing = false;
                if (group.hasHandle()) RememberError(ref firstError, group.stop());
                if (effect.hasHandle())
                {
                    if (effectAttached)
                    {
                        RESULT removed = group.removeDSP(effect);
                        RememberError(ref firstError, removed);
                        if (removed == RESULT.OK) effectAttached = false;
                    }
                    RESULT released = effect.release();
                    RememberError(ref firstError, released);
                    if (released == RESULT.OK) { effect.clearHandle(); effectAttached = false; }
                }
                // Retain the callback owner and native handles on failure so the caller can
                // retry Dispose. Freeing the GCHandle while the DSP survives causes use-after-free.
                if (!effect.hasHandle())
                {
                    if (self.IsAllocated) self.Free();
                    if (group.hasHandle())
                    {
                        RESULT released = group.release();
                        RememberError(ref firstError, released);
                        if (released == RESULT.OK) group.clearHandle();
                    }
                }
                disposed = !effect.hasHandle() && !group.hasHandle();
            }
            finally { RememberError(ref firstError, system.unlockDSP()); }
            return firstError;
        }

        static void RememberError(ref RESULT firstError, RESULT result)
        { if (firstError == RESULT.OK && result != RESULT.OK) firstError = result; }

        void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(FmodRoomAcousticProcessor)); }
        public static void Check(RESULT result)
        {
            if (result != RESULT.OK) throw new InvalidOperationException("FMOD: " + result);
        }
    }
}
