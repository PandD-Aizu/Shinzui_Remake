using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Shinzui.Infrastructure.Services;
using UnityEngine;

namespace Shinzui.View
{
    /// <summary>しゃがみと右腕IKを組み合わせてペンライトを床へ置く</summary>
    [DefaultExecutionOrder(10010)]
    public sealed class PenlightPlacementView : MonoBehaviour
    {
        private PlayerView _player;
        private PlayerBodyView _body;
        private PenlightPlacementService _placement;
        private GameObject _held;
        private bool _playing;
        private float _originalHeight;
        private Pose _target;

        /// <summary>設置動作が右手を使用しているか</summary>
        public bool IsPlaying => _playing;

        /// <summary>プレイヤーと床面判定を接続する</summary>
        /// <param name="player">演出対象のプレイヤー</param>
        /// <param name="placement">床面の設置判定</param>
        public void Configure(PlayerView player, PenlightPlacementService placement)
        {
            _player = player;
            _placement = placement;
        }

        /// <summary>右手で床へ運び、解放時に設置と消費を確定する</summary>
        /// <param name="commit">装備状態を再検証して設置を確定する処理</param>
        /// <returns>設置できたか</returns>
        public async Task<bool> PlayAsync(Func<Task<bool>> commit)
        {
            if (!UnityEngine.Application.isPlaying || _playing || _player == null || _player.IsCinematicControlled || _player.IsHiding
                || !_placement.TryGetPlacement(out _target)) return false;
            _body = _player.GetComponentInChildren<PlayerBodyView>();
            var controller = _player.GetComponent<CharacterController>();
            var prefab = Resources.Load<GameObject>("PlacedPenlight");
            if (_body == null || controller == null || prefab == null
                || !_body.TryGetRightPalmPose(out _)) return false;

            // 移動を止め、手元の実物を表示してから腰と腕を床へ近づける
            _playing = true;
            _originalHeight = controller.height;
            _player.IsCinematicControlled = true;
            _held = Instantiate(prefab, transform);
            _held.name = "Held Penlight";
            UpdateHeldPose();
            try
            {
                if (!await AnimateAsync(.65f, true)) return false;

                // 手を開く瞬間にだけ確定し、床が変わっていたら消費しない
                if (!_placement.TryGetPlacement(out Pose current)
                    || Vector3.Distance(current.position, _target.position) > .05f) return false;
                bool placed = await commit();
                if (placed && _held != null)
                {
                    _held.SetActive(false);
                    Destroy(_held);
                    _held = null;
                }

                await AnimateAsync(.4f, false);
                return placed;
            }
            finally
            {
                ResetPose();
            }
        }

        /// <summary>しゃがみ、腕の接近、指の解放を時間に沿って更新する</summary>
        /// <param name="duration">動作にかける秒数</param>
        /// <param name="reaching">床へ近づく動作か</param>
        /// <returns>動作を完了できたか</returns>
        private async Task<bool> AnimateAsync(float duration, bool reaching)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (this == null || !_playing || !isActiveAndEnabled || _player == null || !_player.isActiveAndEnabled) return false;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float progress = reaching ? t : 1f - t;

                // 足元を固定して腰を下げ、掌をペンライトの上面へ合わせる
                _player.SetHeight(Mathf.Lerp(_originalHeight, Mathf.Min(_originalHeight, 1.05f),
                    Mathf.SmoothStep(0f, 1f, progress)));
                float grip = reaching ? Mathf.Lerp(.65f, 0f, Mathf.InverseLerp(.85f, 1f, t)) : 0f;
                _body.SetPickupPose(_target.position + _target.rotation * Vector3.up * .045f,
                    _target.rotation, progress, grip);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            return true;
        }

        /// <summary>骨格更新後の掌へ手元のペンライトを追従させる</summary>
        private void LateUpdate()
        {
            UpdateHeldPose();
        }

        /// <summary>掌の下に本体を合わせ、腕と小物の位置ずれを防ぐ</summary>
        private void UpdateHeldPose()
        {
            if (_held == null || _body == null || !_body.TryGetRightPalmPose(out Pose palm)) return;
            _held.transform.SetPositionAndRotation(palm.position - palm.rotation * Vector3.up * .045f, palm.rotation);
        }

        /// <summary>中断時も腕、身長、移動状態と手元の小物を復元する</summary>
        private void ResetPose()
        {
            if (!_playing) return;
            _playing = false;
            if (_held != null) Destroy(_held);
            _held = null;
            if (_body != null) _body.SetPickupPose(_target.position, _target.rotation, 0f, 0f);
            if (_player != null)
            {
                _player.SetHeight(_originalHeight);
                _player.IsCinematicControlled = false;
            }
        }

        /// <summary>無効化やシーン切り替えで設置動作を解除する</summary>
        private void OnDisable()
        {
            ResetPose();
        }
    }
}
