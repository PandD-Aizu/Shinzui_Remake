using System;
using Shinzui.Application.Title;
using Shinzui.Application.UseCases;
using Shinzui.Src.Title;
using Shinzui.View.Settings;
using UnityEngine;
using VContainer.Unity;

namespace Shinzui.Presentation.Title
{
    /// <summary>Scene-scoped menu binding and animation, owned and disposed by VContainer.</summary>
    public sealed class TitlePresenter : IInitializable, ITickable, IDisposable
    {
        private static readonly string[] CategoryIds =
            { "Controls", "Camera", "GameSettings", "Graphics", "Audio", "Language", "Accessibility" };

        private readonly ButtonController _view;
        private readonly CreditScreenView _creditView;
        private readonly OptionWindowView _optionView;
        private readonly SettingsUseCase _settings;
        private readonly CreditPlayback _credits;
        private readonly TitleScreenOptions _options;
        private readonly ITitleNavigation _navigation;
        private CanvasGroup _creditsGroup;
        private bool _initialized;
        private bool _disposed;
        private bool _loading;
        private bool _loadStarted;
        private bool _showingCredits;
        private bool _closingCredits;
        private float _loadElapsed;
        private float _creditsElapsed;
        private float _closeStartAlpha;

        public TitlePresenter(ButtonController view, CreditScreenView creditView, OptionWindowView optionView,
            SettingsUseCase settings, CreditPlayback credits, TitleScreenOptions options, ITitleNavigation navigation)
        {
            _view = view;
            _creditView = creditView;
            _optionView = optionView;
            _settings = settings;
            _credits = credits;
            _options = options;
            _navigation = navigation;
        }

        public void Initialize()
        {
            if (_initialized || _disposed) return;
            _initialized = true;
            _creditsGroup = _view.CreditsPanel.GetComponent<CanvasGroup>();
            if (_creditsGroup == null) _creditsGroup = _view.CreditsPanel.AddComponent<CanvasGroup>();
            _view.StartGameRequested += StartGame;
            _view.CreditsRequested += OpenCredits;
            _view.OpenOptionsRequested += OpenOptions;
            _view.CloseOptionsRequested += CloseOptions;
            _view.OptionSelected += SelectOption;
            _view.QuitRequested += Quit;
            _view.SkipRequested += Skip;
            _view.ScreenBackgroundImage.gameObject.SetActive(false);
            _view.OptionPanel.SetActive(false);
            _view.CreditsPanel.SetActive(false);
            if (_view.CreditsSkipHintCanvasGroup != null)
                _view.CreditsSkipHintCanvasGroup.gameObject.SetActive(false);
            _view.TitlePanel.SetActive(true);
            SelectOption(0);
            if (_options.AutoStartCredits) OpenCredits();
        }

        public void Tick() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            if (!_initialized || _disposed) return;
            deltaTime = Mathf.Max(0f, deltaTime);
            if (_loading && !_loadStarted)
            {
                _loadElapsed += deltaTime;
                var alpha = Progress(_loadElapsed, _view.FadeDuration);
                SetScreenAlpha(alpha);
                if (alpha >= 1f) LoadScene(true, _options.GameSceneAddress);
            }
            if (!_showingCredits) return;

            _creditsElapsed += deltaTime;
            if (_closingCredits)
            {
                var progress = Progress(_creditsElapsed, _options.CreditsFadeOutDuration);
                SetCreditsAlpha(_closeStartAlpha * (1f - progress));
                if (progress >= 1f) ReturnFromCredits();
                return;
            }

            SetCreditsAlpha(Progress(_creditsElapsed, _view.CreditsFadeDuration));
            var maxScroll = _creditView.GetMaxScrollPosition();
            var finished = _credits.Tick(deltaTime, maxScroll);
            _creditView.UpdateScrollPosition(maxScroll > 0f ? _credits.Position / maxScroll : 1f);
            if (finished) FinishCredits(false);
        }

        private void StartGame()
        {
            if (_disposed || _loading || _showingCredits || _view.OptionPanel.activeSelf) return;
            _loading = true;
            _loadStarted = false;
            _loadElapsed = 0f;
            SetScreenAlpha(0f);
            _view.ScreenBackgroundImage.gameObject.SetActive(true);
        }

