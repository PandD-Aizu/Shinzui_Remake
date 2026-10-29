using System;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>緊張度と退避、再出現の調整値</summary>
    [Serializable]
    public sealed class MenaceSettings
    {
        public float engagedPerSecond = .10f;
        public float visiblePerSecond = .06f;
        public float proximityPerSecond = .04f;
        public float decayPerSecond = .05f;
        public float backstageDecayPerSecond = .12f;
        public float visibleDistance = 12f;
        public float menaceRadius = 15f;
        public float backstageMinDuration = 20f;
        public float backstageMaxDuration = 35f;
        public float emergeMinDistance = 15f;
        public float emergeMaxDistance = 30f;
        public float featuredHoldTime = 20f;
        public float otherRetreatDistance = 20f;
        public float leashDistance = 35f;
        public float leashTime = 25f;
        public float backstageSpeed = 3f;
        public float backstageSoundInterval = 4f;
        public float pointRetryInterval = 2f;
        public float exitRetryTime = 12f;
    }
}
