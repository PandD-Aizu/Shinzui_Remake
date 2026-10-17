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
using VContainer;

/// <summary>Opt-in Windows display transaction smoke using a separate settings file, never the user's preferences</summary>
public sealed class GraphicsDisplayValidation : MonoBehaviour
{
    /// <summary>Start only in a development player explicitly requested for display validation</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Debug.isDebugBuild || !(Environment.GetCommandLineArgs().Contains("--graphics-display-validation") || Environment.GetCommandLineArgs().Contains("--graphics-menu-validation"))) return;
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
        if (args.Contains("--graphics-menu-validation")) { yield return RunMenu(folder); yield break; }
        var repository = new FileSettingsRepository(Path.Combine(folder, "display-test-settings.json"));
        bool restart = args.Contains("--graphics-display-restart");
        var displayModes = new List<object>();
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
                checks["renderScaleLoadedInNewProcess"] = settings.GetGraphicsDraft().ImageQualityScale == .75f;
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
                foreach (int mode in new[] { 1, 2, 0 })
                {
                    string resolution = mode == 1 ? "1600x900" : "1920x1080";
                    settings.EditGraphics(g => { g.ScreenMode = mode; g.Resolution = resolution; }, false);
                    settings.SaveAndApply();
                    yield return new WaitForSecondsRealtime(2);
                    displayModes.Add(new { requestedMode = mode, requestedResolution = resolution, actualMode = Screen.fullScreenMode.ToString(), Screen.width, Screen.height });
                    checks[$"mode{mode}Pending"] = settings.AwaitingDisplayConfirmation;
                    checks[$"mode{mode}Native"] = mode == 1 ? Screen.width == 1600 && Screen.height == 900 && Screen.fullScreenMode == FullScreenMode.Windowed
                        : Screen.width > 0 && Screen.height > 0 && (Screen.fullScreenMode == FullScreenMode.FullScreenWindow || mode == 0 && Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen);
                    settings.RejectDisplay();
                    yield return new WaitForSecondsRealtime(2);
                    checks[$"mode{mode}Rollback"] = Screen.width == 1920 && Screen.height == 1080 && Screen.fullScreenMode == FullScreenMode.Windowed;
                }

