using Unity.Cinemachine;
using UnityEngine;

namespace Shinzui.View.Flashlight
{
    /// <summary>
    /// ストロボのチャージ量に応じて画面中央へズームするよう
    /// Cinemachineの最終CameraStateの視野角を狭める
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StrobeZoomExtension : CinemachineExtension
    {
        [Tooltip("フルチャージ時の視野角の倍率")]
        [SerializeField, Range(0.3f, 1f)] private float fullChargeFovRatio = 0.7f;

        [Tooltip("ズームインの追従速度")]
        [SerializeField, Min(0.1f)] private float zoomInSpeed = 6f;

        [Tooltip("発光やキャンセルで戻る時の追従速度")]
        [SerializeField, Min(0.1f)] private float zoomOutSpeed = 14f;

        private float _target;
        private float _current;

        /// <summary>
        /// ズーム量を設定する
        /// </summary>
        /// <param name="amount">0でズームなし、1でフルチャージ時のズーム</param>
        public void SetZoom(float amount)
        {
            _target = Mathf.Clamp01(amount);
        }

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize) return;

            // 溜める時はゆっくり寄り、戻る時は素早く引く
            if (deltaTime < 0f)
            {
                _current = _target;
            }
            else
            {
                float speed = _target > _current ? zoomInSpeed : zoomOutSpeed;
                _current = Mathf.Lerp(_current, _target, 1f - Mathf.Exp(-speed * deltaTime));
            }

            // 溜め始めは穏やかに、終盤ほど強く寄せる
            float eased = _current * _current * (3f - 2f * _current);
            state.Lens.FieldOfView *= Mathf.Lerp(1f, fullChargeFovRatio, eased);
        }
    }
}
