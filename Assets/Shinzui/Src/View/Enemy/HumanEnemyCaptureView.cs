using System.IO;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

namespace Shinzui.View
{
    /// <summary>背後の吐息、無人の振り返り、正面の顔アップを順に再生</summary>
    [DefaultExecutionOrder(200)]
    public sealed class HumanEnemyCaptureView : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float durationMultiplier = 1.5f;
        [SerializeField, Min(.1f)] private float breathDuration = .8f;
        [SerializeField, Min(.1f)] private float turnBackDuration = 4f;
        // 正面へ戻る時間を短縮した分は無人の背後を見る間に回し、全体の尺を維持
        [SerializeField, Min(.1f)] private float emptyHoldDuration = 1.53f;
        [SerializeField, Min(.1f)] private float returnDuration = .27f;
        [SerializeField, Min(.01f)] private float rushDuration = .12f;
        [SerializeField, Min(.1f)] private float faceHoldDuration = .65f;
        [SerializeField, Min(0f)] private float attackShakeAngle = 1.25f;
        [SerializeField, Min(0f)] private float attackShakeDistance = .003f;
        [SerializeField, Min(1f)] private float attackShakeFrequency = 18f;
        private PlayerView _player;
        private Camera _camera;
        private Transform _head;
        private HumanEnemyLocomotion _motion;
        private Animator _animator;
        private NavMeshAgent _agent;
        private CinemachineBrain _brain;
        private Renderer[] _renderers;
        private bool[] _visible;
        private Collider[] _colliders;
        private bool[] _colliderStates;
        private bool _motionEnabled, _animatorEnabled, _agentEnabled, _brainEnabled, _controlled;
        private Vector3 _cameraPosition, _enemyPosition;
        private Quaternion _cameraRotation, _enemyRotation, _headRotation;
        private float _fov, _near, _elapsed;
        private int _cameraMask;
        private Light _faceLight;
        private bool _ownsControl, _revealed;
        private FMOD.Sound _sound;
        private FMOD.Channel _channel;
        private FMOD.Studio.Bus _bus;
        private bool _busLocked;
        public bool IsPlaying { get; private set; }
        public string Phase { get; private set; } = "Ready";

        /// <summary>捕獲時の状態を保存してカメラと敵の描画を占有</summary>
        /// <param name="player">捕まったプレイヤー</param>
        /// <returns>必要なカメラと頭の骨が揃い開始できた場合はtrue</returns>
        public bool Begin(PlayerView player)
        {
            Cancel();
            if (player == null || player.MainCamera == null) return false;
            foreach (var bone in GetComponentsInChildren<Transform>())
                if (bone.name == "Head") _head = bone;
            if (_head == null) return false;

            // 通常操作と自動カメラ、骨格更新を止めて演出の初期姿勢を固定
            _player = player;
            _camera = player.MainCamera;
            _cameraPosition = _camera.transform.position;
            _cameraRotation = _camera.transform.rotation;
            _enemyPosition = transform.position;
            _enemyRotation = transform.rotation;
            _headRotation = _head.localRotation;
            _fov = _camera.fieldOfView;
            _near = _camera.nearClipPlane;
            _cameraMask = _camera.cullingMask;
            _controlled = player.IsCinematicControlled;
            player.IsCinematicControlled = true;
            _brain = _camera.GetComponent<CinemachineBrain>();
            _brainEnabled = _brain != null && _brain.enabled;
            if (_brain != null) _brain.enabled = false;
            _motion = GetComponent<HumanEnemyLocomotion>();
            _animator = GetComponentInChildren<Animator>();
            _agent = GetComponent<NavMeshAgent>();
            _motionEnabled = _motion != null && _motion.enabled;
            _animatorEnabled = _animator != null && _animator.enabled;
            _agentEnabled = _agent != null && _agent.enabled;
            if (_motion != null) _motion.enabled = false;
            if (_animator != null) _animator.enabled = false;
            if (_agent != null) _agent.enabled = false;
            _colliders = GetComponentsInChildren<Collider>();
            _colliderStates = new bool[_colliders.Length];
            for (int i = 0; i < _colliders.Length; i++)
            {
                _colliderStates[i] = _colliders[i].enabled;
                _colliders[i].enabled = false;
            }
            _camera.nearClipPlane = .01f;
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0) _camera.cullingMask &= ~(1 << playerLayer);

