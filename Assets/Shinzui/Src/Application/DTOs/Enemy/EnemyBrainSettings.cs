using System;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>知覚の調整値</summary>
    [Serializable]
    public sealed class EnemyBrainSettings
    {
        public float baseSightRate = 2.5f;
        public float instantDetectDistance = 2.5f;
        public float noiseBase = .6f;
        public float decayPerSecond = .12f;
        public float alertedHoldTime = 8f;
    }
}
