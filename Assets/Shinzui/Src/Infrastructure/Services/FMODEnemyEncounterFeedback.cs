using Shinzui.Application.Interfaces;
using Shinzui.Domain.ValueObjects.FMOD;

namespace Shinzui.Infrastructure.Services
{
    public sealed class FMODEnemyEncounterFeedback : IEnemyEncounterFeedback
    {
        private readonly IFMODSEService _sound;
        public FMODEnemyEncounterFeedback(IFMODSEService sound) => _sound = sound;
        public void OnPlayerFound() => _sound.PlayOneShot(FMODEventPath.SE_ON_ENEMY_FOUND.Reference);
    }
}
