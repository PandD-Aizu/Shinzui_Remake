using System;
using System.Collections.Generic;
using System.Linq;
using Shinzui.Application.UseCases;
using Shinzui.Domain.Settings;
using Shinzui.View.Settings;
using UnityEngine;
using Gfx = Shinzui.Domain.Settings.GraphicsSettings;

namespace Shinzui.Presentation.Settings
{
    /// <summary>Maps the detailed title controls to a transactional graphics draft</summary>
    public sealed class GraphicsOptionsPresenter : IDisposable
    {
        private readonly SettingsUseCase _settings;
        private readonly GraphicsOptionsView _view;
        private readonly List<Action<Gfx>> _refresh = new();
        private readonly Resolution[] _modes;
        private float _capabilityElapsed;

        /// <summary>Bind supported display modes and individual graphics settings</summary>
        /// <param name="settings">Draft owner</param>
        /// <param name="view">Authored title extension</param>
        public GraphicsOptionsPresenter(SettingsUseCase settings, GraphicsOptionsView view)
        {
            _settings = settings;
            _view = view;
            _modes = Screen.resolutions.GroupBy(r => $"{r.width}x{r.height}@{Mathf.RoundToInt((float)r.refreshRateRatio.value)}")
                .Select(g => g.First()).ToArray();
            if (_modes.Length == 0) _modes = new[] { Screen.currentResolution };
            Choice("Preset", "Quality preset", new[] { "Low", "Medium", "High", "Ultra", "Custom" },
                g => (int)g.QualityPreset, (g, v) => g.SetQualityPreset((GraphicsQualityPreset)v), false);
            Choice("Mode", "Display mode", new[] { "Exclusive fullscreen", "Windowed", "Borderless" }, g => g.ScreenMode, (g, v) => g.ScreenMode = v, false);
            Choice("Resolution", "Resolution / refresh", _modes.Select(ModeLabel).ToArray(),
                g => Array.FindIndex(_modes, r => g.Resolution == $"{r.width}x{r.height}" && g.RefreshRate == Mathf.RoundToInt((float)r.refreshRateRatio.value)),
                (g, v) => { g.Resolution = $"{_modes[v].width}x{_modes[v].height}"; g.RefreshRate = Mathf.RoundToInt((float)_modes[v].refreshRateRatio.value); }, false);
            if (HDROutputSettings.main != null && HDROutputSettings.main.available)
                Toggle("HDR", "HDR display output", g => g.EnableHDR, (g, v) => g.EnableHDR = v, false);
            Toggle("VSync", "VSync (overrides FPS cap)", g => g.EnableVSync, (g, v) => g.EnableVSync = v, false);
            int[] caps = { 0, 30, 60, 90, 120, 144, 165, 240, 360 };
            Choice("Cap", "FPS limit (VSync off)", caps.Select(v => v == 0 ? "Unlimited" : v.ToString()).ToArray(),
                g => Array.IndexOf(caps, g.FrameRateLimit), (g, v) => g.FrameRateLimit = caps[v], false);
            Choice("Texture", "Texture resolution", new[] { "1/8", "1/4", "1/2", "Full" }, g => g.TextureQuality, (g, v) => g.TextureQuality = v);
            Choice("Filtering", "Anisotropic filtering", new[] { "2x", "4x", "8x", "16x" }, g => g.TextureFilteringQuality, (g, v) => g.TextureFilteringQuality = v);
            Choice("Mesh", "Mesh detail", new[] { "Low", "Medium", "High" }, g => g.MeshQuality, (g, v) => g.MeshQuality = v);
            Choice("AA", "Anti-aliasing", new[] { "Off", "FXAA", "SMAA", "TAA" }, g => g.AntiAliasingType, (g, v) => g.AntiAliasingType = v);
            Choice("Shadow", "Shadows", new[] { "Off", "Low", "Medium", "High" }, g => g.ShadowQuality, (g, v) => g.ShadowQuality = v);
            Choice("GI", "Indirect lighting / reflections", new[] { "Off", "Low", "Medium", "High" }, g => g.GiAndReflectionQuality, (g, v) => g.GiAndReflectionQuality = v);
            Choice("Fog", "Volumetric fog", new[] { "Off", "Low", "Medium", "High" }, g => g.VolumeLightQuality, (g, v) => g.VolumeLightQuality = v);
            Toggle("RT", "Request hardware ray tracing", g => g.EnableRayTracing, (g, v) => g.EnableRayTracing = v);
            Toggle("AO", "Ambient occlusion", g => g.EnableAo, (g, v) => g.EnableAo = v);
            Toggle("SSR", "Reflections (RT / screen-space)", g => g.EnableSsr, (g, v) => g.EnableSsr = v);
            Choice("ReflectionStrength", "Reflection intensity", Enumerable.Range(0, 11).Select(v => $"{v * 10}%").ToArray(),
                g => Mathf.RoundToInt(g.ReflectionIntensity * 10), (g, v) => g.ReflectionIntensity = v / 10f);
            Toggle("Contact", "Contact shadows", g => g.EnableContactShadow, (g, v) => g.EnableContactShadow = v);
            Toggle("Bloom", "Bloom", g => g.EnableBloom, (g, v) => g.EnableBloom = v);
            Toggle("DOF", "Depth of field", g => g.EnableDepthOfField, (g, v) => g.EnableDepthOfField = v);
            Toggle("Blur", "Motion blur", g => g.EnableMotionBlur, (g, v) => g.EnableMotionBlur = v);
            Toggle("Grain", "Film grain", g => g.EnableFilmGrain, (g, v) => g.EnableFilmGrain = v);
            Toggle("Distortion", "Lens distortion", g => g.EnableLensDistortion, (g, v) => g.EnableLensDistortion = v);
            Choice("Brightness", "Brightness", Enumerable.Range(0, 11).Select(v => $"{v * 10}%").ToArray(),
                g => Mathf.RoundToInt(g.BrightnessValue * 10), (g, v) => g.BrightnessValue = v / 10f, false);
            float[] portalScales = { .25f, .5f, .625f, .75f, 1f };
            Choice("Portal", "Portal resolution", new[] { "25%", "50%", "62.5%", "75%", "100%" },
                g => Array.FindIndex(portalScales, v => Mathf.Approximately(v, g.PortalResolutionScale)), (g, v) => g.PortalResolutionScale = portalScales[v]);
            _view.ApplyRequested += Apply;
            _view.CancelRequested += Cancel;
            _view.DefaultsRequested += Defaults;
            _view.ConfirmRequested += Confirm;
            _view.RevertRequested += Revert;
            _view.TickRequested += Tick;
            _settings.DraftChanged += Refresh;
            Refresh();
        }

