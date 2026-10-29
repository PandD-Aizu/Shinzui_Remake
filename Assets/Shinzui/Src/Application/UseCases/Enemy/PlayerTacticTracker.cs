using Shinzui.Application.DTOs.Enemy;
using Shinzui.Domain.Entities.Enemy;
using Shinzui.Domain.ValueObjects.Enemy;
using UnityEngine;

namespace Shinzui.Application.UseCases.Enemy
{
    /// <summary>全敵の観測を一つのランの学習へ集約</summary>
    public sealed class PlayerTacticTracker
    {
        private readonly BehaviorUnlockSettings _settings;
        private float _sneakTime;
        private readonly long[] _decoyEvents = new long[64];
        private int _decoyIndex;
        public BehaviorUnlockEntity State { get; }

        /// <summary>共有するルールを保持</summary>
        /// <param name="settings">学習条件</param>
        /// <param name="state">ラン内で共有する集計と解放状態</param>
        public PlayerTacticTracker(BehaviorUnlockSettings settings, BehaviorUnlockEntity state = null)
        { _settings = settings; State = state ?? new BehaviorUnlockEntity(); }

        /// <summary>敵自身が検出した戦術を記録</summary>
        /// <param name="kind">成立した戦術</param>
        public void Record(PlayerTacticKind kind) => State.Record(kind);

        /// <summary>複数の敵が同じ着弾へ反応しても一回だけ記録</summary>
        /// <param name="noise">調査を開始した着弾音</param>
        public void RecordDecoy(NoiseEvent noise)
        {
            if (noise.EventId > 0)
            {
                for (int i = 0; i < _decoyEvents.Length; i++)
                    if (_decoyEvents[i] == noise.EventId) return;
                _decoyEvents[_decoyIndex] = noise.EventId;
                _decoyIndex = (_decoyIndex + 1) % _decoyEvents.Length;
            }
            Record(PlayerTacticKind.DecoyThrow);
        }

        /// <summary>一フレームに一度だけ接近時間と解放間隔を更新</summary>
        /// <param name="deltaTime">経過秒数</param>
        /// <param name="crouching">しゃがみ中か</param>
        /// <param name="playerPosition">プレイヤーの位置</param>
        /// <param name="reports">全敵の観測結果</param>
        /// <param name="floor">ランの進行階層</param>
        public void Tick(float deltaTime, bool crouching, Vector3 playerPosition, EnemyReport[] reports, int floor)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            State.Tick(deltaTime);
            if (crouching)
                foreach (var report in reports)
                {
                    if (!report.CanReceiveCommand || report.IsBackstage || report.Awareness > EnemyAwarenessLevel.Suspicious
                        || Vector3.Distance(playerPosition, report.Position) > EnemyDirectorSettings.Positive(_settings.sneakDistance, 8f)) continue;
                    // 同時に複数の敵へ接近しても時間を重複加算しない
                    _sneakTime += deltaTime;
                    float duration = EnemyDirectorSettings.Positive(_settings.sneakDuration, 10f);
                    while (_sneakTime >= duration) { Record(PlayerTacticKind.SneakNearEnemy); _sneakTime -= duration; }
                    break;
                }

            if (_settings.rules != null)
                foreach (var rule in _settings.rules)
                    if (rule != null && State.Count(rule.tactic) >= Mathf.Max(1, rule.count) && Unlock(rule.behavior)) return;
            if (floor >= Mathf.Max(2, _settings.ambushFloor)) Unlock(EnemyBehaviorFlags.AmbushFromAbove);
        }

        /// <summary>解放を適用し開発時だけ記録</summary>
        /// <param name="flag">解放対象</param>
        /// <returns>解放したか</returns>
        private bool Unlock(EnemyBehaviorFlags flag)
        {
            if (!State.TryUnlock(flag, EnemyDirectorSettings.Positive(_settings.unlockCooldown, 60f))) return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"Enemy behavior unlocked: {flag}");
#endif
            return true;
        }
    }
}
