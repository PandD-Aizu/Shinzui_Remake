using System;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>知覚の調整値</summary>
    [Serializable]
    public sealed class EnemyBrainSettings
    {
        public float chaseGraceTime = 1.5f;
        public float searchDuration = 20f;
        public float searchRadius = 7f;
        public float searchSpeedMultiplier = .8f;
        public float investigateSpeedMultiplier = .5f;
        public float arrivalDistance = 1f;
        public float lookAroundDuration = 2f;
        public float investigateTurnDuration = 1f;
        public float moveTimeout = 6f;
        public float baseSightRate = 2.5f;
        public float instantDetectDistance = 2.5f;
        public float noiseBase = .6f;
        public float decayPerSecond = .12f;
        public float alertedHoldTime = 8f;
    }
}
