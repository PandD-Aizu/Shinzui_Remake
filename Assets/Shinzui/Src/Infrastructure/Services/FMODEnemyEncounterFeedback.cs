using Shinzui.Application.Interfaces;
using Shinzui.Domain.ValueObjects.FMOD;
using UnityEngine;

namespace Shinzui.Infrastructure.Services
{
    public sealed class FMODEnemyEncounterFeedback : IEnemyEncounterFeedback
    {
        private readonly IFMODSEService _sound;
        public FMODEnemyEncounterFeedback(IFMODSEService sound) => _sound = sound;
        /// <summary>専用FMODイベント作成後に舞台裏移動音を接続</summary>
        /// <param name="position">抽象位置</param>
        public void OnEnemyBackstageMove(Vector3 position) { }
        /// <summary>専用FMODイベント作成後に出現音を接続</summary>
        /// <param name="position">出現位置</param>
        public void OnEnemyEmerge(Vector3 position) { }
        public void OnPlayerFound() => _sound.PlayOneShot(FMODEventPath.SE_ON_ENEMY_FOUND.Reference);
    }
}