        // Event callback: observe every async failure here, including synchronous adapter failures.
        private async void LoadScene(bool addressable, string destination)
        {
            _loading = true;
            _loadStarted = true;
            try
            {
                if (addressable) await _navigation.LoadGameAsync(destination);
                else await _navigation.LoadSceneAsync(destination);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (_disposed) return;
                _loading = false;
                _loadStarted = false;
                _view.ScreenBackgroundImage.gameObject.SetActive(false);
                if (_view.CreditsPanel.activeSelf) ReturnFromCredits();
                _view.TitlePanel.SetActive(true);
            }
        }

        private void OpenOptions()
        {
            if (_disposed || _loading || _showingCredits || _view.OptionPanel.activeSelf) return;
            _settings.BeginEdit();
            _optionView.gameObject.SetActive(true);
            _view.OptionPanel.SetActive(true);
            _view.TitlePanel.SetActive(false);
            SelectOption(0);
        }

        private void CloseOptions()
        {
            if (_disposed || _loading || !_view.OptionPanel.activeSelf) return;
            _settings.SaveAndApply();
            _view.OptionPanel.SetActive(false);
            _view.TitlePanel.SetActive(true);
        }

        private void SelectOption(int index)
        {
            if (_disposed || index < 0 || index >= CategoryIds.Length) return;
            _optionView.ShowCategoryPanel(CategoryIds[index]);
            for (var i = 0; i < CategoryIds.Length; i++)
            {
                var label = _view.GetOptionLabel(i);
                if (label != null) label.color = i == index ? Color.red : Color.white;
            }
        }

        private void OpenCredits()
        {
            if (_disposed || _loading || _showingCredits || _view.OptionPanel.activeSelf) return;
            _showingCredits = true;
            _closingCredits = false;
            _creditsElapsed = 0f;
            _view.TitlePanel.SetActive(false);
            _view.CreditsPanel.SetActive(true);
            _creditsGroup.interactable = false;
            _creditsGroup.blocksRaycasts = true;
            if (_view.CreditsSkipHintCanvasGroup != null)
            {
                var hint = _view.CreditsSkipHintCanvasGroup;
                hint.gameObject.SetActive(_options.AllowSkip);
                hint.interactable = false;
                hint.blocksRaycasts = false;
            }
            SetCreditsAlpha(0f);
            _creditView.SetCreditText(_options.Credits.Text);
            _creditView.ResetScrollPosition();
            _creditView.EnableInteraction(false);
            _credits.Begin();
        }

        private void Skip()
        {
            if (_disposed || _loading) return;
            if (_showingCredits && !_closingCredits && _options.AllowSkip) FinishCredits(true);
            else if (_view.OptionPanel.activeSelf) CloseOptions();
        }

        private void FinishCredits(bool skipped)
        {
            _credits.Stop();
            var hasNextScene = !string.IsNullOrWhiteSpace(_options.NextSceneName);
            if (_options.LoopCredits && (!skipped || !hasNextScene))
            {
                _creditView.ResetScrollPosition();
                _credits.Begin();
            }
            else if (hasNextScene)
            {
                _showingCredits = false;
                LoadScene(false, _options.NextSceneName);
            }
            else
            {
                _closingCredits = true;
                _closeStartAlpha = _creditsGroup.alpha;
                _creditsElapsed = 0f;
            }
        }

        private void ReturnFromCredits()
        {
            _credits.Stop();
            _showingCredits = false;
            _closingCredits = false;
            _view.CreditsPanel.SetActive(false);
            if (_view.CreditsSkipHintCanvasGroup != null)
                _view.CreditsSkipHintCanvasGroup.gameObject.SetActive(false);
            _view.TitlePanel.SetActive(true);
        }

        private void Quit()
        {
            if (!_disposed && !_loading && !_showingCredits) _navigation.Quit();
        }

        private static float Progress(float elapsed, float duration) =>
            duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);

        private void SetScreenAlpha(float alpha)
        {
            var color = _view.ScreenBackgroundImage.color;
            color.a = alpha;
            _view.ScreenBackgroundImage.color = color;
        }

        private void SetCreditsAlpha(float alpha)
        {
            _creditsGroup.alpha = alpha;
            if (_view.CreditsSkipHintCanvasGroup != null) _view.CreditsSkipHintCanvasGroup.alpha = alpha;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _credits.Stop();
            _view.StartGameRequested -= StartGame;
            _view.CreditsRequested -= OpenCredits;
            _view.OpenOptionsRequested -= OpenOptions;
            _view.CloseOptionsRequested -= CloseOptions;
            _view.OptionSelected -= SelectOption;
            _view.QuitRequested -= Quit;
            _view.SkipRequested -= Skip;
        }
    }
}
