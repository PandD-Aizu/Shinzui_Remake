using UnityEditor;
using UnityEngine;
using Shinzui.Domain.Settings;
using Shinzui.Application.UseCases;
using Shinzui.Infrastructure.Repositories;
using Shinzui.Infrastructure.Services;
using Shinzui.DI;
using VContainer;

namespace Shinzui.Editor
{
    /// <summary>
    /// 設定データのデバッグおよび簡単切り替えを行うためのUnityエディタウィンドウ
    /// プレイモード中は稼働中のUseCaseへ即時同期（プレビュー反映）、
    /// エディットモード中はsettings.jsonファイルへ直接読み書きを行う
    /// </summary>
    public class SettingsDebugWindow : EditorWindow
    {
        private GameSettings _editSettings = new();
        private FileSettingsRepository _repository;
        private UnitySettingsApplier _applier;
        
        // 実行中のユースケースのキャッシュ
        private SettingsUseCase _activeUseCase;

        [MenuItem("Tools/Settings Debugger")]
        public static void ShowWindow()
        {
            GetWindow<SettingsDebugWindow>("Settings Debugger");
        }

        private void OnEnable()
        {
            _repository = new FileSettingsRepository();
            _applier = new UnitySettingsApplier();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            if (EditorApplication.isPlaying)
            {
                var lifetimeScope = FindFirstObjectByType<SettingsLifetimeScope>();
                if (lifetimeScope != null && lifetimeScope.Container != null)
                {
                    _activeUseCase = lifetimeScope.Container.Resolve<SettingsUseCase>();
                }
            }
            
            _editSettings = _repository.Load();
        }

