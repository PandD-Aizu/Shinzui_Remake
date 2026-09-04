using VContainer;
using VContainer.Unity;
using Shinzui.Infrastructure.SaveData;

namespace Shinzui.DI
{
    public class RootLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            //ゲームのSave&Load管理
            builder.Register<SaveManager>(Lifetime.Singleton);
        }
    }
}