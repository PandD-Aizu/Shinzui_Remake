using UnityEngine;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>可視時のみ位置と速度を含む視覚サンプル</summary>
    public readonly struct SightSample
    {
        public bool InRange { get; }
        public float Distance { get; }
        public float AngleFromForward { get; }
        public float VisibleRatio { get; }
        public Vector3 SeenPosition { get; }
        public Vector3 SeenVelocity { get; }

        /// <summary>不可視時の座標を除去してサンプルを作成</summary>
        /// <param name="inRange">視認距離内か</param>
        /// <param name="distance">対象までの距離</param>
        /// <param name="angleFromForward">前方との水平角</param>
        /// <param name="visibleRatio">可視点の割合</param>
        /// <param name="seenPosition">視認位置</param>
        /// <param name="seenVelocity">視認速度</param>
        public SightSample(bool inRange, float distance, float angleFromForward, float visibleRatio,
            Vector3 seenPosition, Vector3 seenVelocity)
        {
            InRange = inRange;
            Distance = distance;
            AngleFromForward = angleFromForward;
            VisibleRatio = inRange ? Mathf.Clamp01(visibleRatio) : 0f;
            SeenPosition = VisibleRatio > 0f ? seenPosition : default;
            SeenVelocity = VisibleRatio > 0f ? seenVelocity : default;
        }
    }
}
