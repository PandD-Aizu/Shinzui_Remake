using System;
using System.IO;
using Newtonsoft.Json;
using Shinzui.Application.UseCases;
using Shinzui.DI;
using Shinzui.DI.GenerateTunnel;
using Shinzui.Src.Title;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using VContainer;

/// <summary>Opt-in smoke check through the real title, addressable scene and procedural regeneration</summary>
[InitializeOnLoad]
public static class GraphicsSceneSmoke
{
    private static double _deadline;
    private static int _phase;
    private static string _originalFile;
    private static string _settingsPath;
    private static Camera _camera;

    /// <summary>Restore the pending smoke check after entering play mode</summary>
    static GraphicsSceneSmoke() => EditorApplication.update += Tick;

    /// <summary>Start from the authored Title scene</summary>
    public static void Start()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Shinzui/Scenes/Title.unity");
        SessionState.SetBool("Shinzui.SceneSmoke", true);
        EditorApplication.isPlaying = true;
    }

    /// <summary>Exercise user navigation without changing the user's persisted settings</summary>
    private static void Tick()
    {
        if (!SessionState.GetBool("Shinzui.SceneSmoke", false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (_deadline == 0) _deadline = EditorApplication.timeSinceStartup + 90;
            Require(EditorApplication.timeSinceStartup < _deadline, "Scene smoke timed out");
            if (_phase == 0)
            {
                var title = UnityEngine.Object.FindAnyObjectByType<ButtonController>();
                var scope = UnityEngine.Object.FindAnyObjectByType<TitleLifetimeScope>();
                if (title == null || scope?.Container == null) return;
                _settingsPath = Path.Combine(UnityEngine.Application.persistentDataPath, "settings.json");
                _originalFile = File.Exists(_settingsPath) ? File.ReadAllText(_settingsPath) : null;
                title.OpenOptions();
                title.AlignGraphicOption();
                var settings = scope.Container.Resolve<SettingsUseCase>();
                string confirmed = JsonConvert.SerializeObject(settings.GetGraphicsDraft());
                settings.EditGraphics(g => g.SetQualityPreset(Shinzui.Domain.Settings.GraphicsQualityPreset.Low), false);
                title.AlignAudioOption();
                title.AlignGraphicOption();
                Require(settings.GetGraphicsDraft().QualityPreset == Shinzui.Domain.Settings.GraphicsQualityPreset.Low, "Changing tabs lost the draft");
                title.CloseOptions();
                title.OpenOptions();
                title.AlignGraphicOption();
                Require(JsonConvert.SerializeObject(settings.GetGraphicsDraft()) == confirmed, "Closing options did not discard draft");
                settings.ResetToDefault();
                title.CloseOptions();
                Require((File.Exists(_settingsPath) ? File.ReadAllText(_settingsPath) : null) == _originalFile, "Draft/defaults changed the settings file");
                title.StartGame();
                _phase = 1;
            }
            else if (_phase == 1)
            {
                var scope = UnityEngine.Object.FindAnyObjectByType<GenerateTunnelLifetimeScope>();
                if (scope?.Container == null || Shinzui.View.TunnelGateView.ActiveGates.Count == 0) return;
                _camera = Camera.main;
                Require(_camera != null && _camera.GetComponent<HDAdditionalCameraData>() != null, "Addressable gameplay camera is not HDRP");
                Require(scope.Regenerate(9182), "Next-floor regeneration rejected");
                _phase = 2;
                _deadline = EditorApplication.timeSinceStartup + 5;
            }
            else if (_phase == 2)
            {
                // Wait for deferred destruction and the new portal cameras to render
                if (EditorApplication.timeSinceStartup < _deadline - 2) return;
                Require(Camera.main == _camera, "Regeneration replaced the gameplay camera");
                Require(Shinzui.View.TunnelGateView.ActiveGates.Count > 0, "Regeneration lost portal gates");
                Require((File.Exists(_settingsPath) ? File.ReadAllText(_settingsPath) : null) == _originalFile, "Navigation changed persisted preferences");
                Finish(new { passed = true, titleDraftSurvivesTabChange = true, closeCancels = true, defaultsDoNotPersist = true,
                    titleAddressableTransition = true, regenerationSeed = 9182, gates = Shinzui.View.TunnelGateView.ActiveGates.Count,
                    gameplayCameraPreserved = true, settingsFileUnchanged = true });
            }
        }
        catch (Exception error) { Finish(new { passed = false, error = error.ToString() }); }
    }

    /// <summary>Require one smoke-check invariant</summary>
    /// <param name="condition">Observed condition</param>
    /// <param name="message">Actionable failure</param>
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    /// <summary>Save the evidence and exit the opt-in play session</summary>
    /// <param name="result">Pass or failure details</param>
    private static void Finish(object result)
    {
        Directory.CreateDirectory("Artifacts/GraphicsValidation");
        File.WriteAllText("Artifacts/GraphicsValidation/scene-smoke.json", JsonConvert.SerializeObject(result, Formatting.Indented));
        SessionState.EraseBool("Shinzui.SceneSmoke");
        EditorApplication.isPlaying = false;
    }
}
