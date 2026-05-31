using Shinzui.Infrastructure.Services;
using VContainer;
using VContainer.Unity;

namespace Shinzui.DI
{
    public class JsonSystemLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Jsonファイル作成
            builder.Register<JsonCreationService>(Lifetime.Singleton)
                .AsImplementedInterfaces();
            
            // Jsonシリアライズ・デシリアライズ
            builder.Register<JsonUtilityService>(Lifetime.Singleton)
                .AsImplementedInterfaces();
        }
    }
}