            // 捕獲した敵だけを隠し、他の敵のフェードを妨げない
            _renderers = GetComponentsInChildren<Renderer>();
            _visible = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _visible[i] = _renderers[i].enabled;
                _renderers[i].enabled = false;
            }
            _elapsed = 0f;
            _revealed = false;
            _ownsControl = true;
            IsPlaying = true;
            Phase = "Breath behind";
            PlaySound("HumanCaptureBreath.wav", true);
            return true;
        }

        /// <summary>無人の背後へゆっくり向き直り、正面に戻ってから顔を突き出す</summary>
        private void LateUpdate()
        {
            if (!IsPlaying) return;
            _elapsed += Time.unscaledDeltaTime / durationMultiplier;
            float backEnd = breathDuration + turnBackDuration;
            float holdEnd = backEnd + emptyHoldDuration;
            float returnEnd = holdEnd + returnDuration;
            float yaw = 0f;
            if (_elapsed >= breathDuration && _elapsed < backEnd)
            {
                Phase = "Turn behind";
                // 12乗のカーブで終盤まで動きを溜め、最後の瞬間に一気に振り向く
                float turnProgress = Mathf.Clamp01((_elapsed - breathDuration) / turnBackDuration);
                yaw = 180f * Mathf.Pow(turnProgress, 12f);
            }
            else if (_elapsed >= backEnd && _elapsed < holdEnd)
            {
                Phase = "Nobody behind";
                yaw = 180f;
            }
            else if (_elapsed >= holdEnd && _elapsed < returnEnd)
            {
                Phase = "Return to front";
                // 短いOutQuartで正面へ一気に振り向き、終点だけ減速
                float remaining = 1f - Mathf.Clamp01((_elapsed - holdEnd) / returnDuration);
                yaw = 180f * remaining * remaining * remaining * remaining;
            }
            _camera.transform.SetPositionAndRotation(_cameraPosition, _cameraRotation * Quaternion.Euler(0f, yaw, 0f));

            // 正面に戻ったフレームからだけ敵を表示し、顔の中心をレンズ直前へ合わせる
            if (_elapsed < returnEnd) return;
            if (!_revealed)
            {
                _revealed = true;
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null && _renderers[i].transform.IsChildOf(transform))
                        _renderers[i].enabled = _visible[i];
                _faceLight = new GameObject("Capture Face Light", typeof(Light)).GetComponent<Light>();
                _faceLight.type = LightType.Point;
                _faceLight.range = 2f;
                _faceLight.intensity = .12f;
                _faceLight.shadows = LightShadows.None;
                _faceLight.transform.position = _cameraPosition + _camera.transform.up * .15f;
                PlaySound("MonstrousScream.mp3", false);
            }
            Phase = "Face / jumpscare";
            float progress = Mathf.Clamp01((_elapsed - returnEnd) / rushDuration);
            _camera.fieldOfView = Mathf.Lerp(_fov, 40f, progress);
            transform.rotation = Quaternion.LookRotation(-_camera.transform.forward, _camera.transform.up);
            _head.rotation = transform.rotation;
            Vector3 face = _head.position + _head.up * .12f + _head.forward * .08f;
            transform.position += _cameraPosition + _camera.transform.forward * Mathf.Lerp(.8f, .24f, progress) - face;

            // 敵の配置後に視点だけを揺らし、顔がカメラの揺れに追従して相殺されるのを防ぐ
            ApplyAttackShake((_elapsed - returnEnd) * durationMultiplier,
                (rushDuration + faceHoldDuration) * durationMultiplier);
            if (_elapsed >= returnEnd + rushDuration + faceHoldDuration)
            {
                Phase = "GAME OVER";
                IsPlaying = false;
                ReleaseSound();
            }
        }

        /// <summary>襲撃中だけ基準視点に細かい振動を重ね、終了時は元の姿勢へ戻す</summary>
        /// <param name="elapsed">襲撃開始からの実時間</param>
        /// <param name="duration">襲撃演出全体の実時間</param>
        private void ApplyAttackShake(float elapsed, float duration)
        {
            // 周波数を実時間に合わせ、演出速度を変えても小刻みな揺れを維持
            float strength = Mathf.Clamp01(elapsed / .04f) * Mathf.Clamp01((duration - elapsed) / .1f);
            float phase = elapsed * attackShakeFrequency * Mathf.PI * 2f;
            var vibration = new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 1.31f), Mathf.Sin(phase * .83f));
            Vector3 offset = new Vector3(vibration.y, vibration.x, 0f) * (attackShakeDistance * strength);
            Quaternion rotation = Quaternion.Euler(vibration * (attackShakeAngle * strength));

            // 前フレームの揺れを足し込まず、保存した位置と回転から毎回再構成
            _camera.transform.SetPositionAndRotation(_cameraPosition + _cameraRotation * offset,
                _cameraRotation * rotation);
        }

        /// <summary>Unity Audioが無効な環境でもSEバス経由で吐息と叫び声を再生</summary>
        /// <param name="file">StreamingAssets/EventEnemy内の音声</param>
        /// <param name="behind">開始時の背後の位置から再生するか</param>
        private void PlaySound(string file, bool behind)
        {
            ReleaseSound();
            var system = FMODUnity.RuntimeManager.CoreSystem;
            var mode = FMOD.MODE.CREATESAMPLE | (behind ? FMOD.MODE._3D : FMOD.MODE._2D);
            var result = system.createSound(Path.Combine(UnityEngine.Application.streamingAssetsPath, "EventEnemy", file), mode, out _sound);
            if (result != FMOD.RESULT.OK) { Debug.LogError("Human capture audio: " + result, this); return; }
            FMOD.ChannelGroup group = default;
            var studio = FMODUnity.RuntimeManager.StudioSystem;
            if (studio.getBus("bus:/SE", out _bus) == FMOD.RESULT.OK && _bus.lockChannelGroup() == FMOD.RESULT.OK)
            {
                _busLocked = true;
                studio.flushCommands();
                _bus.getChannelGroup(out group);
            }
            system.playSound(_sound, group, true, out _channel);
            if (behind)
            {
                var position = FMODUnity.RuntimeUtils.ToFMODVector(_cameraPosition - _camera.transform.forward * .65f);
                var velocity = new FMOD.VECTOR();
                _channel.set3DAttributes(ref position, ref velocity);
                _channel.set3DMinMaxDistance(1f, 8f);
            }
            _channel.setVolume(behind ? .85f : .7f);
            _channel.setPaused(false);
        }

        /// <summary>音声とSEバスの参照を解放</summary>
        private void ReleaseSound()
        {
            if (_channel.hasHandle()) { _channel.stop(); _channel.clearHandle(); }
            if (_sound.hasHandle()) { _sound.release(); _sound.clearHandle(); }
            if (_busLocked) { _bus.unlockChannelGroup(); _busLocked = false; }
        }

        /// <summary>再試行やシーン終了時に保存した表示と操作状態を復元</summary>
        public void Cancel()
        {
            ReleaseSound();
            IsPlaying = false;
            if (!_ownsControl) return;
            _ownsControl = false;
            if (_faceLight != null) { _faceLight.enabled = false; Destroy(_faceLight.gameObject); }
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = _visible[i];
            transform.SetPositionAndRotation(_enemyPosition, _enemyRotation);
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = _colliderStates[i];
            if (_head != null) _head.localRotation = _headRotation;
            if (_motion != null) _motion.enabled = _motionEnabled;
            if (_animator != null) _animator.enabled = _animatorEnabled;
            if (_agent != null) _agent.enabled = _agentEnabled;
            if (_camera != null)
            {
                _camera.transform.SetPositionAndRotation(_cameraPosition, _cameraRotation);
                _camera.fieldOfView = _fov;
                _camera.nearClipPlane = _near;
                _camera.cullingMask = _cameraMask;
            }
            if (_player != null) _player.IsCinematicControlled = _controlled;
            if (_brain != null) _brain.enabled = _brainEnabled;
            Phase = "Ready";
        }

        /// <summary>無効化された場合もカメラと音声を解放</summary>
        private void OnDisable() => Cancel();
    }
}
