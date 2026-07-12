using UnityEngine;

namespace Shinzui.Presentation
{
    /// <summary>
    /// FlashlightPresenter に公開する敵ストロボ対象。
    /// </summary>
    public readonly struct StrobeTarget
    {
        public static readonly StrobeTarget Empty = new(null);

        private readonly EnemyController _controller;

        internal StrobeTarget(EnemyController controller)
        {
            _controller = controller;
        }

        public bool IsValid => _controller != null;
        public int Id => IsValid ? _controller.Id : -1;
        public Vector3 StrobeTargetPosition => IsValid ? _controller.StrobeTargetPosition : Vector3.zero;

        /// <summary>
        /// 敵にストロボ効果を適用する
        /// ストロボの強度や持続時間は FlashlightPresenter から制御される
        /// </summary>
        /// <param name="stopMovement">移動を停止するかどうか</param>
        /// <param name="speedMultiplier">速度の倍率</param>
        /// <param name="duration">持続時間</param>
        public void ApplyStrobeEffect(bool stopMovement, float speedMultiplier, float duration)
        {
            if (!IsValid)
            {
                return;
            }

            _controller.ApplyStrobeEffect(stopMovement, speedMultiplier, duration);
        }
    }
}
