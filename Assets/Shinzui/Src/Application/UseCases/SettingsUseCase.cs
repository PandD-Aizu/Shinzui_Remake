using System;
using R3;
using Shinzui.Application.Interfaces;
using Shinzui.Domain.Settings;

namespace Shinzui.Application.UseCases
{
    /// <summary>Owns a confirmed snapshot and a disposable options draft</summary>
    public class SettingsUseCase : IDisposable
    {
        private readonly ISettingsRepository _repository;
        private readonly ISettingsApplier _applier;
        private readonly CompositeDisposable _editDisposables = new();
        private GameSettings _activeSettings;
        private GameSettings _editingSettings;
        private GameSettings _pendingSettings;
        private float _confirmationSeconds;
        public event Action DraftChanged;
        public string LastError { get; private set; }
        public bool AwaitingDisplayConfirmation => _pendingSettings != null;
        public float ConfirmationSeconds => _confirmationSeconds;
        public bool IsEditing => _editingSettings != null;
        public ReactiveProperty<float> MasterVolume { get; } = new();
        public ReactiveProperty<float> BgmVolume { get; } = new();
        public ReactiveProperty<float> SeVolume { get; } = new();
        public ReactiveProperty<float> VoiceVolume { get; } = new();
        public ReactiveProperty<float> ControllerNormalSpeed { get; } = new();
        public ReactiveProperty<float> MouseNormalSensitivity { get; } = new();
        public ReactiveProperty<float> Brightness { get; } = new();
        public ReactiveProperty<int> GraphicsQuality { get; } = new();
        public ReactiveProperty<bool> ShowCenterDot { get; } = new();

        /// <summary>Load confirmed settings and apply them on startup</summary>
        /// <param name="repository">Persistent store</param>
        /// <param name="applier">Category-specific engine adapter</param>
        public SettingsUseCase(ISettingsRepository repository, ISettingsApplier applier)
        {
            _repository = repository;
            _applier = applier;
            Initialize();
        }

        /// <summary>Load and validate the persistent snapshot</summary>
        public void Initialize()
        {
            _activeSettings = _repository.Load();
            _activeSettings.Graphics.Validate();
            _applier.ApplyAll(_activeSettings);
        }

        /// <summary>Start a fresh draft and restore any previous preview</summary>
        public void BeginEdit()
        {
            if (IsEditing) CancelEdit();
            _editingSettings = _activeSettings.Clone();
            SynchronizeDraft();
        }

        /// <summary>Return an independent graphics snapshot for the presenter</summary>
        /// <returns>The draft or last confirmed graphics settings</returns>
        public GraphicsSettings GetGraphicsDraft() => (_editingSettings ?? _activeSettings).Graphics.Clone();

        /// <summary>Edit graphics without applying a disruptive display change</summary>
        /// <param name="edit">Draft mutation</param>
        /// <param name="custom">Whether this changes a preset detail</param>
        public void EditGraphics(Action<GraphicsSettings> edit, bool custom = true)
        {
            if (!IsEditing || AwaitingDisplayConfirmation) return;
            edit(_editingSettings.Graphics);
            if (custom) _editingSettings.Graphics.QualityPreset = GraphicsQualityPreset.Custom;
            _editingSettings.Graphics.Validate();
            SynchronizeDraft();
        }

        /// <summary>Synchronize controls without triggering preview handlers</summary>
        private void SynchronizeDraft()
        {
            _editDisposables.Clear();
            var model = _editingSettings;
            MasterVolume.Value = model.Audio.SystemVolume;
            BgmVolume.Value = model.Audio.BgmVolume;
            SeVolume.Value = model.Audio.SeVolume;
            VoiceVolume.Value = model.Audio.VoiceVolume;
            ControllerNormalSpeed.Value = model.Camera.ControllerNormalSpeed;
            MouseNormalSensitivity.Value = model.Camera.MouseNormalSensitivity;
            Brightness.Value = model.Graphics.BrightnessValue;
            GraphicsQuality.Value = (int)model.Graphics.QualityPreset;
            ShowCenterDot.Value = model.Accessibility.ShowCenterDot;
            BindPropertiesToModel();
            DraftChanged?.Invoke();
        }

