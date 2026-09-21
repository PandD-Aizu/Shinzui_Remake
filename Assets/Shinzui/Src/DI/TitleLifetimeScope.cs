using System;
using Shinzui.Application.Title;
using Shinzui.Infrastructure.Title;
using Shinzui.Presentation.Settings;
using Shinzui.Presentation.Title;
using Shinzui.Src.Title;
using Shinzui.View.Settings;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Shinzui.DI
{
    /// <summary>Single composition root for the title, credits and options in this scene.</summary>
    public sealed class TitleLifetimeScope : LifetimeScope
    {
        [Header("Scene views (including inactive panels)")]
        [SerializeField] private ButtonController titleView;
        [SerializeField] private CreditScreenView creditView;
        [SerializeField] private OptionWindowView optionView;
        [Header("Navigation")]
        [SerializeField] private string gameSceneAddress;
        [Header("Credits")]
        [SerializeField] private CreditData creditData;
        [SerializeField] private bool loopCredits;
        [SerializeField] private bool allowSkip = true;
        [SerializeField] private bool autoStartCredits;
        [SerializeField] private string nextSceneName;
        [SerializeField, Min(0f)] private float creditsFadeOutDuration = 0.5f;

        protected override void Configure(IContainerBuilder builder)
        {
            ValidateConfiguration();
            builder.RegisterComponent(titleView);
            builder.RegisterComponent(creditView);
            builder.RegisterComponent(optionView);
            builder.RegisterInstance(new TitleScreenOptions(gameSceneAddress,
                creditData.CreateContent(creditView.SpaceBetweenSections), loopCredits, allowSkip,
                autoStartCredits, nextSceneName, creditsFadeOutDuration));
            builder.Register<UnityTitleNavigation>(Lifetime.Scoped).As<ITitleNavigation>();
            builder.Register<CreditPlayback>(Lifetime.Scoped);
            SettingsRegistration.RegisterServices(builder);
            builder.RegisterEntryPoint<OptionPresenter>(Lifetime.Scoped);
            builder.RegisterEntryPoint<TitlePresenter>(Lifetime.Scoped).AsSelf();
        }

        public void ValidateConfiguration()
        {
            Require(titleView, nameof(titleView));
            Require(creditView, nameof(creditView));
            Require(optionView, nameof(optionView));
            Require(creditData, nameof(creditData));
            Require(titleView.TitlePanel, "titleView.TitlePanel");
            Require(titleView.OptionPanel, "titleView.OptionPanel");
            Require(titleView.CreditsPanel, "titleView.CreditsPanel");
            Require(titleView.ScreenBackgroundImage, "titleView.ScreenBackgroundImage");
            if (string.IsNullOrWhiteSpace(gameSceneAddress))
                throw new InvalidOperationException($"[{nameof(TitleLifetimeScope)}] Assign gameSceneAddress on '{name}'.");
            if (titleView.gameObject.scene != gameObject.scene || creditView.gameObject.scene != gameObject.scene ||
                optionView.gameObject.scene != gameObject.scene)
                throw new InvalidOperationException("Title views must belong to the same scene as their LifetimeScope.");
        }

        private void Require(UnityEngine.Object reference, string field)
        {
            if (reference == null)
                throw new InvalidOperationException($"[{nameof(TitleLifetimeScope)}] Assign {field} on '{name}'.");
        }
    }
}
