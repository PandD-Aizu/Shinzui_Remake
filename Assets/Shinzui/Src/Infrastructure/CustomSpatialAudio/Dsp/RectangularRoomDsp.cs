using System;
using Shinzui.Application.CustomSpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio.Binaural;

namespace Shinzui.Infrastructure.CustomSpatialAudio.Dsp
{
    /// <summary>
    /// Phase A mono-to-stereo direct/first-reflection renderer and three-band diffuse tail.
    /// No Unity/FMOD calls, allocations, locks, I/O or geometry work occur while processing.
    /// Uses optional measured HRIRs for direct/reflected paths, or equal-power pan when absent.
    /// A single audio thread owns this object after construction.
    /// </summary>
    public sealed class RectangularRoomDsp
    {
        private const float MaximumInputAmplitude = 16f;
        private readonly float[] historyMono, historyLow, historyMid, historyHigh;
        private readonly ThreeBandFeedbackDelayNetwork late;
        private readonly float lowAlpha, highAlpha;
        private readonly int transitionSamples;
        private float lowState, highState;
        private int writePosition, transitionPosition;
        private SpatialDspParameters current, target, pending;

        public int SampleRate { get; }

        public RectangularRoomDsp(int sampleRate)
        {
            SpatialDspParameters.ValidateSampleRate(sampleRate);
            SampleRate = sampleRate;
            int historyLength = (int)(SpatialDspParameters.MaximumPathDelaySeconds * sampleRate) +
                HrirFilter.MaximumTapCount + 2;
            historyLow = new float[historyLength];
            historyMid = new float[historyLength];
            historyHigh = new float[historyLength];
            historyMono = new float[historyLength];
            late = new ThreeBandFeedbackDelayNetwork(sampleRate);
            lowAlpha = 1f - (float)Math.Exp(-2.0 * Math.PI * 250.0 / sampleRate);
            highAlpha = 1f - (float)Math.Exp(-2.0 * Math.PI * 4000.0 / sampleRate);
            transitionSamples = Math.Max(2, sampleRate / 50); // 20 ms; fixed taps, no delay glides.
        }

        /// <summary>
        /// Call on the audio thread before Process, using a reference atomically published by
        /// the control thread. Latest pending parameters win; an active crossfade completes
        /// before the next starts. Null/mismatched-rate configurations are rejected unchanged.
        /// </summary>
        public bool ApplyParameters(SpatialDspParameters parameters)
        {
            if (parameters == null || parameters.SampleRate != SampleRate) return false;
            if (ReferenceEquals(parameters, current) && target == null)
            {
                pending = null;
                return true;
            }
            if (ReferenceEquals(parameters, target))
            {
                pending = null;
                return true;
            }
            pending = parameters;
            return true;
        }

        /// <summary>
        /// Replaces frameCount stereo frames in the destination. A null mono buffer supplies
        /// silence and continues direct-delay history and the reverb tail. Offsets are sample
        /// offsets, not frame offsets. Separate input/output arrays are required. Invalid
        /// buffer arguments return false without touching output or DSP state.
        /// Non-finite input is muted and overloads above +/-16 are bounded before feedback.
        /// </summary>
        public bool Process(float[] mono, int inputOffset, float[] interleavedStereo,
            int outputOffset, int frameCount)
        {
            if (interleavedStereo == null || outputOffset < 0 || frameCount < 0 ||
                outputOffset > interleavedStereo.Length ||
                frameCount > (interleavedStereo.Length - outputOffset) / 2 || inputOffset < 0)
                return false;
            if (mono != null && (ReferenceEquals(mono, interleavedStereo) ||
                inputOffset > mono.Length || frameCount > mono.Length - inputOffset))
                return false;
            if (frameCount == 0) return true;

            BeginBlock();
            if (current == null)
            {
                Array.Clear(interleavedStereo, outputOffset, frameCount * 2);
                return true;
            }

            for (int frame = 0; frame < frameCount; frame++)
            {
                float input = mono == null ? 0f : Sanitize(mono[inputOffset + frame]);
                historyMono[writePosition] = input;
                lowState = Flush(lowState + lowAlpha * (input - lowState));
                highState = Flush(highState + highAlpha * (input - highState));
                // Complementary first-order split: these three samples sum back to input.
                historyLow[writePosition] = lowState;
                historyMid[writePosition] = highState - lowState;
                historyHigh[writePosition] = input - highState;

                float blend = target == null ? 0f : (float)transitionPosition / (transitionSamples - 1);
                SpatialDspParameters next = target ?? current;
                RenderPaths(current, out float leftA, out float rightA);
                float left = leftA, right = rightA;
                if (target != null)
                {
                    RenderPaths(target, out float leftB, out float rightB);
                    left += (leftB - leftA) * blend;
                    right += (rightB - rightA) * blend;
                }

                // Diffuse excitation cannot precede the earliest active reflection. This
                // remains a single-room return approximation without directional late energy.
                ReadBands(current.LateDelaySamples, out float lateLow, out float lateMid, out float lateHigh);
                lateLow *= current.LateInputGains.Low;
                lateMid *= current.LateInputGains.Mid;
                lateHigh *= current.LateInputGains.High;
                if (target != null)
                {
                    ReadBands(target.LateDelaySamples, out float newLow, out float newMid, out float newHigh);
                    lateLow += (newLow * target.LateInputGains.Low - lateLow) * blend;
                    lateMid += (newMid * target.LateInputGains.Mid - lateMid) * blend;
                    lateHigh += (newHigh * target.LateInputGains.High - lateHigh) * blend;
                }
                late.Process(lateLow, lateMid, lateHigh, current, next, blend, out float wetLeft, out float wetRight);
                float wetGain = current.LateOutputGain + (next.LateOutputGain - current.LateOutputGain) * blend;
                interleavedStereo[outputOffset++] = left + wetLeft * wetGain;
                interleavedStereo[outputOffset++] = right + wetRight * wetGain;

                if (++writePosition == historyLow.Length) writePosition = 0;
                if (target != null && ++transitionPosition >= transitionSamples)
                {
                    current = target;
                    target = null;
                    transitionPosition = 0;
                    // A queued change starts on the next sample, including when this call
                    // has more frames. Deferring to BeginBlock made latency depend on the
                    // host's callback/buffer size.
                    BeginBlock();
                }
            }
            return true;
        }