        /// <summary>Preview only the category touched by a live control</summary>
        private void BindPropertiesToModel()
        {
            MasterVolume.Skip(1).Subscribe(v => { if (CanEdit()) { _editingSettings.Audio.SystemVolume = v; _applier.ApplyAudio(_editingSettings.Audio); } }).AddTo(_editDisposables);
            BgmVolume.Skip(1).Subscribe(v => { if (CanEdit()) { _editingSettings.Audio.BgmVolume = v; _applier.ApplyAudio(_editingSettings.Audio); } }).AddTo(_editDisposables);
            SeVolume.Skip(1).Subscribe(v => { if (CanEdit()) { _editingSettings.Audio.SeVolume = v; _applier.ApplyAudio(_editingSettings.Audio); } }).AddTo(_editDisposables);
            VoiceVolume.Skip(1).Subscribe(v => { if (CanEdit()) { _editingSettings.Audio.VoiceVolume = v; _applier.ApplyAudio(_editingSettings.Audio); } }).AddTo(_editDisposables);
            ControllerNormalSpeed.Skip(1).Subscribe(v => { if (CanEdit()) { _editingSettings.Camera.ControllerNormalSpeed = v; _applier.ApplyCamera(_editingSettings.Camera); } }).AddTo(_editDisposables);
            MouseNormalSensitivity.Skip(1).Subscribe(v => { if (CanEdit()) { _editingSettings.Camera.MouseNormalSensitivity = v; _applier.ApplyCamera(_editingSettings.Camera); } }).AddTo(_editDisposables);
            ShowCenterDot.Skip(1).Subscribe(v => { if (CanEdit()) { _editingSettings.Accessibility.ShowCenterDot = v; _applier.ApplyAccessibility(_editingSettings.Accessibility); } }).AddTo(_editDisposables);
            Brightness.Skip(1).Subscribe(v => { if (CanEdit()) _editingSettings.Graphics.BrightnessValue = v; }).AddTo(_editDisposables);
            GraphicsQuality.Skip(1).Subscribe(v =>
            {
                if (!CanEdit()) return;
                _editingSettings.Graphics.SetQualityPreset((GraphicsQualityPreset)Math.Clamp(v, 0, 4));
                DraftChanged?.Invoke();
            }).AddTo(_editDisposables);
        }

        /// <summary>Prevent background controls from changing a pending candidate</summary>
        /// <returns>Whether the current draft accepts changes</returns>
        private bool CanEdit() => IsEditing && !AwaitingDisplayConfirmation;

        /// <summary>Apply a candidate, requiring confirmation for disruptive display changes</summary>
        public void SaveAndApply()
        {
            if (!CanEdit()) return;
            LastError = null;
            _editingSettings.Graphics.Validate();
            var candidate = _editingSettings.Clone();
            _applier.ApplyAll(candidate);
            if (candidate.Graphics.DisplayDiffersFrom(_activeSettings.Graphics))
            {
                _pendingSettings = candidate;
                _confirmationSeconds = 15f;
                DraftChanged?.Invoke();
                return;
            }
            Commit(candidate);
        }

        /// <summary>Persist only a confirmed candidate, leaving options ready for further editing</summary>
        /// <param name="candidate">Confirmed settings</param>
        private void Commit(GameSettings candidate)
        {
            try { _repository.Save(candidate); }
            catch (Exception)
            {
                LastError = "Settings could not be saved. Previous settings restored.";
                _applier.ApplyAll(_activeSettings);
                _pendingSettings = null;
                _editingSettings = _activeSettings.Clone();
                SynchronizeDraft();
                return;
            }
            _activeSettings = candidate.Clone();
            _pendingSettings = null;
            _editingSettings = _activeSettings.Clone();
            SynchronizeDraft();
        }

        /// <summary>Accept the pending display change and persist it</summary>
        public void ConfirmDisplay()
        {
            if (_pendingSettings != null) Commit(_pendingSettings);
        }

        /// <summary>Restore the last confirmed settings after rejecting a display mode</summary>
        public void RejectDisplay()
        {
            if (_pendingSettings == null) return;
            _pendingSettings = null;
            _applier.ApplyAll(_activeSettings);
            _editingSettings = _activeSettings.Clone();
            SynchronizeDraft();
        }

        /// <summary>Use unscaled elapsed time and revert immediately on focus loss</summary>
        /// <param name="deltaSeconds">Unscaled elapsed seconds</param>
        /// <param name="hasFocus">Whether the application still has focus</param>
        public void TickDisplayConfirmation(float deltaSeconds, bool hasFocus)
        {
            if (!AwaitingDisplayConfirmation) return;
            _confirmationSeconds -= Math.Max(0f, deltaSeconds);
            if (!hasFocus || _confirmationSeconds <= 0f) RejectDisplay();
        }

        /// <summary>Discard the draft and restore only categories that were previewed</summary>
        public void CancelEdit()
        {
            if (!IsEditing) return;
            if (AwaitingDisplayConfirmation) RejectDisplay();
            _applier.ApplyAudio(_activeSettings.Audio);
            _applier.ApplyCamera(_activeSettings.Camera);
            _applier.ApplyAccessibility(_activeSettings.Accessibility);
            _editDisposables.Clear();
            _editingSettings = null;
        }

        /// <summary>Reset the draft without writing disk or changing the display mode</summary>
        public void ResetToDefault()
        {
            if (AwaitingDisplayConfirmation) RejectDisplay();
            _editingSettings = new GameSettings();
            _applier.ApplyAudio(_editingSettings.Audio);
            _applier.ApplyCamera(_editingSettings.Camera);
            _applier.ApplyAccessibility(_editingSettings.Accessibility);
            SynchronizeDraft();
        }

        /// <summary>Identify legacy console builds</summary>
        /// <returns>Whether console-specific UI is required</returns>
        public bool IsConsolePlatform()
        {
#if UNITY_SWITCH && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        /// <summary>Roll back uncommitted previews and release subscriptions</summary>
        public void Dispose()
        {
            CancelEdit();
            _editDisposables.Dispose();
            MasterVolume.Dispose(); BgmVolume.Dispose(); SeVolume.Dispose(); VoiceVolume.Dispose();
            ControllerNormalSpeed.Dispose(); MouseNormalSensitivity.Dispose();
            Brightness.Dispose(); GraphicsQuality.Dispose(); ShowCenterDot.Dispose();
        }
    }
}