                settings.EditGraphics(g => g.Resolution = "1280x720", false);
                settings.SaveAndApply();
                yield return new WaitForSecondsRealtime(2);
                settings.ConfirmDisplay();
                checks["confirmedSaved"] = !settings.AwaitingDisplayConfirmation && repository.Load().Graphics.Resolution == "1280x720";
                settings.BeginEdit();
                settings.EditGraphics(g => g.ImageQualityScale = .75f);
                settings.SaveAndApply();
                checks["renderScaleSavedWithoutDisplayConfirmation"] = !settings.AwaitingDisplayConfirmation && repository.Load().Graphics.ImageQualityScale == .75f;
                settings.BeginEdit();
                settings.ResetToDefault();
                checks["defaultsRestoreNativeScaleInDraft"] = settings.GetGraphicsDraft().ImageQualityScale == 1f;
                checks["defaultsDoNotOverwriteSavedScale"] = repository.Load().Graphics.ImageQualityScale == .75f;
                settings.CancelEdit();
                checks["cancelDefaultsRestoresSavedScale"] = settings.GetGraphicsDraft().ImageQualityScale == .75f;
            }
        }
        string name = restart ? "player-display-restart.json" : "player-display-transactions.json";
        File.WriteAllText(Path.Combine(folder, name), JsonConvert.SerializeObject(new
        {
            passed = checks.Values.All(value => value), editor = UnityEngine.Application.isEditor,
            api = SystemInfo.graphicsDeviceType.ToString(), Screen.width, Screen.height,
            mode = Screen.fullScreenMode.ToString(), isolatedSettingsFile = true, displayModes, checks
        }, Formatting.Indented));
        UnityEngine.Application.Quit(checks.Values.All(value => value) ? 0 : 1);
    }
    /// <summary>Exercise real title buttons repeatedly while redirecting only this diagnostic instance to an isolated file</summary>
    private IEnumerator RunMenu(string folder)
    {
        yield return new WaitForSecondsRealtime(2);
        var scope = FindAnyObjectByType<Shinzui.DI.TitleLifetimeScope>();
        var title = FindAnyObjectByType<Shinzui.Src.Title.ButtonController>();
        var settings = scope.Container.Resolve<SettingsUseCase>();
        string userPath = Path.Combine(UnityEngine.Application.persistentDataPath, "settings.json");
        string userBefore = File.Exists(userPath) ? File.ReadAllText(userPath) : null;
        var repository = new FileSettingsRepository(Path.Combine(folder, "menu-test-settings.json"));
        var baseline = new GameSettings(); baseline.ResetToDefault();
        baseline.Graphics.ScreenMode = 1; baseline.Graphics.Resolution = "1280x720";
        baseline.Graphics.EnableVSync = false; baseline.Graphics.FrameRateLimit = 60;
        repository.Save(baseline);
        var field = typeof(SettingsUseCase).GetField("_repository", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field.SetValue(settings, repository);
        if (!ReferenceEquals(field.GetValue(settings), repository)) throw new InvalidOperationException("Menu test repository was not isolated");
        settings.CancelEdit(); settings.Initialize();
        yield return new WaitForSecondsRealtime(2);
        var checks = new Dictionary<string, bool>();
        var observations = new List<object>();
        int initialButtons = -1;
        for (int cycle = 0; cycle < 12; cycle++)
        {
            title.OpenOptions(); title.AlignGraphicOption();
            yield return null;
            string confirmed = JsonConvert.SerializeObject(settings.GetGraphicsDraft());
            var buttons = title.OptionPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            if (initialButtons < 0) initialButtons = buttons.Length;
            checks[$"open{cycle}"] = settings.IsEditing && buttons.Length == initialButtons;
            var scale = buttons.First(b => b.name == ">" && b.transform.parent.parent.name == "RenderScale");
            var portal = buttons.First(b => b.name == ">" && b.transform.parent.parent.name == "Portal");
            portal.Select(); Canvas.ForceUpdateCanvases();
            var scroll = portal.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, (RectTransform)portal.transform);
            checks[$"scroll{cycle}"] = bounds.min.y >= scroll.viewport.rect.yMin - 1 && bounds.max.y <= scroll.viewport.rect.yMax + 1;
            Submit(scale);
            checks[$"edit{cycle}"] = JsonConvert.SerializeObject(settings.GetGraphicsDraft()) != confirmed;
            Submit(buttons.First(b => b.name == "Defaults" && b.transform.parent.name == "SettingsActions"));
            Submit(buttons.First(b => b.name == "Cancel" && b.transform.parent.name == "SettingsActions"));
            checks[$"cancel{cycle}"] = JsonConvert.SerializeObject(settings.GetGraphicsDraft()) == confirmed;
            settings.EditGraphics(g => g.SetQualityPreset((GraphicsQualityPreset)(cycle % 4)), false);
            Submit(scale);
            Submit(buttons.First(b => b.name == ">" && b.transform.parent.parent.name == "AO"));
            Submit(buttons.First(b => b.name == ">" && b.transform.parent.parent.name == "AA"));
            string expected = JsonConvert.SerializeObject(settings.GetGraphicsDraft());
            Submit(buttons.First(b => b.name == "Apply" && b.transform.parent.name == "SettingsActions"));
            for (int frame = 0; frame < 45; frame++) yield return null;
            checks[$"save{cycle}"] = !settings.AwaitingDisplayConfirmation && JsonConvert.SerializeObject(repository.Load().Graphics) == expected;
            observations.Add(new { cycle, settings = repository.Load().Graphics, Screen.width, Screen.height, selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject?.name });
            if (cycle == 0 || cycle == 11)
            {
                portal.Select(); yield return new WaitForEndOfFrame();
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(folder, $"menu-cycle-{cycle}.png"), image.EncodeToPNG()); Destroy(image);
            }
            if (cycle == 11)
            {
                buttons.First(b => b.name == "Apply" && b.transform.parent.name == "SettingsActions").Select();
                scroll.verticalNormalizedPosition = 1;
                UnityEngine.Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(folder, "menu-runtime-status.png"), image.EncodeToPNG()); Destroy(image);
            }
            title.CloseOptions(); yield return null;
            checks[$"close{cycle}"] = !title.OptionPanel.activeSelf && !settings.IsEditing;
        }
        checks["userSettingsUnchanged"] = userBefore == (File.Exists(userPath) ? File.ReadAllText(userPath) : null);
        File.WriteAllText(Path.Combine(folder, "player-menu-regression.json"), JsonConvert.SerializeObject(new
        { passed = checks.Values.All(v => v), isolatedSettingsFile = true, input = "Production EventSystem submit handlers; not physical device automation", checks, observations }, Formatting.Indented));
        UnityEngine.Application.Quit(checks.Values.All(v => v) ? 0 : 1);
    }

    /// <summary>Dispatch the same submit event used by keyboard and controller UI navigation</summary>
    private static void Submit(UnityEngine.UI.Button button)
    {
        button.Select();
        UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,
            new UnityEngine.EventSystems.BaseEventData(UnityEngine.EventSystems.EventSystem.current), UnityEngine.EventSystems.ExecuteEvents.submitHandler);
    }

}
