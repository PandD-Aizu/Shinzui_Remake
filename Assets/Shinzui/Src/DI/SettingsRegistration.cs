using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases;
using Shinzui.Infrastructure.Repositories;
using Shinzui.Infrastructure.Services;
using VContainer;

namespace Shinzui.DI
{
    internal static class SettingsRegistration
    {
        public static void RegisterServices(IContainerBuilder builder)
        {
            builder.Register(_ => new FileSettingsRepository(), Lifetime.Scoped).As<ISettingsRepository>();
            // VContainer resolves every constructor parameter, including optional ones.
            // Honor the applier's FMOD fallback when this scene has no sound scope.
            builder.Register(resolver => new UnitySettingsApplier(
                    resolver.TryResolve<IFMODVCAService>(out var audio) ? audio : null), Lifetime.Scoped)
                .As<ISettingsApplier>();
            builder.Register<SettingsUseCase>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();
        }
    }
}
