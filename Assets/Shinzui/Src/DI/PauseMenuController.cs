using System;
using Shinzui.Application.UseCases;
using Shinzui.Presentation.Settings;
using Shinzui.Infrastructure.Repositories;
using Shinzui.Infrastructure.Services;
using Shinzui.Infrastructure.Title;
using Shinzui.View.Settings;
using UnityEngine;

namespace Shinzui.DI
{
    /// <summary>ポーズ画面を既存の設定保存とタイトル遷移へ接続する</summary>
    public sealed class PauseMenuController : MonoBehaviour
    {
        private PauseMenuView _view;
        private SettingsUseCase _settings;
        private GraphicsOptionsBinder _graphicsBinder;

        /// <summary>ポーズ画面を生成して各操作を接続する</summary>
        private void Start()
        {
            _view = Instantiate(Resources.Load<PauseMenuView>("PauseMenu"), transform);
            _view.OptionsOpened += OpenOptions;
            _view.OptionsClosed += SaveOptions;
            _view.TitleRequested += ReturnToTitle;
        }

        /// <summary>保存済み設定を読み込み、操作と設定値を接続する</summary>
        private void OpenOptions()
        {
            if (_settings == null)
            {
                _settings = new SettingsUseCase(new FileSettingsRepository(), new UnitySettingsApplier());
                _view.Sliders[0].onValueChanged.AddListener(value => _settings.MasterVolume.Value = value);
                _view.Sliders[1].onValueChanged.AddListener(value => _settings.BgmVolume.Value = value);
                _view.Sliders[2].onValueChanged.AddListener(value => _settings.SeVolume.Value = value);
                _view.Sliders[3].onValueChanged.AddListener(value => _settings.VoiceVolume.Value = value);
                _view.Sliders[4].onValueChanged.AddListener(value => _settings.ControllerNormalSpeed.Value = value);
                _view.Sliders[5].onValueChanged.AddListener(value => _settings.MouseNormalSensitivity.Value = value);
                _view.Sliders[6].onValueChanged.AddListener(value => _settings.Brightness.Value = value);
                _view.CenterDot.onValueChanged.AddListener(value => _settings.ShowCenterDot.Value = value);
            }

            // 再度開いた場合も確定済みの設定から編集を開始する
            _settings.BeginEdit();
            float[] values = { _settings.MasterVolume.Value, _settings.BgmVolume.Value,
                _settings.SeVolume.Value, _settings.VoiceVolume.Value, _settings.ControllerNormalSpeed.Value,
                _settings.MouseNormalSensitivity.Value, _settings.Brightness.Value };
            for (int i = 0; i < values.Length; i++) _view.Sliders[i].SetValueWithoutNotify(values[i]);
            _view.CenterDot.SetIsOnWithoutNotify(_settings.ShowCenterDot.Value);

            // グラフィック画面も同じ編集セッションへ接続する
            if (_graphicsBinder == null)
            {
                _graphicsBinder = new GraphicsOptionsBinder(_settings, _view.Graphics);
                _view.GraphicsOpened += _graphicsBinder.Refresh;
            }
            else _graphicsBinder.Refresh();
        }

        /// <summary>オプションの編集結果を保存する</summary>
        private void SaveOptions()
        {
            _settings?.SaveAndApply();
        }

        /// <summary>二重押下を防いでAddressablesのタイトルへ遷移する</summary>
        private async void ReturnToTitle()
        {
            if (_view.IsBusy) return;
            _view.IsBusy = true;
            try
            {
                await new UnityTitleNavigation().LoadGameAsync("Title");
            }
            catch (Exception exception)
            {
                // 読み込み失敗時はメニューを維持して再操作を許可する
                if (_view != null) _view.IsBusy = false;
                Debug.LogException(exception);
            }
        }

        /// <summary>シーン破棄時に設定の購読を解放する</summary>
        private void OnDestroy()
        {
            // 画面の購読を設定本体より先に解放する
            if (_view != null && _graphicsBinder != null) _view.GraphicsOpened -= _graphicsBinder.Refresh;
            _graphicsBinder?.Dispose();
            _settings?.Dispose();
        }
    }
}
