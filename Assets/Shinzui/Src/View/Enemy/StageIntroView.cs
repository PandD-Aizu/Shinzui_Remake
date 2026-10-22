using System;
using System.IO;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shinzui.View
{
    /// <summary>偽チュートリアルから転倒、敵の登場、任意入力による起立までを演出</summary>
    public sealed class StageIntroView : MonoBehaviour
    {
        public enum IntroPhase { Dormant, Preparing, Tutorial, Corruption, Falling, Approach, AwaitingInput, Rising, Escape, Complete, Failed, Lunging }

        [Header("References")]
        [SerializeField] private EnemyView eventEnemy;
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private TMP_Text message;
        [SerializeField] private Canvas gameplayHud;
        [Header("Timing")]
        [SerializeField, Min(0)] private float startDelay = 1.5f;
        [SerializeField, Min(.1f)] private float tutorialDuration = 3f;
        [SerializeField, Min(.1f)] private float corruptionDuration = 4f;
        [SerializeField, Min(.1f)] private float fallDuration = .9f;
        [SerializeField, Min(.1f)] private float approachDuration = 6.5f;
        [SerializeField, Min(.05f)] private float rushDuration = .28f;
        [SerializeField, Min(.1f)] private float riseDuration = 1.5f;
        [SerializeField, Min(.1f)] private float lungeDuration = .2f;
        [SerializeField, Min(0)] private float escapeGrace = 2.5f;
        [Header("Enemy")]
        [SerializeField, Min(1.5f)] private float stoppingDistance = 1.5f;
        [Header("Impact")]
        [SerializeField, Range(0f, 5f)] private float cameraShake = 2.5f;
        [SerializeField, Range(0f, 1f)] private float impactBlur = .65f;
        [Header("Audio")]
        [SerializeField] private string screamFile = "EventEnemy/MonstrousScream.mp3";
        [SerializeField, Range(0f, 1f)] private float screamVolume = .8f;
        [SerializeField, Range(-1f, 0f)] private float screamPan = -1f;
        [Header("Text")]
        [SerializeField] private Color bloodColor = new Color(.25f, .005f, .018f);
        [SerializeField, Range(0f, 1f)] private float eventNoiseIntensity = .02f;

        private PlayerView _player;
        private NavMeshAgent _agent;
        private Renderer[] _renderers;
        private bool[] _rendererStates;
        private Collider[] _enemyColliders;
        private bool[] _colliderStates;
        private CinemachineBrain _brain;
        private bool _brainEnabled;
        private Vector3 _standingPosition, _pronePosition, _approachTarget;
        private Quaternion _standingRotation, _proneRotation;
        private HumanEnemyLocomotion _locomotion;
        private int _cameraMask;
        private float _nearClipPlane;
        private float _fieldOfView;
        private bool _hudEnabled;
        private float _elapsed, _agentSpeed;
        private bool _ownsControl, _begun;
        private bool _agentUpdateRotation;
        private float _agentStoppingDistance;
        private FMOD.Sound _scream;
        private FMOD.Channel _screamChannel;
        private FMOD.Studio.Bus _screamBus;
        private bool _busLocked;
        private Vector3[] _approachCorners;
        private float _approachLength;
        private Volume _impactVolume;
        private VolumeProfile _impactProfile;
        private UniversalAdditionalCameraData _cameraData;
        private bool _postProcessing;
        private LayerMask _volumeMask;
        public IntroPhase Phase { get; private set; }
        public event Action<bool> ControlChanged;
        public event Action Finished;
        public event Action Failed;
        public float EventNoiseIntensity => (Phase >= IntroPhase.Tutorial && Phase <= IntroPhase.Escape) || Phase == IntroPhase.Lunging ? eventNoiseIntensity : 0f;

        /// <summary>敵の描画と案内を初期状態へ戻す</summary>
        private void Awake()
        {
            if (eventEnemy == null) return;
            _agent = eventEnemy.ResolveAgent();
            _locomotion = eventEnemy.GetComponent<HumanEnemyLocomotion>();
            _renderers = eventEnemy.GetComponentsInChildren<Renderer>(true);
            _rendererStates = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _rendererStates[i] = _renderers[i].enabled;
            _enemyColliders = eventEnemy.GetComponentsInChildren<Collider>(true);
            _colliderStates = new bool[_enemyColliders.Length];
            for (int i = 0; i < _enemyColliders.Length; i++) _colliderStates[i] = _enemyColliders[i].enabled;
            SetEnemyVisible(false);
            SetEnemyCollisions(false);
            if (overlay != null) overlay.alpha = 0f;
        }

        /// <summary>依存オブジェクトを受け取りマップ準備待ちへ入る</summary>
        /// <param name="player">操作対象のプレイヤー</param>
        public void Begin(PlayerView player)
        {
            if (_begun) return;
            _begun = true;
            _player = player;
            if (_player == null || _player.MainCamera == null || _agent == null || _locomotion == null || message == null)
            {
                Debug.LogError("StageIntro: Missing player, camera, enemy agent or tutorial label", this);
                Cancel();
                return;
            }
            _agentSpeed = _agent.speed;
            ChangePhase(IntroPhase.Preparing);
        }

        /// <summary>段階ごとの時間と新規入力で演出を進める</summary>
        private void Update()
        {
            if (!_begun || Phase == IntroPhase.Complete || Time.timeScale <= 0f) return;
            _elapsed += Time.deltaTime;
            switch (Phase)
            {
                case IntroPhase.Preparing:
                    if (_elapsed >= startDelay && _player.IsGrounded &&
                        NavMesh.SamplePosition(_player.transform.position, out _, 3f, NavMesh.AllAreas))
                    {
                        ShowMessage("走る\n<size=65%>Shift を押しながら移動</size>");
                        ChangePhase(IntroPhase.Tutorial);
                    }
                    break;
                case IntroPhase.Tutorial:
                    if (_elapsed >= tutorialDuration) ChangePhase(IntroPhase.Corruption);
                    break;
                case IntroPhase.Corruption:
                    CorruptText(Mathf.Clamp01(_elapsed / corruptionDuration));
                    if (_elapsed >= corruptionDuration && _player.IsGrounded) StartFall();
                    break;
                case IntroPhase.Falling:
                    if (_elapsed >= fallDuration)
                    {
                        SetEnemyVisible(true);
                        _agent.isStopped = true;
                        ChangePhase(IntroPhase.Approach);
                        ShowMessage("何かキーを押して立ち上がる");
                        if (UnityEngine.Application.isFocused && HasFreshInput()) RequestStandUp();
                    }
                    break;
                case IntroPhase.Approach:
                    MoveApproachingEnemy();
                    if (_elapsed >= approachDuration)
                    {
                        _agent.ResetPath();
                        _agent.isStopped = true;
                        _agent.velocity = Vector3.zero;
                        overlay.alpha = 0f;
                        _locomotion.BeginLunge(_player.MainCamera);
                        if (_screamChannel.hasHandle())
                        {
                            _screamChannel.setPosition(0, FMOD.TIMEUNIT.MS);
                            _screamChannel.setPan(0f);
                        }
                        ChangePhase(IntroPhase.Lunging);
                    }
                    else if (UnityEngine.Application.isFocused && HasFreshInput()) RequestStandUp();
                    break;
                case IntroPhase.Lunging:
                    if (_elapsed >= lungeDuration + .12f)
                    {
                        // 共通のゲームオーバーUIが属するCanvasを再表示
                        if (gameplayHud != null) gameplayHud.enabled = _hudEnabled;
                        ReleaseScream();
                        ReleaseImpact();
                        ChangePhase(IntroPhase.Failed);
                        Failed?.Invoke();
                    }
                    break;
                case IntroPhase.Rising:
                    if (_elapsed >= riseDuration)
                    {
                        ReleaseControl();
                        ShowMessage("走れ");
                        ChangePhase(IntroPhase.Escape);
                    }
                    break;
                case IntroPhase.Escape:
                    if (_elapsed >= escapeGrace)
                    {
                        overlay.alpha = 0f;
                        _agent.speed = _agentSpeed;
                        SetEnemyCollisions(true);
                        ReleaseScream();
                        ChangePhase(IntroPhase.Complete);
                        Finished?.Invoke();
                    }
                    break;
            }
        }

        /// <summary>通常カメラ評価後に転倒姿勢と起立姿勢を適用</summary>
        private void LateUpdate()
        {
            if (!_ownsControl) return;
            float blend = Phase == IntroPhase.Falling ? Mathf.SmoothStep(0, 1, _elapsed / fallDuration) : 1f;
            if (Phase == IntroPhase.Rising) blend = 1f - Mathf.SmoothStep(0, 1, _elapsed / riseDuration);
            Vector3 position = Vector3.Lerp(_standingPosition, _pronePosition, blend);
            Quaternion rotation = Quaternion.Slerp(_standingRotation, _proneRotation, blend);

            // 接地後は横を見る視点を保ち、入力失敗時はその正面へ敵を飛び込ませる
            if (_locomotion != null)
            {
                _locomotion.SetIntroPose(Phase == IntroPhase.Rising ? blend : 1f, _player.MainCamera);
                if (Phase == IntroPhase.Lunging || Phase == IntroPhase.Failed)
                    _locomotion.SetLungeProgress(Phase == IntroPhase.Failed ? 1f : _elapsed / lungeDuration);
            }
            // 接地直後だけ反動を加え、その後は地面の視点で静止
            if (Phase == IntroPhase.Falling)
                position += Vector3.up * Mathf.Sin(blend * Mathf.PI * 3f) * .035f * blend;
            _player.MainCamera.transform.SetPositionAndRotation(position, rotation);
            _player.MainCamera.fieldOfView = Mathf.Lerp(_fieldOfView, 45f, blend);
            ApplyImpact();
        }

        /// <summary>接近経路の大部分を一気に進み、残りは入力期限までゆっくり詰める</summary>
        private void MoveApproachingEnemy()
        {
            float burst = Mathf.Min(rushDuration, approachDuration * .25f);
            float progress = _elapsed < burst
                ? .88f * (1f - Mathf.Pow(1f - _elapsed / burst, 3f))
                : Mathf.Lerp(.88f, 1f, (_elapsed - burst) / (approachDuration - burst));
            float remaining = _approachLength * progress;

            // 検証済みの経路を辿り、NavMeshの加減速に演出の緩急を左右させない
            for (int corner = 1; corner < _approachCorners.Length; corner++)
            {
                float length = Vector3.Distance(_approachCorners[corner - 1], _approachCorners[corner]);
                if (remaining <= length || corner == _approachCorners.Length - 1)
                {
                    _agent.Warp(Vector3.Lerp(_approachCorners[corner - 1], _approachCorners[corner], length > 0f ? remaining / length : 1f));
                    break;
                }
                remaining -= length;
            }
        }

        /// <summary>演出専用の一時Volumeを用意し、通常のポスト処理から独立させる</summary>
        private void PrepareImpact()
        {
            _cameraData = _player.MainCamera.GetUniversalAdditionalCameraData();
            _postProcessing = _cameraData.renderPostProcessing;
            _volumeMask = _cameraData.volumeLayerMask;
            _cameraData.renderPostProcessing = true;
            _cameraData.volumeLayerMask |= 1 << gameObject.layer;

            _impactProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var blur = _impactProfile.Add<MotionBlur>(true);
            blur.mode.Override(MotionBlurMode.CameraAndObjects);
            blur.intensity.Override(impactBlur);
            blur.clamp.Override(.08f);
            _impactVolume = gameObject.AddComponent<Volume>();
            _impactVolume.isGlobal = true;
            _impactVolume.priority = 100f;
            _impactVolume.sharedProfile = _impactProfile;
            _impactVolume.weight = 0f;
        }

        /// <summary>転倒と二段の突進に同期して視点の反動とブラーを短く重ねる</summary>
        private void ApplyImpact()
        {
            float strength = 0f;
            if (Phase == IntroPhase.Falling)
                strength = Mathf.Sin(Mathf.Clamp01(_elapsed / fallDuration) * Mathf.PI) * .65f;
            else if (Phase == IntroPhase.Approach)
                strength = 1f - Mathf.SmoothStep(0f, 1f, _elapsed / (rushDuration + .18f));
            else if (Phase == IntroPhase.Lunging)
                strength = Mathf.Sin(Mathf.Clamp01(_elapsed / (lungeDuration + .12f)) * Mathf.PI) * 1.4f;

            // 毎フレームの基準姿勢に小さな回転と後ずさりを加え、揺れを累積させない
            var camera = _player.MainCamera.transform;
            camera.rotation *= Quaternion.Euler(
                Mathf.Sin(_elapsed * 73f) * cameraShake * strength,
                Mathf.Sin(_elapsed * 91f) * cameraShake * strength * .55f,
                Mathf.Sin(_elapsed * 59f) * cameraShake * strength * .7f);
            camera.position -= camera.forward * (.025f * strength);
            if (_impactVolume != null) _impactVolume.weight = Mathf.Clamp01(strength);
        }

        /// <summary>演出用Volumeを破棄し、カメラのポスト処理設定を復元</summary>
        private void ReleaseImpact()
        {
            if (_impactVolume == null) return;
            _impactVolume.enabled = false;
            Destroy(_impactVolume);
            foreach (var component in _impactProfile.components) Destroy(component);
            Destroy(_impactProfile);
            _impactVolume = null;
            _impactProfile = null;
            if (_cameraData != null)
            {
                _cameraData.renderPostProcessing = _postProcessing;
                _cameraData.volumeLayerMask = _volumeMask;
            }
        }

        /// <summary>新しく押されたキー・ボタンを受け付ける</summary>
        /// <returns>受付開始後に新規入力がある場合はtrue</returns>
        private bool HasFreshInput()
        {
            if (Keyboard.current != null)
                foreach (var key in Keyboard.current.allKeys)
                    if (key.wasPressedThisFrame) return true;
            if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
            if (Gamepad.current != null)
                foreach (var control in Gamepad.current.allControls)
                    if (control is ButtonControl button && button.wasPressedThisFrame) return true;
            return false;
        }

        /// <summary>入力期限前の新規入力で起立を開始</summary>
        /// <returns>起立を開始した場合はtrue</returns>
        public bool RequestStandUp()
        {
            if (Phase != IntroPhase.Approach || _elapsed >= approachDuration) return false;

            // 接近を止め、四つん這いから通常姿勢への復帰を開始
            _agent.ResetPath();
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            overlay.alpha = 0f;
            ChangePhase(IntroPhase.Rising);
            return true;
        }

        /// <summary>転倒位置と敵の進路を確保して叫び声と操作停止を同期</summary>
        private void StartFall()
        {
            if (!TryPlaceEnemy())
            {
                Debug.LogWarning("StageIntro: No clear approach path; introduction cancelled", this);
                Cancel();
                return;
            }
            _ownsControl = true;
            _player.IsCinematicControlled = true;
            ControlChanged?.Invoke(true);
            var camera = _player.MainCamera;
            _standingPosition = camera.transform.position;
            _standingRotation = camera.transform.rotation;
            _cameraMask = camera.cullingMask;
            _nearClipPlane = camera.nearClipPlane;
            _fieldOfView = camera.fieldOfView;
            camera.nearClipPlane = .03f;
            camera.cullingMask &= ~(1 << LayerMask.NameToLayer("Player"));
            PrepareImpact();
            if (gameplayHud != null) { _hudEnabled = gameplayHud.enabled; gameplayHud.enabled = false; }
            _brain = camera.GetComponent<CinemachineBrain>();
            _brainEnabled = _brain != null && _brain.enabled;
            if (_brain != null) _brain.enabled = false;
            Vector3 floor = _player.transform.position;
            if (Physics.Raycast(_standingPosition, Vector3.down, out var hit, 4f, 1 << 8, QueryTriggerInteraction.Ignore)) floor = hit.point;
            _pronePosition = new Vector3(_standingPosition.x, floor.y + .24f, _standingPosition.z);
            // うつ伏せの高さで敵のいる横方向を見て、距離が近くても見上げない
            Quaternion enemyRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(_pronePosition - _approachTarget, Vector3.up));
            _agent.transform.rotation = enemyRotation;
            _agentUpdateRotation = _agent.updateRotation;
            _agent.updateRotation = false;
            _agentStoppingDistance = _agent.stoppingDistance;
            _agent.stoppingDistance = 0f;
            Vector3 look = Vector3.ProjectOnPlane(_approachTarget - _pronePosition, Vector3.up).normalized;
            _proneRotation = Quaternion.LookRotation(look, Vector3.up) * Quaternion.Euler(0, 0, -20f);
            overlay.alpha = 0f;
            PlayScream();
            ChangePhase(IntroPhase.Falling);
        }

        /// <summary>壁で遮られないNavMesh上の接近経路を選択</summary>
        /// <returns>敵の配置と経路が確保できた場合はtrue</returns>
        private bool TryPlaceEnemy()
        {
            if (!NavMesh.SamplePosition(_player.transform.position, out var origin, 3f, NavMesh.AllAreas)) return false;
            Vector3 forward = Vector3.ProjectOnPlane(_player.CameraForward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = _player.transform.forward;
            Vector3 best = Vector3.zero;
            float distance = 0f;
            // 足側の約3mから接近を始め、壁がある場合だけ身体の向きを変更
            foreach (float angle in new[] { 180f, 145f, -145f, -75f, 75f, 0f })
            {
                Vector3 direction = Quaternion.Euler(0, angle, 0) * forward;
                Vector3 end = origin.position + direction * 3f;
                if (NavMesh.Raycast(origin.position, end, out var wall, NavMesh.AllAreas)) end = wall.position - direction * .6f;
                if (!NavMesh.SamplePosition(end, out var candidate, .8f, NavMesh.AllAreas)) continue;
                float length = Vector3.Distance(origin.position, candidate.position);
                if (length <= distance || length < stoppingDistance + 1f) continue;
                if (Physics.Linecast(origin.position + Vector3.up, candidate.position + Vector3.up, 1 << 8, QueryTriggerInteraction.Ignore)) continue;
                distance = length;
                best = candidate.position;
                break;
            }
            if (distance < stoppingDistance + 1f) return false;
            Vector3 away = (best - origin.position).normalized;
            // 身体ではなく突き出した顔を接近先に合わせ、カメラを通り越さない距離を確保
            Vector3 side = Vector3.Cross(Vector3.up, away);
            Vector3 faceDirection = (away * stoppingDistance + side * 1.25f).normalized;
            Vector3 cameraPosition = origin.position + Vector3.up * .24f;
            Vector3 faceTarget = cameraPosition + faceDirection * .6f;
            Quaternion rotation = Quaternion.LookRotation(-faceDirection, Vector3.up);
            Vector3 predictedFace = _locomotion.PredictIntroFacePosition(origin.position, rotation, cameraPosition);
            _approachTarget = origin.position + Vector3.ProjectOnPlane(faceTarget - predictedFace, Vector3.up);
            if (!NavMesh.SamplePosition(_approachTarget, out var target, 1f, NavMesh.AllAreas)) return false;
            _approachTarget = target.position;
            if (Physics.Linecast(origin.position + Vector3.up * .5f, _approachTarget + Vector3.up * .5f, 1 << 8, QueryTriggerInteraction.Ignore)) return false;

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(best, _approachTarget, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            _approachCorners = path.corners;
            _approachLength = 0f;
            for (int corner = 1; corner < _approachCorners.Length; corner++)
                _approachLength += Vector3.Distance(_approachCorners[corner - 1], _approachCorners[corner]);
            _agent.enabled = true;
            if (!_agent.Warp(best)) return false;
            _agent.transform.rotation = Quaternion.LookRotation(-away, Vector3.up);
            _agent.isStopped = true;
            return true;
        }

        /// <summary>文字ごとの頂点色をずらして血が染みる変化を描く</summary>
        /// <param name="progress">変色の進行率</param>
        private void CorruptText(float progress)
        {
            message.ForceMeshUpdate();
            var info = message.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                var character = info.characterInfo[i];
                if (!character.isVisible) continue;
                var colors = info.meshInfo[character.materialReferenceIndex].colors32;
                float delay = Mathf.Repeat(i * .371f, 1f) * .35f;
                for (int corner = 0; corner < 4; corner++)
                {
                    float stain = Mathf.SmoothStep(0, 1, (progress - delay + corner * .04f) / .65f);
                    colors[character.vertexIndex + corner] = Color.Lerp(Color.white, bloodColor, stain);
                }
            }
            message.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        /// <summary>指定MP3をFMODで読み込み左耳側へ再生</summary>
        private void PlayScream()
        {
            var system = FMODUnity.RuntimeManager.CoreSystem;
            var result = system.createSound(Path.Combine(UnityEngine.Application.streamingAssetsPath, screamFile),
                FMOD.MODE._2D | FMOD.MODE.CREATESAMPLE, out _scream);
            if (result != FMOD.RESULT.OK)
            {
                Debug.LogError("StageIntro scream: " + result, this);
                return;
            }
            // 既存のSEバスを通し、音量設定とマスターミュートを引き継ぐ
            FMOD.ChannelGroup group = default;
            var studio = FMODUnity.RuntimeManager.StudioSystem;
            if (studio.getBus("bus:/SE", out _screamBus) == FMOD.RESULT.OK &&
                _screamBus.lockChannelGroup() == FMOD.RESULT.OK)
            {
                _busLocked = true;
                studio.flushCommands();
                _screamBus.getChannelGroup(out group);
            }
            result = system.playSound(_scream, group, true, out _screamChannel);
            if (result != FMOD.RESULT.OK)
            {
                Debug.LogError("StageIntro scream playback: " + result, this);
                return;
            }
            _screamChannel.setPan(screamPan);
            _screamChannel.setVolume(screamVolume);
            _screamChannel.setPaused(false);
        }

        /// <summary>叫び声と専用に確保したバス参照を解放</summary>
        private void ReleaseScream()
        {
            if (_screamChannel.hasHandle()) { _screamChannel.stop(); _screamChannel.clearHandle(); }
            if (_scream.hasHandle()) { _scream.release(); _scream.clearHandle(); }
            if (_busLocked) { _screamBus.unlockChannelGroup(); _busLocked = false; }
        }

        /// <summary>段階を切り替え、経過時間をリセット</summary>
        /// <param name="phase">次の演出段階</param>
        private void ChangePhase(IntroPhase phase)
        {
            Phase = phase;
            _elapsed = 0f;
        }

        /// <summary>案内の文字色を初期化して表示</summary>
        /// <param name="text">表示文字列</param>
        private void ShowMessage(string text)
        {
            message.text = text;
            message.color = Color.white;
            overlay.alpha = 1f;
        }

        /// <summary>演出中のカーソルを非表示に保つ</summary>
        public void HideCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>敵モデルの元の描画状態を保ちながら表示を切り替える</summary>
        /// <param name="visible">敵を表示するか</param>
        private void SetEnemyVisible(bool visible)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = visible && _rendererStates[i];
        }

        /// <summary>登場前の不可視の衝突と演出中の押し合いを防ぐ</summary>
        /// <param name="enabled">元の衝突状態へ戻すか</param>
        private void SetEnemyCollisions(bool enabled)
        {
            if (_enemyColliders == null) return;
            for (int i = 0; i < _enemyColliders.Length; i++)
                if (_enemyColliders[i] != null) _enemyColliders[i].enabled = enabled && _colliderStates[i];
        }

        /// <summary>保存した視点と通常操作を復元</summary>
        private void ReleaseControl()
        {
            if (!_ownsControl) return;
            _ownsControl = false;
            ReleaseImpact();
            if (_locomotion != null) _locomotion.SetIntroPose(0f);
            if (_agent != null)
            {
                _agent.updateRotation = _agentUpdateRotation;
                _agent.stoppingDistance = _agentStoppingDistance;
            }
            if (_player != null)
            {
                _player.IsCinematicControlled = false;
                _player.MainCamera.transform.SetPositionAndRotation(_standingPosition, _standingRotation);
                _player.MainCamera.cullingMask = _cameraMask;
                _player.MainCamera.nearClipPlane = _nearClipPlane;
                _player.MainCamera.fieldOfView = _fieldOfView;
            }
            if (gameplayHud != null) gameplayHud.enabled = _hudEnabled;
            if (_brain != null) _brain.enabled = _brainEnabled;
            ControlChanged?.Invoke(false);
        }

        /// <summary>中断時に操作と表示、音声の資源を復元</summary>
        public void Cancel()
        {
            bool unfinished = _begun && Phase != IntroPhase.Complete;
            ReleaseControl();
            if (overlay != null) overlay.alpha = 0f;
            SetEnemyVisible(true);
            SetEnemyCollisions(true);
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.speed = _agentSpeed;
            ReleaseScream();
            ChangePhase(IntroPhase.Complete);
            if (unfinished) Finished?.Invoke();
        }

        /// <summary>シーン離脱や無効化でも演出の占有を残さない</summary>
        private void OnDisable() => Cancel();
    }
}
