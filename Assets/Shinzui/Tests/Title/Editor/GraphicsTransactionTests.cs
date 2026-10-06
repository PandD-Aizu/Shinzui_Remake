using NUnit.Framework;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases;
using Shinzui.Domain.Settings;

namespace Shinzui.Tests.Title
{
    /// <summary>Regression coverage for confirmed settings versus uncommitted previews</summary>
    public sealed class GraphicsTransactionTests
    {
        private Store _store;
        private Applier _applier;
        private SettingsUseCase _settings;
        [SetUp] public void SetUp() { _store = new Store(); _applier = new Applier(); _settings = new SettingsUseCase(_store, _applier); _settings.BeginEdit(); }
        [TearDown] public void TearDown() => _settings.Dispose();

        [Test] public void AudioPreviewDoesNotApplyGraphicsOrPersist()
        {
            int initial = _applier.AllCalls;
            _settings.MasterVolume.Value = 0.12f;
            Assert.That(_applier.AllCalls, Is.EqualTo(initial));
            Assert.That(_applier.AudioCalls, Is.EqualTo(1));
            Assert.That(_store.Saves, Is.Zero);
            _settings.CancelEdit();
            Assert.That(_applier.LastVolume, Is.EqualTo(_store.Value.Audio.SystemVolume));
        }

        [Test] public void GraphicsDraftAndDefaultsDoNotApplyDisplay()
        {
            int initial = _applier.AllCalls;
            _settings.EditGraphics(g => g.Resolution = "1280x720", false);
            _settings.ResetToDefault();
            Assert.That(_applier.AllCalls, Is.EqualTo(initial));
            Assert.That(_store.Saves, Is.Zero);
        }

        [Test] public void CancelDiscardsGraphicsAndReopeningReadsConfirmedSnapshot()
        {
            _settings.GraphicsQuality.Value = 0;
            _settings.CancelEdit();
            _settings.BeginEdit();
            Assert.That(_settings.GraphicsQuality.Value, Is.EqualTo((int)_store.Value.Graphics.QualityPreset));
            Assert.That(_store.Saves, Is.Zero);
        }

        [Test] public void DisplayCandidateIsNotPersistedUntilConfirmed()
        {
            _settings.EditGraphics(g => g.Resolution = "1280x720", false);
            _settings.SaveAndApply();
            Assert.That(_settings.AwaitingDisplayConfirmation, Is.True);
            Assert.That(_store.Saves, Is.Zero);
            using (var restarted = new SettingsUseCase(_store, new Applier()))
                Assert.That(restarted.GetGraphicsDraft().Resolution, Is.EqualTo("1920x1080"));
            _settings.ConfirmDisplay();
            Assert.That(_store.Saves, Is.EqualTo(1));
            Assert.That(_store.Value.Graphics.Resolution, Is.EqualTo("1280x720"));
        }

        [TestCase(true, 15.1f)]
        [TestCase(false, 0f)]
        public void TimeoutAndFocusLossRestoreConfirmedDisplay(bool focused, float seconds)
        {
            _settings.EditGraphics(g => g.ScreenMode = 1, false);
            _settings.SaveAndApply();
            _settings.TickDisplayConfirmation(seconds, focused);
            Assert.That(_settings.AwaitingDisplayConfirmation, Is.False);
            Assert.That(_applier.Last.Graphics.ScreenMode, Is.EqualTo(_store.Value.Graphics.ScreenMode));
            Assert.That(_store.Saves, Is.Zero);
        }

        [Test] public void AppliedOverridesAreCustomAndSurviveRestart()
        {
            _settings.EditGraphics(g => { g.EnableMotionBlur = true; g.PortalResolutionScale = 0.5f; });
            _settings.SaveAndApply();
            using var restarted = new SettingsUseCase(_store, new Applier());
            var graphics = restarted.GetGraphicsDraft();
            Assert.That(graphics.QualityPreset, Is.EqualTo(GraphicsQualityPreset.Custom));
            Assert.That(graphics.EnableMotionBlur, Is.True);
            Assert.That(graphics.PortalResolutionScale, Is.EqualTo(0.5f));
        }

