using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using Shinzui.Application.Interfaces;
using Shinzui.Application.Title;
using Shinzui.Application.UseCases;
using Shinzui.DI;
using Shinzui.Domain.Settings;
using Shinzui.Infrastructure.Services;
using Shinzui.Infrastructure.Repositories;
using Shinzui.Presentation.Title;
using Shinzui.Src.Title;
using Shinzui.View.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace Shinzui.Tests.Title
{
    public sealed class TitleLifetimeTests
    {
        private GameObject _root;
        private CreditData _data;
        private ButtonController _view;
        private OptionWindowView _optionView;
        private TitleLifetimeScope _scope;
        private IObjectResolver _container;
        private Navigation _navigation;
        private SettingsStore _store;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Title DI test");
            _root.SetActive(false);
            _view = Child("Title view").AddComponent<ButtonController>();
            var titlePanel = Child("Title");
            var optionPanel = Child("Options");
            var creditsPanel = Child("Credits");
            Set(_view, "titlePanel", titlePanel);
            Set(_view, "optionPanel", optionPanel);
            Set(_view, "creditsPanel", creditsPanel);
            Set(_view, "screenBackgroundImage", Child("Screen").AddComponent<Image>());
            var creditsView = creditsPanel.AddComponent<CreditScreenView>();
            _optionView = optionPanel.AddComponent<OptionWindowView>();
            Set(_optionView, "generateCategoryTabs", false);
            foreach (var field in new[] { "masterVolumeSlider", "bgmVolumeSlider", "seVolumeSlider", "voiceVolumeSlider", "controllerSpeedSlider", "mouseSensitivitySlider", "brightnessSlider" })
            {
                var slider = Child(field).AddComponent<Slider>();
                if (field == "controllerSpeedSlider" || field == "mouseSensitivitySlider")
                {
                    slider.minValue = 1f;
                    slider.maxValue = 100f;
                }
                Set(_optionView, field, slider);
            }
            Set(_optionView, "centerDotToggle", Child("Center dot").AddComponent<Toggle>());
            _data = ScriptableObject.CreateInstance<CreditData>();
            _scope = _root.AddComponent<TitleLifetimeScope>();
            Set(_scope, "titleView", _view);
            Set(_scope, "creditView", creditsView);
            Set(_scope, "optionView", _optionView);
            Set(_scope, "creditData", _data);
            Set(_scope, "gameSceneAddress", "game-address");
            _navigation = new Navigation();
            _store = new SettingsStore();
        }

        [TearDown]
        public void TearDown()
        {
            _container?.Dispose();
            _container = null;
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_data);
        }

        private void Build()
        {
            // Exercise the production composition root; replace only engine/file adapters.
            var builder = new ContainerBuilder();
            typeof(TitleLifetimeScope).GetMethod("Configure", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_scope, new object[] { builder });
            builder.RegisterInstance<ITitleNavigation>(_navigation);
            builder.RegisterInstance<ISettingsRepository>(_store);
            builder.RegisterInstance<ISettingsApplier>(new SettingsApplier());
            _container = builder.Build();
        }

        [Test]
        public void InactiveViewsAreInjectedAndInitializationDoesNotReplaceAuthoredTabs()
        {
            var tabs = Child("Authored tabs");
            var button = new GameObject("Existing button", typeof(RectTransform), typeof(Button));
            button.transform.SetParent(tabs.transform);
            Set(_optionView, "categoryTabParent", tabs.GetComponent<RectTransform>());
            Build();
            Assert.That(_container.Resolve<ButtonController>(), Is.SameAs(_view));
            Assert.That(_view.TitlePanel.activeSelf, Is.True);
            Assert.That(_view.OptionPanel.activeSelf, Is.False);
            Assert.That(tabs.transform.childCount, Is.EqualTo(1));
            Assert.That(_optionView.ControllerSpeedSlider.value, Is.EqualTo(50f));
        }

        [Test]
        public void ReopeningOptionsStartsANewEditableSession()
        {
            Build();
            _view.OpenOptions();
            _optionView.MasterVolumeSlider.value = 0.3f;
            _view.CloseOptions();
            _view.OpenOptions();
            _optionView.MasterVolumeSlider.value = 0.7f;
            _view.CloseOptions();
            Assert.That(_store.Saves, Is.EqualTo(2));
            Assert.That(_store.Value.Audio.SystemVolume, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(_view.TitlePanel.activeSelf, Is.True);
        }

        [Test]
        public void RepeatedStartClicksIssueOnlyOneLoad()
        {
            Build();
            var presenter = _container.Resolve<TitlePresenter>();
            _view.StartGame();
            _view.StartGame();
            presenter.Tick(1f);
            _view.StartGame();
            presenter.Tick(1f);
            Assert.That(_navigation.Loads, Is.EqualTo(1));
            Assert.That(_navigation.LastAddress, Is.EqualTo("game-address"));
        }

        [Test]
        public void FailedLoadRestoresMenuAndAllowsRetry()
        {
            _navigation.Fail = true;
            Build();
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: test load failure"));
            _view.StartGame();
            _container.Resolve<TitlePresenter>().Tick(1f);
            Assert.That(_view.ScreenBackgroundImage.gameObject.activeSelf, Is.False);
            _navigation.Fail = false;
            _view.StartGame();
            _container.Resolve<TitlePresenter>().Tick(1f);
            Assert.That(_navigation.Loads, Is.EqualTo(2));
        }

        [Test]
        public void DisposingContainerCancelsPendingFadeAndRemovesButtonBindings()
        {
            Build();
            var presenter = _container.Resolve<TitlePresenter>();
            _view.StartGame();
            _container.Dispose();
            _container = null;
            presenter.Tick(10f);
            _view.QuitGame();
            _view.OpenOptions();
            Assert.That(_navigation.Loads, Is.Zero);
            Assert.That(_navigation.Quits, Is.Zero);
            Assert.That(_view.OptionPanel.activeSelf, Is.False);
        }

        [Test]
        public void ReinitializingPresenterDoesNotDuplicateSubscriptions()
        {
            Build();
            _container.Resolve<TitlePresenter>().Initialize();
            _view.QuitGame();
            Assert.That(_navigation.Quits, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AsyncLoadFailureAfterDisposalDoesNotTouchTheView()
        {
            Build();
            _view.StartGame();
            _container.Resolve<TitlePresenter>().Tick(1f);
            _container.Dispose();
            _container = null;
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: late load failure"));
            _navigation.Completion.SetException(new InvalidOperationException("late load failure"));
            yield return null;
            Assert.That(_view.ScreenBackgroundImage.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void CreditsCanBeSkippedDuringEndDelayAndReopened()
        {
            _data.delayBeforeStart = 0f;
            _data.delayAfterEnd = 30f;
            Build();
            var presenter = _container.Resolve<TitlePresenter>();
            for (var i = 0; i < 2; i++)
            {
                _view.StartCredits();
                presenter.Tick(1f);
                Assert.That(_view.CreditsPanel.activeSelf, Is.True);
                _view.SkipCredits();
                presenter.Tick(1f);
                Assert.That(_view.CreditsPanel.activeSelf, Is.False);
                Assert.That(_view.TitlePanel.activeSelf, Is.True);
            }
        }

        [Test]
        public void ConfigurationReportsMissingViewBeforeContainerConstruction()
        {
            Set(_scope, "titleView", null);
            var exception = Assert.Throws<InvalidOperationException>(() => _scope.ValidateConfiguration());
            Assert.That(exception.Message, Does.Contain("titleView"));
        }

        [Test]
        public void ProductionSettingsAdapterResolvesWithoutASoundScope()
        {
            var builder = new ContainerBuilder();
            typeof(TitleLifetimeScope).GetMethod("Configure", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_scope, new object[] { builder });
            // Suppress UI startup only: resolve the actual production adapter and its dependencies.
            using var settings = new SettingsUseCase(_store, new SettingsApplier());
            builder.RegisterInstance(settings);
            builder.RegisterInstance<ITitleNavigation>(_navigation);
            using var container = builder.Build();
            Assert.That(container.Resolve<ISettingsApplier>(), Is.TypeOf<UnitySettingsApplier>());
        }

        [Test]
        public void CreditTimingAccountsForStartAndEndDelaysAndCompletesOnce()
        {
            var playback = new CreditPlayback(new TitleScreenOptions("game", new CreditContent("", 10f, 2f, 3f), false, true, false, "", 0.5f));
            playback.Begin();
            Assert.That(playback.Tick(2.5f, 10f), Is.False);
            Assert.That(playback.Position, Is.EqualTo(5f));
            Assert.That(playback.Tick(3.4f, 10f), Is.False);
            Assert.That(playback.Tick(0.2f, 10f), Is.True);
            Assert.That(playback.Tick(100f, 10f), Is.False);
        }

        [Test]
        public void SettingsPropertiesSurviveSavingAndReloading()
        {
            var path = Path.Combine(Path.GetTempPath(), "shinzui-title-" + Guid.NewGuid() + ".json");
            try
            {
                var repository = new FileSettingsRepository(path);
                var settings = new GameSettings();
                settings.Audio.SystemVolume = 0.37f;
                settings.Camera.MouseNormalSensitivity = 74f;
                settings.Graphics.BrightnessValue = 0.61f;
                repository.Save(settings);
                var reloaded = repository.Load();
                Assert.That(reloaded.Audio.SystemVolume, Is.EqualTo(0.37f));
                Assert.That(reloaded.Camera.MouseNormalSensitivity, Is.EqualTo(74f));
                Assert.That(reloaded.Graphics.BrightnessValue, Is.EqualTo(0.61f));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [TestCase("{}")]
        [TestCase("{\"Audio\":null}")]
        public void LegacySettingsRetainDefaults(string json)
        {
            var path = Path.Combine(Path.GetTempPath(), "shinzui-title-" + Guid.NewGuid() + ".json");
            try
            {
                File.WriteAllText(path, json);
                var settings = new FileSettingsRepository(path).Load();
                Assert.That(settings.Audio.SystemVolume, Is.EqualTo(new GameSettings().Audio.SystemVolume));
                Assert.That(settings.Camera, Is.Not.Null);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        private GameObject Child(string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(_root.transform);
            return child;
        }

        private static void Set(UnityEngine.Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (value is bool boolean) property.boolValue = boolean;
            else if (value is string text) property.stringValue = text;
            else property.objectReferenceValue = (UnityEngine.Object)value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class Navigation : ITitleNavigation
        {
            public int Loads;
            public int Quits;
            public bool Fail;
            public string LastAddress;
            public readonly TaskCompletionSource<bool> Completion = new();
            public Task LoadGameAsync(string address)
            {
                Loads++;
                LastAddress = address;
                if (Fail) throw new InvalidOperationException("test load failure");
                return Completion.Task;
            }
            public Task LoadSceneAsync(string sceneName) => LoadGameAsync(sceneName);
            public void Quit() => Quits++;
        }

        private sealed class SettingsStore : ISettingsRepository
        {
            public GameSettings Value = new();
            public int Saves;
            public GameSettings Load() => Value.Clone();
            public void Save(GameSettings settings) { Value = settings.Clone(); Saves++; }
        }

        private sealed class SettingsApplier : ISettingsApplier
        {
            public void ApplyAudio(Shinzui.Domain.Settings.AudioSettings audio) { }
            public void ApplyGraphics(GraphicsSettings graphics) { }
            public void ApplyCamera(CameraSettings camera) { }
            public void ApplyControls(ControlsSettings controls) { }
            public void ApplyGameplay(GameplaySettings gameplay) { }
            public void ApplyLanguage(LanguageSettings language) { }
            public void ApplyAccessibility(AccessibilitySettings accessibility) { }
            public void ApplyAll(GameSettings settings) { }
        }
    }
}