        private void OnGUI()
        {
            CheckPlayModeState();

            GUILayout.Label("Game Settings Debugger", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            if (EditorApplication.isPlaying)
            {
                if (_activeUseCase != null)
                {
                    EditorGUILayout.HelpBox("Playing Mode: Live syncing with SettingsUseCase", MessageType.Info);
                    DrawLiveUseCaseGui();
                }
                else
                {
                    EditorGUILayout.HelpBox("Playing Mode: Waiting for SettingsLifetimeScope to initialize...", MessageType.Warning);
                    if (GUILayout.Button("Force Find LifetimeScope"))
                    {
                        LoadCurrentSettings();
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Editor Mode: Modifying settings.json directly", MessageType.Info);
                DrawFileDirectGui();
            }

            DrawPresetsGui();
        }

        private void CheckPlayModeState()
        {
            if (EditorApplication.isPlaying && _activeUseCase == null)
            {
                var lifetimeScope = FindFirstObjectByType<SettingsLifetimeScope>();
                if (lifetimeScope != null && lifetimeScope.Container != null)
                {
                    _activeUseCase = lifetimeScope.Container.Resolve<SettingsUseCase>();
                }
            }
            else if (!EditorApplication.isPlaying && _activeUseCase != null)
            {
                _activeUseCase = null;
                LoadCurrentSettings();
            }
        }

        private void DrawLiveUseCaseGui()
        {
            EditorGUI.BeginChangeCheck();

            // 音量
            float master = EditorGUILayout.Slider("Master Volume", _activeUseCase.MasterVolume.Value, 0f, 1f);
            float bgm = EditorGUILayout.Slider("BGM Volume", _activeUseCase.BgmVolume.Value, 0f, 1f);
            float se = EditorGUILayout.Slider("SE Volume", _activeUseCase.SeVolume.Value, 0f, 1f);
            float voice = EditorGUILayout.Slider("Voice Volume", _activeUseCase.VoiceVolume.Value, 0f, 1f);

            // カメラ
            float ctrlSpeed = EditorGUILayout.Slider("Controller Camera Speed", _activeUseCase.ControllerNormalSpeed.Value, 0f, 1f);
            float mouseSens = EditorGUILayout.Slider("Mouse Sensitivity", _activeUseCase.MouseNormalSensitivity.Value, 0f, 1f);

            // グラフィックス
            float brightness = EditorGUILayout.Slider("Brightness", _activeUseCase.Brightness.Value, 0f, 1f);

            // アクセシビリティ
            bool centerDot = EditorGUILayout.Toggle("Show Center Dot", _activeUseCase.ShowCenterDot.Value);

            if (EditorGUI.EndChangeCheck())
            {
                _activeUseCase.MasterVolume.Value = master;
                _activeUseCase.BgmVolume.Value = bgm;
                _activeUseCase.SeVolume.Value = se;
                _activeUseCase.VoiceVolume.Value = voice;
                _activeUseCase.ControllerNormalSpeed.Value = ctrlSpeed;
                _activeUseCase.MouseNormalSensitivity.Value = mouseSens;
                _activeUseCase.Brightness.Value = brightness;
                _activeUseCase.ShowCenterDot.Value = centerDot;
            }

            GUILayout.Space(10);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save and Apply"))
            {
                _activeUseCase.SaveAndApply();
                Debug.Log("[Debugger] Live settings saved and applied.");
            }
            if (GUILayout.Button("Cancel (Rollback)"))
            {
                _activeUseCase.CancelEdit();
                Debug.Log("[Debugger] Live settings editing rolled back.");
            }
            if (GUILayout.Button("Reset to Default"))
            {
                _activeUseCase.ResetToDefault();
                Debug.Log("[Debugger] Live settings reset to default.");
            }
            GUILayout.EndHorizontal();
        }

        private void DrawFileDirectGui()
        {
            EditorGUI.BeginChangeCheck();

            // 音量
            _editSettings.Audio.SystemVolume = EditorGUILayout.Slider("Master Volume", _editSettings.Audio.SystemVolume, 0f, 1f);
            _editSettings.Audio.BgmVolume = EditorGUILayout.Slider("BGM Volume", _editSettings.Audio.BgmVolume, 0f, 1f);
            _editSettings.Audio.SeVolume = EditorGUILayout.Slider("SE Volume", _editSettings.Audio.SeVolume, 0f, 1f);
            _editSettings.Audio.VoiceVolume = EditorGUILayout.Slider("Voice Volume", _editSettings.Audio.VoiceVolume, 0f, 1f);

            // カメラ
            _editSettings.Camera.ControllerNormalSpeed = EditorGUILayout.Slider("Controller Camera Speed", _editSettings.Camera.ControllerNormalSpeed, 0f, 1f);
            _editSettings.Camera.MouseNormalSensitivity = EditorGUILayout.Slider("Mouse Sensitivity", _editSettings.Camera.MouseNormalSensitivity, 0f, 1f);

            // グラフィックス
            _editSettings.Graphics.BrightnessValue = EditorGUILayout.Slider("Brightness", _editSettings.Graphics.BrightnessValue, 0f, 1f);

            // アクセシビリティ
            _editSettings.Accessibility.ShowCenterDot = EditorGUILayout.Toggle("Show Center Dot", _editSettings.Accessibility.ShowCenterDot);

            if (EditorGUI.EndChangeCheck())
            {
                // GUI値のキャッシュ更新
            }

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save to File"))
            {
                _repository.Save(_editSettings);
                Debug.Log("[Debugger] settings.json saved directly.");
            }
            if (GUILayout.Button("Reload from File"))
            {
                _editSettings = _repository.Load();
                Debug.Log("[Debugger] settings.json reloaded.");
            }
            GUILayout.EndHorizontal();
        }

        private void DrawPresetsGui()
        {
            GUILayout.Space(15);
            GUILayout.Label("Debug Presets", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Preset: Mute (無音)"))
            {
                ApplyPreset(0f, 0f, 0f, 0f, null, null, null, null);
            }
            if (GUILayout.Button("Preset: Bright (明度最大)"))
            {
                ApplyPreset(null, null, null, null, null, null, 1.0f, null);
            }
            if (GUILayout.Button("Preset: Fast Turn (感度最大)"))
            {
                ApplyPreset(null, null, null, null, 1.0f, 1.0f, null, null);
            }
            if (GUILayout.Button("Preset: Reset Default"))
            {
                var def = new GameSettings();
                def.ResetToDefault();
                ApplyPreset(
                    def.Audio.SystemVolume,
                    def.Audio.BgmVolume,
                    def.Audio.SeVolume,
                    def.Audio.VoiceVolume,
                    def.Camera.ControllerNormalSpeed,
                    def.Camera.MouseNormalSensitivity,
                    def.Graphics.BrightnessValue,
                    def.Accessibility.ShowCenterDot
                );
            }
            GUILayout.EndHorizontal();
        }

        private void ApplyPreset(
            float? master, float? bgm, float? se, float? voice,
            float? ctrlSpeed, float? mouseSens,
            float? brightness, bool? showCenterDot
        )
        {
            if (EditorApplication.isPlaying && _activeUseCase != null)
            {
                if (master.HasValue) _activeUseCase.MasterVolume.Value = master.Value;
                if (bgm.HasValue) _activeUseCase.BgmVolume.Value = bgm.Value;
                if (se.HasValue) _activeUseCase.SeVolume.Value = se.Value;
                if (voice.HasValue) _activeUseCase.VoiceVolume.Value = voice.Value;
                if (ctrlSpeed.HasValue) _activeUseCase.ControllerNormalSpeed.Value = ctrlSpeed.Value;
                if (mouseSens.HasValue) _activeUseCase.MouseNormalSensitivity.Value = mouseSens.Value;
                if (brightness.HasValue) _activeUseCase.Brightness.Value = brightness.Value;
                if (showCenterDot.HasValue) _activeUseCase.ShowCenterDot.Value = showCenterDot.Value;
                
                _activeUseCase.SaveAndApply();
                Debug.Log("[Debugger] Live Preset applied and saved.");
            }
            else
            {
                var s = _repository.Load();
                if (master.HasValue) s.Audio.SystemVolume = master.Value;
                if (bgm.HasValue) s.Audio.BgmVolume = bgm.Value;
                if (se.HasValue) s.Audio.SeVolume = se.Value;
                if (voice.HasValue) s.Audio.VoiceVolume = voice.Value;
                if (ctrlSpeed.HasValue) s.Camera.ControllerNormalSpeed = ctrlSpeed.Value;
                if (mouseSens.HasValue) s.Camera.MouseNormalSensitivity = mouseSens.Value;
                if (brightness.HasValue) s.Graphics.BrightnessValue = brightness.Value;
                if (showCenterDot.HasValue) s.Accessibility.ShowCenterDot = showCenterDot.Value;

                _repository.Save(s);
                _editSettings = s;
                Debug.Log("[Debugger] File-direct Preset applied and saved.");
            }
        }
    }
}
