using UnityEngine;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>自身の状態と知覚だけを含み、プレイヤーの実座標を受け取らない入力</summary>
    public readonly struct EnemyBrainInput
    {
        public Vector3 EnemyPosition { get; }
        public SightSample Sight { get; }
        public PlayerSignature PlayerSignature { get; }
        public float NoiseGain { get; }
        public NoiseEvent HeardNoise { get; }
        public bool IsReady { get; }
        public bool IsStunned { get; }

        /// <summary>物理層で採取した知覚をまとめる</summary>
        /// <param name="enemyPosition">敵自身の位置</param>
        /// <param name="sight">視認時だけ座標が入るサンプル</param>
        /// <param name="playerSignature">姿勢と光の状態</param>
        /// <param name="noiseGain">聞こえた刺激の合計</param>
        /// <param name="heardNoise">聞こえた中で最も強い音</param>
        /// <param name="isReady">行動可能か</param>
        /// <param name="isStunned">ストロボで停止中か</param>
        public EnemyBrainInput(Vector3 enemyPosition, SightSample sight, PlayerSignature playerSignature,
            float noiseGain = 0f, NoiseEvent heardNoise = default, bool isReady = true, bool isStunned = false)
        {
            EnemyPosition = enemyPosition;
            Sight = sight;
            PlayerSignature = playerSignature;
            NoiseGain = noiseGain;
            HeardNoise = noiseGain > 0f ? heardNoise : default;
            IsReady = isReady;
            IsStunned = isStunned;
        }
    }
}