        /// <summary>Format a monitor-supported mode</summary>
        /// <param name="mode">Enumerated display mode</param>
        /// <returns>Resolution and refresh label</returns>
        private static string ModeLabel(Resolution mode) => $"{mode.width} x {mode.height} / {mode.refreshRateRatio.value:0.##} Hz";

        /// <summary>Add an enumerated draft control</summary>
        /// <param name="key">Row identifier</param>
        /// <param name="label">Visible label</param>
        /// <param name="options">Choice labels</param>
        /// <param name="get">Read draft value</param>
        /// <param name="set">Write draft value</param>
        /// <param name="custom">Mark a detailed preset override</param>
        private void Choice(string key, string label, string[] options, Func<Gfx, int> get, Action<Gfx, int> set, bool custom = true)
        {
            _view.AddChoice(key, label, delta =>
            {
                var current = get(_settings.GetGraphicsDraft());
                var next = (Math.Max(0, current) + delta + options.Length) % options.Length;
                _settings.EditGraphics(g => set(g, next), custom);
            });
            _refresh.Add(g => { int value = get(g); _view.SetChoice(key, value >= 0 && value < options.Length ? options[value] : "Current / unavailable"); });
        }

        /// <summary>Add a boolean draft control</summary>
        /// <param name="key">Row identifier</param>
        /// <param name="label">Visible label</param>
        /// <param name="get">Read value</param>
        /// <param name="set">Write value</param>
        /// <param name="custom">Mark detailed override</param>
        private void Toggle(string key, string label, Func<Gfx, bool> get, Action<Gfx, bool> set, bool custom = true) =>
            Choice(key, label, new[] { "Off", "On" }, g => get(g) ? 1 : 0, (g, v) => set(g, v != 0), custom);

        /// <summary>Apply the draft, reporting failures without leaving confirmation hidden</summary>
        private void Apply() { _settings.SaveAndApply(); Refresh(); }
        /// <summary>Discard previews and refresh the editable confirmed snapshot</summary>
        private void Cancel() { _settings.CancelEdit(); if (_view.isActiveAndEnabled) _settings.BeginEdit(); }
        /// <summary>Restore draft defaults</summary>
        private void Defaults() => _settings.ResetToDefault();
        /// <summary>Accept the pending display mode</summary>
        private void Confirm() { _settings.ConfirmDisplay(); Refresh(); }
        /// <summary>Reject the pending display mode</summary>
        private void Revert() { _settings.RejectDisplay(); Refresh(); }
        /// <summary>Advance the display timeout</summary>
        /// <param name="delta">Unscaled seconds</param>
        /// <param name="focus">Application focus</param>
        private void Tick(float delta, bool focus)
        {
            _settings.TickDisplayConfirmation(delta, focus);
            _view.SetConfirmation(_settings.AwaitingDisplayConfirmation, _settings.ConfirmationSeconds);
            _capabilityElapsed += delta;
            if (_capabilityElapsed >= 1) { _capabilityElapsed = 0; Refresh(); }
        }
        /// <summary>Refresh all rows after preset, defaults or rollback</summary>
        private void Refresh()
        {
            var graphics = _settings.GetGraphicsDraft();
            foreach (var update in _refresh) update(graphics);
            _view.SetCapability(_settings.LastError ?? (_view.GetCapabilityReport() + $"\nDraft: {graphics.QualityPreset} / RT {(graphics.EnableRayTracing ? "On" : "Off")}"));
            _view.SetConfirmation(_settings.AwaitingDisplayConfirmation, _settings.ConfirmationSeconds);
        }
        /// <summary>Release view and draft events</summary>
        public void Dispose()
        {
            _view.ApplyRequested -= Apply; _view.CancelRequested -= Cancel; _view.DefaultsRequested -= Defaults;
            _view.ConfirmRequested -= Confirm; _view.RevertRequested -= Revert; _view.TickRequested -= Tick;
            _settings.DraftChanged -= Refresh;
        }
    }
}
