using System;
using System.Collections.Generic;
using R3;
using Shinzui.Application.UseCases;
using Shinzui.Domain.Settings;
using Shinzui.View.Settings;
using Shinzui.View.UI;

namespace Shinzui.Presentation.Settings
{
    /// <summary>グラフィック設定画面と編集中の設定を双方向に接続する</summary>
    public sealed class GraphicsOptionsBinder : IDisposable
    {
        private readonly SettingsUseCase _useCase;
        private readonly GraphicsOptionsView _view;
        private readonly CompositeDisposable _disposables = new();
        private readonly CompositeDisposable _upscalingDisposables = new();
        private readonly List<Action> _refreshValues = new();

        /// <summary>全描画項目と対応方式の変更を購読する</summary>
        /// <param name="useCase">編集中の設定</param>
        /// <param name="view">接続する画面</param>
        public GraphicsOptionsBinder(SettingsUseCase useCase, GraphicsOptionsView view)
        {
            _useCase = useCase;
            _view = view;

            // 選択肢を用意してから値と利用者の操作を接続する
            _view.SetResolutions(_useCase.AvailableResolutions);
            Bind(_view.Preset, _useCase.GraphicsPreset,
                value => value == GraphicsPreset.Custom ? -1 : (int)value,
                index => _useCase.SelectGraphicsPreset((GraphicsPreset)index));
            Bind(_view.ScreenMode, _useCase.ScreenMode);
            Bind(_view.Resolution, _useCase.ResolutionIndex);
            Bind(_view.FrameRateLimit, _useCase.FrameRateLimit,
                value => Array.IndexOf(GraphicsOptionsView.FrameRateValues, value),
                index => _useCase.FrameRateLimit.Value = GraphicsOptionsView.FrameRateValues[index]);
            Bind(_view.AntiAliasing, _useCase.AntiAliasing);
            Bind(_view.TextureQuality, _useCase.TextureQuality);
            Bind(_view.TextureFiltering, _useCase.TextureFiltering);
            Bind(_view.MeshQuality, _useCase.MeshQuality);
            Bind(_view.GiAndReflection, _useCase.GiAndReflectionQuality);
            Bind(_view.VolumeLight, _useCase.VolumeLightQuality);
            Bind(_view.ShadowQuality, _useCase.ShadowQuality);
            Bind(_view.VSync, _useCase.EnableVSync);
            Bind(_view.RayTracing, _useCase.EnableRayTracing);
            Bind(_view.Ssr, _useCase.EnableSsr);
            Bind(_view.Ao, _useCase.EnableAo);
            Bind(_view.Sss, _useCase.EnableSss);
            Bind(_view.ContactShadow, _useCase.EnableContactShadow);
            Bind(_view.Bloom, _useCase.EnableBloom);
            Bind(_view.LensFlare, _useCase.EnableLensFlare);
            Bind(_view.FilmGrain, _useCase.EnableFilmGrain);
            Bind(_view.DepthOfField, _useCase.EnableDepthOfField);
            Bind(_view.LensDistortion, _useCase.EnableLensDistortion);

            // 描画環境の初期化後も対応方式を毎フレーム確認する
            Refresh();
            Observable.EveryUpdate().Select(_ => _useCase.SupportedUpscaler).DistinctUntilChanged()
                .Subscribe(BindUpscaling).AddTo(_disposables);
        }

        /// <summary>選択肢と対応状態と現在の編集値を再表示する</summary>
        public void Refresh()
        {
            // 再度開いた画面にも現在の環境と編集値を反映する
            _view.SetResolutions(_useCase.AvailableResolutions);
            _view.SetRayTracingSupported(_useCase.IsRayTracingSupported);
            foreach (var refresh in _refreshValues) refresh();
            BindUpscaling(_useCase.SupportedUpscaler);
        }

        /// <summary>整数の設定値を同じ添字の選択肢へ接続する</summary>
        /// <param name="selector">操作する選択肢</param>
        /// <param name="property">接続する設定値</param>
        private void Bind(RequiemOptionSelector selector, ReactiveProperty<int> property)
        {
            // 整数値を選択位置として扱う
            Bind(selector, property, value => value, index => property.Value = index);
        }

        /// <summary>真偽値をオフとオンの選択肢へ接続する</summary>
        /// <param name="selector">操作する選択肢</param>
        /// <param name="property">接続する設定値</param>
        private void Bind(RequiemOptionSelector selector, ReactiveProperty<bool> property)
        {
            // オフを0、オンを1に対応させる
            Bind(selector, property, value => value ? 1 : 0, index => property.Value = index == 1);
        }

        /// <summary>設定値の表示と利用者の操作を分離して接続する</summary>
        /// <typeparam name="T">設定値の型</typeparam>
        /// <param name="selector">操作する選択肢</param>
        /// <param name="property">購読する設定値</param>
        /// <param name="toIndex">設定値から選択位置への変換</param>
        /// <param name="write">利用者の操作を設定へ反映する処理</param>
        private void Bind<T>(RequiemOptionSelector selector, ReactiveProperty<T> property,
            Func<T, int> toIndex, Action<int> write)
        {
            // 初回と編集値の変更を通知なしで表示する
            property.Subscribe(value => selector.SetIndexWithoutNotify(toIndex(value))).AddTo(_disposables);
            _refreshValues.Add(() => selector.SetIndexWithoutNotify(toIndex(property.Value)));

            // 利用者の操作だけを設定に書き戻す
            Observable.FromEvent<int>(h => selector.ValueChanged += h, h => selector.ValueChanged -= h)
                .Subscribe(write).AddTo(_disposables);
        }

        /// <summary>対応するアップスケーラーの設定へ購読先を切り替える</summary>
        /// <param name="upscaler">現在利用できる方式</param>
        private void BindUpscaling(UpscalerType upscaler)
        {
            // 旧方式の購読と操作を解除して対応方式を表示する
            _upscalingDisposables.Clear();
            _view.SetUpscaler(upscaler == UpscalerType.None ? null :
                upscaler == UpscalerType.Fsr2 ? "FSR 2" : "DLSS");
            var enabled = _useCase.UpscalingEnabled;
            var quality = _useCase.UpscalingQuality;
            enabled.Subscribe(value => _view.Upscaling.SetIndexWithoutNotify(value ? quality.Value + 1 : 0))
                .AddTo(_upscalingDisposables);
            quality.Subscribe(value => _view.Upscaling.SetIndexWithoutNotify(enabled.Value ? value + 1 : 0))
                .AddTo(_upscalingDisposables);
            if (upscaler == UpscalerType.None) return;

            // オフでは品質を保持し、有効化時は指定した品質を先に反映する
            Observable.FromEvent<int>(h => _view.Upscaling.ValueChanged += h, h => _view.Upscaling.ValueChanged -= h)
                .Subscribe(index =>
                {
                    if (index > 0) quality.Value = index - 1;
                    enabled.Value = index > 0;
                }).AddTo(_upscalingDisposables);
        }

        /// <summary>設定の購読と画面の操作イベントを解放する</summary>
        public void Dispose()
        {
            // 毎フレームの監視と全選択肢の購読を停止する
            _disposables.Dispose();
            _upscalingDisposables.Dispose();
            _refreshValues.Clear();
        }
    }
}
