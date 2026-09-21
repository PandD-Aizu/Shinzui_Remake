using System;
using System.IO;
using UnityEngine;

namespace Shinzui.CustomSpatialAudio.Probe
{
    /// <summary>Composition root for the isolated player experiment, never added to gameplay scenes.</summary>
    public sealed class CustomSpatialAudioProbeEntryPoint : MonoBehaviour
    {
        void Start()
        {
            int exit = 0;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                string output = Path.Combine(UnityEngine.Application.persistentDataPath, "CustomSpatialAudioProbe");
                for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-customAudioOutput") output = args[i + 1];
                CustomSpatialAudioProbe.Run(output, false);
                if (Array.IndexOf(args, "-customAudioRealtime") >= 0) CustomSpatialAudioProbe.Run(output, true);
            }
            catch (Exception error) { Debug.LogException(error); exit = 1; }
            if (!UnityEngine.Application.isEditor) UnityEngine.Application.Quit(exit);
        }
    }
}
