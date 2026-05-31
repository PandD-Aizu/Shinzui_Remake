using Shinzui.Infrastructure.Repositories;
using Shinzui.Infrastructure.Services;
using VContainer;
using VContainer.Unity;

namespace Shinzui.DI
{
    public class SoundSystemLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // 設定保存のためのレポジトリ
            builder.Register<FMODSettingsRepository>(Lifetime.Singleton)
                .AsImplementedInterfaces();
            
            // BGM再生サービス
            builder.Register<FMODBGMService>(Lifetime.Singleton)
                .AsImplementedInterfaces();
            
            // SE再生サービス
            builder.Register<FMODSEService>(Lifetime.Singleton)
                .AsImplementedInterfaces();
            
            // VCAコントロールサービス
            builder.Register<FMODVCAService>(Lifetime.Singleton)
                .AsImplementedInterfaces();
        }
    }
}