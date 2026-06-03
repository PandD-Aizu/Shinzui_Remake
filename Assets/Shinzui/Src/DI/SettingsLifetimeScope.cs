using VContainer;
using VContainer.Unity;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases;
using Shinzui.Infrastructure.Repositories;
using Shinzui.Infrastructure.Services;
using Shinzui.Presentation.Settings;
using Shinzui.View.Settings;
using UnityEngine;

namespace Shinzui.DI
{
    /// <summary>
    /// オプション設定システム関連の依存関係注入を管理するVContainerのLifetimeScope
    /// RootLifetimeScopeまたは個別のシーンスコープに登録して使用する
    /// </summary>
    public class SettingsLifetimeScope : LifetimeScope
    {
        [Header("UI Views")]
        [SerializeField] private OptionWindowView optionWindowView;

        protected override void Configure(IContainerBuilder builder)
        {
            // --- インフラ層の登録 ---
            builder.Register<FileSettingsRepository>(Lifetime.Singleton).As<ISettingsRepository>();
            builder.Register<UnitySettingsApplier>(Lifetime.Singleton).As<ISettingsApplier>();

            // --- アプリケーション層の登録 ---
            builder.Register<SettingsUseCase>(Lifetime.Singleton);

            // --- ビュー層の登録 ---
            var viewInstance = optionWindowView;
            if (viewInstance == null)
            {
                viewInstance = FindFirstObjectByType<OptionWindowView>();
            }

            if (viewInstance != null)
            {
                builder.RegisterComponent(viewInstance);
            }
            else
            {
                Debug.LogWarning("[Settings] OptionWindowView instance was not assigned and not found in the scene hierarchy.");
            }

            // --- プレゼンテーション層の登録 ---
            builder.RegisterEntryPoint<OptionPresenter>();
        }
    }
}
