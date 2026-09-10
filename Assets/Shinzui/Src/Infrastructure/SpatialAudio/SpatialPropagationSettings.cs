using System;
using SteamAudio;
using UnityEngine;

namespace Shinzui.Infrastructure.SpatialAudio
{
    /// <summary>Scene-scoped acoustic tuning. Null in runtime options preserves authored event settings.</summary>
    [Serializable]
    public sealed class SpatialPropagationSettings
    {
        public bool VolumetricOcclusion = true;
        [Range(.01f, 4)] public float SourceRadius = .35f;
        [Range(1, 128)] public int OcclusionSamples = 32;
        public bool Transmission = true;
        [Range(1, 8)] public int MaxTransmissionSurfaces = 4;
        public bool Reflections = true;
        [Range(0, 1)] public float DirectGain = 1;
        [Range(0, 1)] public float ReflectionGain = .35f;

        internal SpatialPropagationSettings Snapshot()
        {
            if (!Finite(SourceRadius) || SourceRadius < .01f || SourceRadius > 4 ||
                OcclusionSamples < 1 || OcclusionSamples > 128 || MaxTransmissionSurfaces < 1 || MaxTransmissionSurfaces > 8 ||
                !Finite(DirectGain) || DirectGain < 0 || DirectGain > 1 ||
                !Finite(ReflectionGain) || ReflectionGain < 0 || ReflectionGain > 1)
                throw new ArgumentException("Invalid spatial propagation settings.");
            return (SpatialPropagationSettings)MemberwiseClone();
        }

        internal void Configure(SteamAudioSource source)
        {
            source.occlusionType = VolumetricOcclusion ? OcclusionType.Volumetric : OcclusionType.Raycast;
            source.occlusionRadius = SourceRadius;
            source.occlusionSamples = OcclusionSamples;
            source.transmission = Transmission;
            source.maxTransmissionSurfaces = MaxTransmissionSurfaces;
            source.reflections = Reflections;
        }

        internal void Configure(FMOD.DSP dsp)
        {
            Check(dsp.getNumParameters(out int count));
            int configured = 0;
            for (int i = 0; i < count; i++)
            {
                Check(dsp.getParameterInfo(i, out var info));
                string name = System.Text.Encoding.UTF8.GetString(info.name).TrimEnd('\0');
                switch (name)
                {
                    case "Interpolation": Check(dsp.setParameterInt(i, 1)); break; // Bilinear HRTF interpolation.
                    case "ApplyDA": Check(dsp.setParameterInt(i, 2)); break; // One inverse-distance law in this DSP.
                    case "ApplyOccl": Check(dsp.setParameterInt(i, 1)); break;
                    case "ApplyTrans": Check(dsp.setParameterInt(i, Transmission ? 1 : 0)); break;
                    case "TransType": Check(dsp.setParameterInt(i, 1)); break; // Three-band transmission EQ.
                    case "ApplyRefl": Check(dsp.setParameterBool(i, Reflections)); break;
                    case "ApplyPath": Check(dsp.setParameterBool(i, false)); break; // No duplicate indirect path.
                    case "DirMixLevel": Check(dsp.setParameterFloat(i, DirectGain)); break;
                    case "ReflMixLevel": Check(dsp.setParameterFloat(i, ReflectionGain)); break;
                    case "ReflBinaural": Check(dsp.setParameterBool(i, true)); break;
                    default: continue;
                }
                configured++;
            }
            if (configured != 10) throw new InvalidOperationException("Steam Audio DSP tuning parameters are missing.");
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void Check(FMOD.RESULT result)
        { if (result != FMOD.RESULT.OK) throw new InvalidOperationException("FMOD propagation: " + result); }
    }
}