        [Test] public void PresetsReplaceOverridesAndPreserveDisplay()
        {
            var graphics = new GraphicsSettings { Resolution = "2560x1440", FrameRateLimit = 144 };
            graphics.SetQualityPreset(GraphicsQualityPreset.Low);
            Assert.That(graphics.EnableRayTracing, Is.False);
            graphics.SetQualityPreset(GraphicsQualityPreset.Ultra);
            Assert.That(graphics.EnableRayTracing, Is.True);
            Assert.That(graphics.ImageQualityScale, Is.EqualTo(1f));
            Assert.That(graphics.Resolution, Is.EqualTo("2560x1440"));
            Assert.That(graphics.FrameRateLimit, Is.EqualTo(144));
        }

        [Test] public void GraphicsCloneIncludesEveryWritableProperty()
        {
            var original = new GameSettings();
            foreach (var property in typeof(GraphicsSettings).GetProperties())
            {
                if (!property.CanWrite) continue;
                if (property.PropertyType == typeof(bool)) property.SetValue(original.Graphics, !(bool)property.GetValue(original.Graphics));
                else if (property.PropertyType == typeof(int)) property.SetValue(original.Graphics, 17);
                else if (property.PropertyType == typeof(float)) property.SetValue(original.Graphics, 0.37f);
                else if (property.PropertyType == typeof(string)) property.SetValue(original.Graphics, "sentinel");
            }
            var clone = original.Clone();
            foreach (var property in typeof(GraphicsSettings).GetProperties())
                Assert.That(property.GetValue(clone.Graphics), Is.EqualTo(property.GetValue(original.Graphics)), property.Name);
            Assert.That(clone.Graphics, Is.Not.SameAs(original.Graphics));
        }

        [Test] public void SaveFailureRollsBackAppliedCandidateAndReportsFailure()
        {
            _store.FailSave = true;
            _settings.EditGraphics(g => g.ScreenMode = 1, false);
            _settings.SaveAndApply();
            _settings.ConfirmDisplay();
            Assert.That(_settings.AwaitingDisplayConfirmation, Is.False);
            Assert.That(_settings.LastError, Is.Not.Empty);
            Assert.That(_store.Saves, Is.Zero);
            Assert.That(_settings.GetGraphicsDraft().ScreenMode, Is.EqualTo(_store.Value.Graphics.ScreenMode));
            Assert.That(_applier.Last.Graphics.ScreenMode, Is.EqualTo(_store.Value.Graphics.ScreenMode));
        }

        [Test] public void LegacyUltraRasterOverrideIsHonestlyLabelledCustom()
        {
            var graphics = new GraphicsSettings { EnableRayTracing = false };
            graphics.Validate();
            Assert.That(graphics.QualityPreset, Is.EqualTo(GraphicsQualityPreset.Custom));
            Assert.That(graphics.EnableRayTracing, Is.False);
            graphics.SetQualityPreset(GraphicsQualityPreset.Ultra);
            Assert.That(graphics.EnableRayTracing, Is.True);
        }

        private sealed class Store : ISettingsRepository
        {
            public GameSettings Value = new();
            public int Saves;
            public bool FailSave;
            public GameSettings Load() => Value.Clone();
            public void Save(GameSettings settings) { if (FailSave) throw new System.IO.IOException("Simulated disk failure"); Value = settings.Clone(); Saves++; }
        }
        private sealed class Applier : ISettingsApplier
        {
            public int AllCalls, AudioCalls;
            public float LastVolume;
            public GameSettings Last;
            public void ApplyAudio(AudioSettings value) { AudioCalls++; LastVolume = value.SystemVolume; }
            public void ApplyGraphics(GraphicsSettings value) { }
            public void ApplyCamera(CameraSettings value) { }
            public void ApplyControls(ControlsSettings value) { }
            public void ApplyGameplay(GameplaySettings value) { }
            public void ApplyLanguage(LanguageSettings value) { }
            public void ApplyAccessibility(AccessibilitySettings value) { }
            public void ApplyAll(GameSettings value) { AllCalls++; Last = value.Clone(); }
        }
    }
}
