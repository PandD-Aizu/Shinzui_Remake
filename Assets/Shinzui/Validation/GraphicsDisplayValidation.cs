using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Shinzui.Application.UseCases;
using Shinzui.Domain.Settings;
using Shinzui.Infrastructure.Repositories;
using Shinzui.Infrastructure.Services;
using UnityEngine;

/// <summary>Opt-in Windows display transaction smoke using a separate settings file, never the user's preferences</summary>
public sealed class GraphicsDisplayValidation : MonoBehaviour
{
    /// <summary>Start only in a development player explicitly requested for display validation</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Debug.isDebugBuild || !Environment.GetCommandLineArgs().Contains("--graphics-display-validation")) return;
        var host = new GameObject("Display transaction validation");
        DontDestroyOnLoad(host);
        host.AddComponent<GraphicsDisplayValidation>();
    }

    /// <summary>Check real window resizing, timeout rollback and confirmed settings across separate process launches</summary>
    private IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--graphics-output");
        string folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(UnityEngine.Application.dataPath, "../GraphicsEvidence");
        Directory.CreateDirectory(folder);
        var repository = new FileSettingsRepository(Path.Combine(folder, "display-test-settings.json"));
        bool restart = args.Contains("--graphics-display-restart");
        var checks = new Dictionary<string, bool>();
        yield return new WaitForSecondsRealtime(2);
        if (!restart)
        {
            var baseline = new GameSettings();
            baseline.Graphics.ScreenMode = 1;
            baseline.Graphics.EnableVSync = false;
            repository.Save(baseline);
        }
        using (var settings = new SettingsUseCase(repository, new UnitySettingsApplier()))
        {
            yield return new WaitForSecondsRealtime(2);
            if (restart)
            {
                checks["confirmedResolutionLoadedInNewProcess"] = settings.GetGraphicsDraft().Resolution == "1280x720";
                checks["nativeWindowMatchesSavedResolution"] = Screen.width == 1280 && Screen.height == 720 && Screen.fullScreenMode == FullScreenMode.Windowed;
            }
            else
            {
                checks["baselineNativeWindow"] = Screen.width == 1920 && Screen.height == 1080;
                settings.BeginEdit();
                settings.EditGraphics(g => g.Resolution = "1280x720", false);
                yield return new WaitForSecondsRealtime(1);
                checks["draftDoesNotResize"] = Screen.width == 1920 && Screen.height == 1080;
                settings.CancelEdit();
                settings.BeginEdit();
                checks["cancelRestoresDraft"] = settings.GetGraphicsDraft().Resolution == "1920x1080";
                settings.EditGraphics(g => g.Resolution = "1280x720", false);
                settings.SaveAndApply();
                yield return new WaitForSecondsRealtime(2);
                checks["candidateNativeWindow"] = Screen.width == 1280 && Screen.height == 720;
                checks["candidateNotSaved"] = settings.AwaitingDisplayConfirmation && repository.Load().Graphics.Resolution == "1920x1080";
                settings.TickDisplayConfirmation(15.1f, true);
                yield return new WaitForSecondsRealtime(2);
                checks["timeoutRestoresNativeWindow"] = !settings.AwaitingDisplayConfirmation && Screen.width == 1920 && Screen.height == 1080;
                settings.EditGraphics(g => g.Resolution = "1280x720", false);
                settings.SaveAndApply();
                yield return new WaitForSecondsRealtime(2);
                settings.TickDisplayConfirmation(0, false);
                yield return new WaitForSecondsRealtime(2);
                checks["focusLossSignalRestoresNativeWindow"] = !settings.AwaitingDisplayConfirmation && Screen.width == 1920 && Screen.height == 1080;
                settings.EditGraphics(g => g.Resolution = "1280x720", false);
                settings.SaveAndApply();
                yield return new WaitForSecondsRealtime(2);
                settings.ConfirmDisplay();
                checks["confirmedSaved"] = !settings.AwaitingDisplayConfirmation && repository.Load().Graphics.Resolution == "1280x720";
            }
        }
        string name = restart ? "player-display-restart.json" : "player-display-transactions.json";
        File.WriteAllText(Path.Combine(folder, name), JsonConvert.SerializeObject(new
        {
            passed = checks.Values.All(value => value), editor = UnityEngine.Application.isEditor,
            api = SystemInfo.graphicsDeviceType.ToString(), Screen.width, Screen.height,
            mode = Screen.fullScreenMode.ToString(), isolatedSettingsFile = true, checks
        }, Formatting.Indented));
        UnityEngine.Application.Quit(checks.Values.All(value => value) ? 0 : 1);
    }
}
