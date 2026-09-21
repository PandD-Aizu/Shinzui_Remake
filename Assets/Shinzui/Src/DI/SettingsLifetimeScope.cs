using VContainer;
using VContainer.Unity;
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
            SettingsRegistration.RegisterServices(builder);

            // --- ビュー層の登録 ---
            if (optionWindowView != null)
            {
                builder.RegisterComponent(optionWindowView);
            }
            else
            {
                // VContainer searches this scene, including inactive UI, and reports a
                // missing registration instead of selecting a view from another scene.
                builder.RegisterComponentInHierarchy<OptionWindowView>();
            }

            // --- プレゼンテーション層の登録 ---
            builder.RegisterEntryPoint<OptionPresenter>(Lifetime.Scoped);
        }
    }
}