        /// <summary>Clears every delay/filter and retains the latest parameters. Audio must be suspended.</summary>
        public void Reset()
        {
            Array.Clear(historyLow, 0, historyLow.Length);
            Array.Clear(historyMid, 0, historyMid.Length);
            Array.Clear(historyHigh, 0, historyHigh.Length);
            Array.Clear(historyMono, 0, historyMono.Length);
            late.Reset();
            lowState = 0f;
            highState = 0f;
            writePosition = 0;
            transitionPosition = 0;
            current = pending ?? target ?? current;
            target = null;
            pending = null;
        }

        private void BeginBlock()
        {
            if (target != null || pending == null) return;
            if (current == null) current = pending;
            else if (!ReferenceEquals(current, pending))
            {
                target = pending;
                transitionPosition = 0;
            }
            pending = null;
        }

        private void RenderPaths(SpatialDspParameters parameters, out float left, out float right)
        {
            left = 0f;
            right = 0f;
            for (int i = 0; i < RoomAcousticResponse.PathCount; i++)
            {
                SpatialDspParameters.PathCoefficients path = parameters.GetPath(i);
                if ((path.LeftGain == 0f && path.RightGain == 0f) ||
                    (path.Amplitude.Low == 0f && path.Amplitude.Mid == 0f && path.Amplitude.High == 0f))
                    continue;
                if (path.LeftKernel != null)
                {
                    // FIR reads the existing delayed dry history, so changing filters needs
                    // no allocation or history reset. The outer fixed-tap crossfade also
                    // handles head turns and transitions into/out of binaural rendering.
                    float earLeft = 0f, earRight = 0f;
                    int newer = writePosition - path.WholeDelaySamples;
                    if (newer < 0) newer += historyLow.Length;
                    float[] leftKernel = path.LeftKernel, rightKernel = path.RightKernel;
                    if (path.UniformAmplitude)
                    {
                        // Direct sound has equal band amplitudes. Its gain and fractional
                        // travel delay are already baked into the immutable ear kernels.
                        for (int tap = 0; tap < leftKernel.Length; tap++)
                        {
                            float value = historyMono[newer];
                            earLeft += value * leftKernel[tap];
                            earRight += value * rightKernel[tap];
                            if (--newer < 0) newer = historyMono.Length - 1;
                        }
                    }
                    else
                    {
                        for (int tap = 0; tap < leftKernel.Length; tap++)
                        {
                            float value = historyLow[newer] * path.Amplitude.Low +
                                historyMid[newer] * path.Amplitude.Mid + historyHigh[newer] * path.Amplitude.High;
                            earLeft += value * leftKernel[tap];
                            earRight += value * rightKernel[tap];
                            if (--newer < 0) newer = historyLow.Length - 1;
                        }
                    }
                    left += earLeft;
                    right += earRight;
                    continue;
                }
                ReadBands(path.DelaySamples, out float low, out float mid, out float high);
                float sample = low * path.Amplitude.Low + mid * path.Amplitude.Mid + high * path.Amplitude.High;
                left += sample * path.LeftGain;
                right += sample * path.RightGain;
            }
        }

        private void ReadBands(float delaySamples, out float low, out float mid, out float high)
        {
            int wholeDelay = (int)delaySamples;
            float fraction = delaySamples - wholeDelay;
            int newer = writePosition - wholeDelay;
            if (newer < 0) newer += historyLow.Length;
            int older = newer == 0 ? historyLow.Length - 1 : newer - 1;
            low = historyLow[newer] + (historyLow[older] - historyLow[newer]) * fraction;
            mid = historyMid[newer] + (historyMid[older] - historyMid[newer]) * fraction;
            high = historyHigh[newer] + (historyHigh[older] - historyHigh[newer]) * fraction;
        }

        private static float Sanitize(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            if (value > MaximumInputAmplitude) return MaximumInputAmplitude;
            if (value < -MaximumInputAmplitude) return -MaximumInputAmplitude;
            return value;
        }

        private static float Flush(float value) => value > -1e-20f && value < 1e-20f ? 0f : value;
    }
}
