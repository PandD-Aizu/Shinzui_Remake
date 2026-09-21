using System;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>Scene-local tuning. Defaults also apply to scenes authored before the director existed.</summary>
    [Serializable]
    public sealed class EnemyDirectorSettings
    {
        public float ambientDuration = 12f;
        public float buildUpDuration = 35f;
        public float recoveryDuration = 18f;
        public float searchDuration = 14f;
        public float hintInterval = 8f;
        public float hintCellSize = 12f;
        public float searchRadius = 7f;
        public float searchPointInterval = 3f;
        public float retreatDistance = 18f;
        public float pressureDistance = 18f;
        public float chasePressurePerSecond = 0.075f;
        public float proximityPressurePerSecond = 0.025f;
        public float pressureDecayPerSecond = 0.06f;
        public float sightDistance = 16f;
        public float fieldOfView = 110f;
        public float detectionTime = 0.3f;
        public float eyeHeight = 1f;
        public float walkingNoiseRadius = 4f;
        public float runningNoiseRadius = 13f;
        public float footstepInterval = 0.65f;
        public float patrolSpeedMultiplier = 0.65f;
        public float repathInterval = 0.3f;
        public int sightBlockingLayers = -1;

        internal static float Positive(float value, float fallback)
            => float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? fallback : value;
    }

    public enum EnemyDirectorPhase { Ambient, BuildUp, Hunt, Search, Recovery }
}
