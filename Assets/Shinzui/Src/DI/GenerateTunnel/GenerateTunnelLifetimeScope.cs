using Shinzui.Application.Interfaces.Tunnel;
using Shinzui.Application.UseCases.Tunnel;
using Shinzui.Domain.DomainServices.Tunnel;
using Shinzui.Presentation.GenerateTunnel;
using Shinzui.View.GenerateTunnel;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Shinzui.DI.GenerateTunnel
{
    /// <summary>
    /// トンネルランダム生成システム関連の依存関係注入を管理するVContainerのLifetimeScope。
    /// ドメイン・アプリケーション・プレゼンテーション・ビューの全層をClean Architectureに沿って結合する。
    /// </summary>
    public sealed class GenerateTunnelLifetimeScope : LifetimeScope
    {
        [Header("Views")]
        [SerializeField] private GenerateTunnelTestBootstrap bootstrapView;
        [SerializeField] private TunnelMapView mapView;

        protected override void Configure(IContainerBuilder builder)
        {
            // ドメイン層の登録
            builder.Register<TunnelLayoutGenerator>(Lifetime.Singleton).As<ITunnelLayoutGenerator>();

            // アプリケーション層の登録
            builder.Register<GenerateTunnelUseCase>(Lifetime.Singleton).As<IGenerateTunnelUseCase>();

            // ビュー層の登録
            var resolvedMapView = mapView;
            if (resolvedMapView == null)
            {
                resolvedMapView = FindFirstObjectByType<TunnelMapView>();
            }
            if (resolvedMapView != null)
            {
                builder.RegisterComponent(resolvedMapView);
            }

            var resolvedBootstrap = bootstrapView;
            if (resolvedBootstrap == null)
            {
                resolvedBootstrap = FindFirstObjectByType<GenerateTunnelTestBootstrap>();
            }
            if (resolvedBootstrap != null)
            {
                builder.RegisterComponent(resolvedBootstrap);
            }

            // プレゼンテーション層の登録
            builder.Register<GenerateTunnelPresenter>(Lifetime.Singleton);
        }
    }
}
