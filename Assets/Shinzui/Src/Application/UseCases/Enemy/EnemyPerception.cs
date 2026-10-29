using Shinzui.Application.DTOs.Enemy;
using UnityEngine;
using Shinzui.Domain.Entities.Enemy;

namespace Shinzui.Application.UseCases.Enemy
{
    /// <summary>位置を推測せず、知覚サンプルから疑いの増分を計算</summary>
    public sealed class EnemyPerception
    {
        private readonly EnemyBrainSettings _settings;
        private readonly SuspicionMeter _suspicion;

        /// <summary>敵一体の知覚状態を作成</summary>
        /// <param name="settings">知覚設定</param>
        public EnemyPerception(EnemyBrainSettings settings)
        {
            _settings = settings;
            _suspicion = new SuspicionMeter(EnemyDirectorSettings.Positive(settings.decayPerSecond, .12f),
                EnemyDirectorSettings.Positive(settings.alertedHoldTime, 8f));
        }

        /// <summary>視覚と聴覚から警戒段階を更新</summary>
        /// <param name="sight">視覚サンプル</param>
        /// <param name="signature">姿勢と光</param>
        /// <param name="maxDistance">最大視認距離</param>
        /// <param name="fieldOfView">視野角</param>
        /// <param name="noiseGain">聴覚刺激の合計</param>
        /// <param name="deltaTime">経過秒数</param>
        public void Update(SightSample sight, PlayerSignature signature, float maxDistance, float fieldOfView,
            float noiseGain, float deltaTime)
        {
            // 視覚だけが追跡確定を許可する
            float gain = SightGain(sight, signature, maxDistance, fieldOfView, _settings);
            _suspicion.AddSight(gain * deltaTime);
            if (sight.InRange && sight.VisibleRatio >= .34f && sight.AngleFromForward <= fieldOfView * .5f
                && sight.Distance <= EnemyDirectorSettings.Positive(_settings.instantDetectDistance, 2.5f))
                _suspicion.ForceEngage();

            _suspicion.AddNoise(noiseGain);
            _suspicion.Tick(deltaTime, gain > 0f || noiseGain > 0f);
        }

        /// <summary>Domainの状態をApplicationの報告へまとめる</summary>
        /// <param name="id">敵の識別子</param>
        /// <param name="position">敵の位置</param>
        /// <param name="command">現在の命令</param>
        /// <param name="distance">索敵距離</param>
        /// <param name="ready">行動可能か</param>
        /// <param name="visible">対象が見えているか</param>
        /// <param name="visibleToPlayer">プレイヤーから見えるか</param>
        /// <param name="noiseGain">最大の音刺激</param>
        /// <param name="heardPosition">音源位置</param>
        /// <returns>知覚状態を含む報告</returns>
        public EnemyReport CreateReport(int id, Vector3 position, EnemyCommand command, float distance,
            bool ready, bool visible, bool visibleToPlayer, float noiseGain, Vector3 heardPosition)
            => new(id, position, command, distance, ready, visible, visibleToPlayer, _suspicion.Level, noiseGain, heardPosition);

        /// <summary>距離、視野、姿勢、光から毎秒の視覚刺激を計算</summary>
        /// <param name="sight">視覚サンプル</param>
        /// <param name="signature">姿勢と光</param>
        /// <param name="maxDistance">最大視認距離</param>
        /// <param name="fieldOfView">視野角</param>
        /// <param name="settings">知覚設定</param>
        /// <returns>毎秒の疑い増分</returns>
        public static float SightGain(SightSample sight, PlayerSignature signature, float maxDistance,
            float fieldOfView, EnemyBrainSettings settings)
        {
            if (!sight.InRange || sight.VisibleRatio <= 0f || sight.AngleFromForward > fieldOfView * .5f) return 0f;

            // 近距離と中心視を基準に、遠距離と周辺視を減衰する
            float distance = sight.Distance <= 4f ? 1f : Mathf.Clamp01((maxDistance - sight.Distance) / Mathf.Max(.01f, maxDistance - 4f));
            float angle = sight.AngleFromForward <= 30f ? 1f : .35f;
            float posture = signature.IsCrouching ? (signature.IsMoving ? .55f : .35f) : signature.IsRunning ? 1.5f : 1f;
            float light = signature.FlashlightOn ? (signature.IlluminatesEnemy ? 2.2f : 1.6f) : 1f;
            return EnemyDirectorSettings.Positive(settings.baseSightRate, 2.5f) * distance * angle * sight.VisibleRatio * posture * light;
        }

        /// <summary>遮蔽による可聴距離の短縮を反映</summary>
        /// <param name="noise">音の刺激</param>
        /// <param name="distance">音源までの距離</param>
        /// <param name="hasLine">遮蔽がないか</param>
        /// <param name="settings">知覚設定</param>
        /// <returns>疑いの増分</returns>
        public static float NoiseGain(NoiseEvent noise, float distance, bool hasLine, EnemyBrainSettings settings)
        {
            float radius = noise.Radius * (hasLine ? 1f : .6f);
            return radius > 0f ? EnemyDirectorSettings.Positive(settings.noiseBase, .6f) * Mathf.Clamp01(1f - distance / radius) : 0f;
        }
    }
}
