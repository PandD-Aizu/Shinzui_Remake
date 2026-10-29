using System.Collections.Generic;
using Shinzui.View.Flashlight;
using UnityEngine;

namespace Shinzui.View
{
    /// <summary>主観用の全身モデルと懐中電灯を握る骨格姿勢を制御</summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class PlayerBodyView : MonoBehaviour
    {
        [SerializeField] private Transform model;
        [SerializeField] private SkinnedMeshRenderer headRenderer;
        [SerializeField] private bool showHead;
        [SerializeField] private PlayerView player;
        [SerializeField] private FlashlightView flashlight;
        [SerializeField] private Vector3 gripWristOffset = new Vector3(-.02f, -.065f, -.22f);
        [SerializeField, Range(0f, 1f)] private float gripStrength = 1f;

        [Header("Free Right Arm")]
        [SerializeField, Min(0f)] private float walkingArmSwing = .15f;
        [SerializeField, Min(0f)] private float runningArmSwing = .23f;
        [SerializeField, Min(0f)] private float idleArmSway = .008f;

        [Header("Pickup Torso")]
        [SerializeField, Range(0f, 60f)] private float pickupForwardLean = 40f;
        [SerializeField, Range(0f, 20f)] private float pickupSideLean = 12f;
        [SerializeField, Range(0f, 85f)] private float pickupTotalForwardLean = 75f;
        [SerializeField, Min(.01f)] private float pickupLeanSmoothTime = .1f;

        [Header("Gait")]
        [SerializeField, Min(.1f)] private float walkReferenceSpeed = 1.45f;
        [SerializeField, Min(.1f)] private float runReferenceSpeed = 3.8f;
        [SerializeField, Min(.1f)] private float walkStepsPerSecond = 1.8f;
        [SerializeField, Min(.1f)] private float runStepsPerSecond = 2.7f;
        [SerializeField, Min(0f)] private float maxFootReach = .3f;

        [Header("Crouch")]
        [SerializeField, Range(0f, 60f)] private float crouchLeanAngle = 38f;
        [SerializeField, Min(0f)] private float crouchFootForward = .12f;
        [SerializeField, Min(0f)] private float crouchFootSpread = .05f;
        [SerializeField, Min(0f)] private float crouchFootStagger = .06f;
        [SerializeField, Range(0f, 45f)] private float crouchHeelRaise = 22f;

        [Header("Fear")]
        [SerializeField, Min(0f)] private float calmTremorAngle = .12f;
        [SerializeField, Min(0f)] private float chasedTremorAngle = 1.3f;
        [SerializeField, Min(1f)] private float tremorFrequency = 9f;
        [SerializeField, Min(0f)] private float chasedLeanAngle = 7f;
        [SerializeField, Min(0f)] private float chasedShoulderRaise = 6f;
        [SerializeField, Min(.1f)] private float fearBlendSpeed = 2.5f;

        private readonly Dictionary<string, Transform> _bones = new();
        private readonly Dictionary<Transform, Quaternion> _restRotations = new();
        private readonly List<FingerChain> _leftFingers = new();
        private readonly List<FingerChain> _rightFingers = new();
        private Vector3 _hipPosition;
        private Vector3 _leftFootPosition;
        private Vector3 _rightFootPosition;
        private Quaternion _leftFootRotation;
        private Quaternion _rightFootRotation;
        private Quaternion _handBasis;
        private float _standingEyeHeight;
        private float _phase;
        private float _stride;
        private float _footReach;
        private float _crouchWeight;
        private float _fear;
        private float _tremorTime;
        private Vector2 _tremor;
        private float _idlePhase;
        private float _armRunWeight;
        private Vector3 _rightHandOffset = new Vector3(.24f, .99f, .04f);
        private bool _initialized;
        private Vector3 _pickupPosition;
        private Quaternion _pickupRotation;
        private Quaternion _rightHandBasis;
        private float _pickupWeight;
        private float _pickupGripStrength;
        private Camera _pickupCamera;
        private float _pickupNearClip;
        private Vector3 _pickupBaseHips;
        private Vector3 _pickupBaseSpine;
        private Vector3 _pickupBaseChest;
        private Vector3 _pickupBaseShoulder;
        private float _pickupArmLength;
        private Vector2 _pickupLean;
        private Vector2 _pickupLeanVelocity;
        private Vector3 _pickupEyeOffset;

        /// <summary>取得時の上半身の前傾による視点の移動量</summary>
        public Vector3 PickupEyeOffset => isActiveAndEnabled && model != null
            ? model.TransformVector(_pickupEyeOffset) : Vector3.zero;

        /// <summary>右手の取得姿勢を設定し、通常の腕振りと合成</summary>
        /// <param name="position">掴む位置</param>
        /// <param name="rotation">指先を前、掌の甲を上とした向き</param>
        /// <param name="progress">手を伸ばす割合</param>
        /// <param name="grip">指を握る割合</param>
        public void SetPickupPose(Vector3 position, Quaternion rotation, float progress, float grip)
        {
            _pickupPosition = position;
            _pickupRotation = rotation;
            _pickupWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / .85f));
            _pickupGripStrength = Mathf.Clamp01(grip);

            // 近距離の手がニアクリップで消えないよう、取得中だけ描画開始位置を寄せる
            if (_pickupWeight > 0f && _pickupCamera == null && player != null && player.MainCamera != null)
            {
                _pickupCamera = player.MainCamera;
                _pickupNearClip = _pickupCamera.nearClipPlane;
                _pickupCamera.nearClipPlane = Mathf.Min(_pickupNearClip, .03f);
            }
            if (_pickupWeight <= 0f) RestorePickupCamera();
        }

        /// <summary>取得開始前のカメラ描画距離へ戻す</summary>
        private void RestorePickupCamera()
        {
            if (_pickupCamera == null) return;
            _pickupCamera.nearClipPlane = _pickupNearClip;
            _pickupCamera = null;
        }

        /// <summary>無効化時にも取得用のカメラ設定を解除</summary>
        private void OnDisable()
        {
            _pickupWeight = 0f;
            _pickupLean = Vector2.zero;
            _pickupLeanVelocity = Vector2.zero;
            _pickupEyeOffset = Vector3.zero;
            RestorePickupCamera();
        }

        /// <summary>上半身の可動域と腕の長さから取得可能か判定</summary>
        /// <param name="position">掴む位置</param>
        /// <param name="rotation">指定した持ち手の向き</param>
        /// <returns>骨を伸ばさずに届くか</returns>
        public bool CanReachPickup(Vector3 position, Quaternion? rotation = null)
        {
            InitializePose();
            if (!_initialized) return false;
            Vector3 forward = rotation.HasValue ? rotation.Value * Vector3.forward
                : (position - player.CameraPosition).normalized;
            return FindPickupLean(position - forward * .09f, out _);
        }

        /// <summary>取得の前傾を含まない姿勢をモデル座標で保存</summary>
        private void CachePickupBasePose()
        {
            _pickupBaseHips = model.InverseTransformPoint(_bones["Hips"].position);
            _pickupBaseSpine = model.InverseTransformPoint(_bones["Spine"].position);
            _pickupBaseChest = model.InverseTransformPoint(_bones["Chest"].position);
            _pickupBaseShoulder = model.InverseTransformPoint(_bones["RightUpperArm"].position);
            _pickupArmLength = Vector3.Distance(_bones["RightUpperArm"].position, _bones["RightLowerArm"].position)
                + Vector3.Distance(_bones["RightLowerArm"].position, _bones["RightHand"].position) - .005f;
        }

        /// <summary>腰・背骨・胸へ分散する前傾と側屈の回転を作成</summary>
        /// <param name="lean">前傾角度と右方向の側屈角度</param>
        /// <param name="weight">関節への配分</param>
        /// <returns>ワールド座標の回転</returns>
        private Quaternion PickupLeanRotation(Vector2 lean, float weight)
        {
            return Quaternion.AngleAxis(-lean.y * weight, model.forward)
                * Quaternion.AngleAxis(lean.x * weight, model.right);
        }

        /// <summary>ボーンを変更せず前傾後の右肩位置を予測</summary>
        /// <param name="lean">前傾角度と側屈角度</param>
        /// <returns>予測した右肩の位置</returns>
        private Vector3 PredictPickupShoulder(Vector2 lean)
        {
            Vector3 hips = model.TransformPoint(_pickupBaseHips);
            Vector3 spine = model.TransformPoint(_pickupBaseSpine);
            Vector3 chest = model.TransformPoint(_pickupBaseChest);
            Vector3 shoulder = model.TransformPoint(_pickupBaseShoulder);
            Quaternion hipRotation = PickupLeanRotation(lean, .55f);
            Quaternion spineRotation = PickupLeanRotation(lean, .3f);
            Quaternion chestRotation = PickupLeanRotation(lean, .15f);
            Vector3 movedSpine = hips + hipRotation * (spine - hips);
            Vector3 movedChest = movedSpine + spineRotation * hipRotation * (chest - spine);
            return movedChest + chestRotation * spineRotation * hipRotation * (shoulder - chest);
        }

        /// <summary>可動域内で届く最小限の前傾を探索</summary>
        /// <param name="wrist">手首の目標位置</param>
        /// <param name="lean">到達可能な姿勢、または最も近づく姿勢</param>
        /// <returns>腕の可動範囲まで肩を寄せられるか</returns>
        private bool FindPickupLean(Vector3 wrist, out Vector2 lean)
        {
            // しゃがみと怯えの前傾を上限から差し引く
            float forwardLimit = Mathf.Min(pickupForwardLean,
                Mathf.Max(0f, pickupTotalForwardLean - crouchLeanAngle * _crouchWeight - chasedLeanAngle * _fear));
            float sideSign = Mathf.Sign(model.InverseTransformPoint(wrist).x - _pickupBaseShoulder.x);
            float bestDistance = float.PositiveInfinity;
            float bestCost = float.PositiveInfinity;
            bool reachable = false;
            lean = Vector2.zero;
            for (int forward = 0; forward <= 24; forward++)
            for (int side = 0; side <= 4; side++)
            {
                Vector2 candidate = new Vector2(forwardLimit * forward / 24f, pickupSideLean * side * sideSign / 4f);
                float distance = Vector3.Distance(PredictPickupShoulder(candidate), wrist);
                float cost = candidate.x * candidate.x + candidate.y * candidate.y * 4f;
                if (distance <= _pickupArmLength)
                {
                    if (!reachable || cost < bestCost) { lean = candidate; bestCost = cost; }
                    reachable = true;
                }
                else if (!reachable && distance < bestDistance)
                {
                    lean = candidate;
                    bestDistance = distance;
                }
            }
            return reachable;
        }

        /// <summary>取得進捗に合わせて上半身を傾け、終了時は滑らかに戻す</summary>
        /// <param name="deltaTime">経過秒数</param>
        private void PosePickupTorso(float deltaTime)
        {
            Vector2 target = Vector2.zero;
            if (_pickupWeight > 0f)
            {
                FindPickupLean(_pickupPosition - _pickupRotation * Vector3.forward * .09f, out target);
                target *= _pickupWeight;
            }
            _pickupLean = Vector2.SmoothDamp(_pickupLean, target, ref _pickupLeanVelocity,
                pickupLeanSmoothTime, Mathf.Infinity, deltaTime);
            _pickupLean.x = Mathf.Clamp(_pickupLean.x, 0f, Mathf.Min(pickupForwardLean,
                Mathf.Max(0f, pickupTotalForwardLean - crouchLeanAngle * _crouchWeight - chasedLeanAngle * _fear)));
            _pickupLean.y = Mathf.Clamp(_pickupLean.y, -pickupSideLean, pickupSideLean);

            // 取得前傾だけによる頭部の移動を保存し、しゃがみや呼吸を二重に加算しない
            Vector3 headBefore = _bones["Head"].position;

            // 骨盤から順に回し、首は反対へ戻して頭部の過度な倒れ込みを抑える
            _bones["Hips"].rotation = PickupLeanRotation(_pickupLean, .55f) * _bones["Hips"].rotation;
            _bones["Spine"].rotation = PickupLeanRotation(_pickupLean, .3f) * _bones["Spine"].rotation;
            _bones["Chest"].rotation = PickupLeanRotation(_pickupLean, .15f) * _bones["Chest"].rotation;
            _bones["Neck"].rotation = Quaternion.Inverse(PickupLeanRotation(_pickupLean, .6f)) * _bones["Neck"].rotation;
            _pickupEyeOffset = model.InverseTransformVector(_bones["Head"].position - headBefore);
        }

        /// <summary>指の関節と手首基準の曲げ軸を保持</summary>
        private struct FingerChain
        {
            public Transform Proximal;
            public Transform Intermediate;
            public Transform Distal;
            public Vector3 Axis;
            public float Strength;
        }

        /// <summary>モデルとプレイヤーの参照を設定してバインド姿勢を保存</summary>
        /// <param name="owner">移動を担当するプレイヤー</param>
        /// <param name="heldLight">既存の懐中電灯</param>
        /// <param name="rig">骨格モデルのルート</param>
        /// <param name="head">独立した頭部レンダラー</param>
        public void Configure(PlayerView owner, FlashlightView heldLight, Transform rig, SkinnedMeshRenderer head)
        {
            player = owner;
            flashlight = heldLight;
            model = rig;
            headRenderer = head;
            _initialized = false;
            InitializePose();
        }

        /// <summary>保存済み参照から骨格を初期化</summary>
        private void Awake()
        {
            InitializePose();
        }

        /// <summary>頭部だけの表示を切り替え、胴体や骨格は維持</summary>
        /// <param name="visible">頭部を表示するか</param>
        public void SetHeadVisible(bool visible)
        {
            showHead = visible;
            if (headRenderer != null) headRenderer.enabled = visible;
        }

        /// <summary>変形前の関節姿勢と視点の基準高さを保存</summary>
        private void InitializePose()
        {
            if (_initialized || model == null || player == null) return;

            // 通常時から頭部以外のモデルを表示
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer != headRenderer) renderer.enabled = true;

            // バインド姿勢を基準に毎フレームの回転を再構成
            _bones.Clear();
            _restRotations.Clear();
            foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
            {
                _bones[bone.name] = bone;
                _restRotations[bone] = bone.localRotation;
            }
            _hipPosition = _bones["Hips"].localPosition;
            _leftFootPosition = model.InverseTransformPoint(_bones["LeftFoot"].position);
            _rightFootPosition = model.InverseTransformPoint(_bones["RightFoot"].position);
            _leftFootRotation = Quaternion.Inverse(model.rotation) * _bones["LeftFoot"].rotation;
            _rightFootRotation = Quaternion.Inverse(model.rotation) * _bones["RightFoot"].rotation;

            // 指の伸びる方向と掌の向きから、FBXの骨軸に依存しない握り基準を作成
            Transform hand = _bones["LeftHand"];
            Vector3 fingers = hand.InverseTransformDirection(_bones["LeftMiddleProximal"].position - hand.position);
            Vector3 palm = hand.InverseTransformDirection(-model.up);
            _handBasis = Quaternion.Inverse(Quaternion.LookRotation(fingers, palm));
            Transform right = _bones["RightHand"];
            _rightHandBasis = Quaternion.Inverse(Quaternion.LookRotation(
                right.InverseTransformDirection(_bones["RightMiddleProximal"].position - right.position),
                right.InverseTransformDirection(model.up)));
            CacheFingers("Left", _leftFingers);
            CacheFingers("Right", _rightFingers);
            _standingEyeHeight = player.transform.InverseTransformPoint(player.PostureEyePosition).y;
            CachePickupBasePose();
            _initialized = true;
            SetHeadVisible(showHead);
        }

        /// <summary>カメラと懐中電灯の更新後に手足を追従</summary>
        private void LateUpdate()
        {
            UpdatePose(Time.deltaTime);
            // Cinemachineが更新したレンズへ取得中の近距離描画を最後に反映
            if (_pickupCamera != null) _pickupCamera.nearClipPlane = Mathf.Min(_pickupNearClip, .03f);
        }

        /// <summary>足の接地と持ち手への腕IKを更新</summary>
        /// <param name="deltaTime">経過秒数</param>
        public void UpdatePose(float deltaTime)
        {
            InitializePose();
            if (!_initialized) return;

            // 前フレームの変形を累積せず、しゃがみ量を骨盤へ適用
            foreach (var pair in _restRotations) pair.Key.localRotation = pair.Value;
            // カメラ演出による視点の下降をしゃがみとして扱わず、追従先の高さを参照
            float eyeHeight = player.transform.InverseTransformPoint(player.PostureEyePosition).y;
            float crouch = Mathf.Clamp(_standingEyeHeight - eyeHeight, 0f, .8f);
            Transform hips = _bones["Hips"];
            hips.localPosition = _hipPosition;
            PoseCrouch(hips, crouch);
            SetHeadVisible(showHead);

            // スタミナ低下と追跡で怯えを強め、手の震えと身を縮める姿勢へ反映
            float targetFear = player.IsChased ? 1f : (1f - player.StaminaRatio) * .5f;
            _fear = Mathf.MoveTowards(_fear, targetFear, deltaTime * fearBlendSpeed);
            UpdateTremor(deltaTime);
            PoseFear(hips);
            CachePickupBasePose();
            PosePickupTorso(deltaTime);

            // 実速度に合わせて足を運び、しゃがみ中も膝を曲げて接地
            Vector3 velocity = player.CurrentVelocity;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            // 歩数と歩幅を実速度から求め、足裏が床を滑らないように運ぶ
            float stepsPerSecond = GetStepsPerSecond(speed);
            _stride = Mathf.MoveTowards(_stride, Mathf.InverseLerp(.05f, .6f, speed), deltaTime * 4f);
            _footReach = Mathf.MoveTowards(_footReach,
                Mathf.Min(maxFootReach, speed / stepsPerSecond * .4f) * Mathf.Lerp(1f, .7f, _crouchWeight),
                deltaTime * 1.5f);
            _phase = Mathf.Repeat(_phase + deltaTime * stepsPerSecond * Mathf.PI * _stride, Mathf.PI * 2f);
            PoseLeg("Left", _leftFootPosition, _leftFootRotation, _phase);
            PoseLeg("Right", _rightFootPosition, _rightFootRotation, _phase + Mathf.PI);

            // 空いた右腕には歩調と呼吸の動きを加え、左腕は持ち手へ追従
            PoseRightArm(speed, crouch, deltaTime);

            Transform light = flashlight != null ? flashlight.HeldLightTransform : null;
            if (light != null)
            {
                // 懐中電灯の向きは毎フレーム再設定されるため、震えは累積せず光の筋だけ揺れる
                float tremorAngle = Mathf.Lerp(calmTremorAngle, chasedTremorAngle, _fear);
                light.rotation *= Quaternion.Euler(_tremor.x * tremorAngle, _tremor.y * tremorAngle, 0f);
                light.position += (light.right * _tremor.y + light.up * _tremor.x) * tremorAngle * .0015f;

                Transform leftHand = _bones["LeftHand"];
                Vector3 wrist = light.TransformPoint(gripWristOffset);
                Transform upper = _bones["LeftUpperArm"];
                Transform lower = _bones["LeftLowerArm"];

                // 真上を向いた場合も腕を引き伸ばさず、ライトごと届く範囲へ寄せる
                float reach = Vector3.Distance(upper.position, lower.position)
                    + Vector3.Distance(lower.position, leftHand.position) - .005f;
                Vector3 offset = wrist - upper.position;
                if (offset.sqrMagnitude > reach * reach)
                {
                    Vector3 reachable = upper.position + offset.normalized * reach;
                    light.position += reachable - wrist;
                    wrist = reachable;
                }
                Vector3 pole = player.CameraPosition + player.CameraRight * -.38f
                    - Vector3.up * .55f - player.CameraForward * .08f;
                SolveLimb(upper, lower, leftHand, wrist, pole);
                leftHand.rotation = Quaternion.LookRotation(light.up, light.right) * _handBasis;
                CurlFingers(leftHand, _leftFingers, gripStrength);
            }
        }

        /// <summary>待機中の呼吸と歩調に合わせて右腕・手首・指を動かす</summary>
        /// <param name="speed">実際の水平移動速度</param>
        /// <param name="crouch">視点の下降量</param>
        /// <param name="deltaTime">経過秒数</param>
        private void PoseRightArm(float speed, float crouch, float deltaTime)
        {
            // 静止時も緩やかに揺らし、走行姿勢への切り替えを滑らかにつなぐ
            _idlePhase = Mathf.Repeat(_idlePhase + deltaTime * Mathf.Lerp(1.8f, 4.2f, _fear), Mathf.PI * 4f);
            float blend = 1f - Mathf.Exp(-8f * deltaTime);
            _armRunWeight = Mathf.Lerp(_armRunWeight, Mathf.InverseLerp(walkReferenceSpeed + .7f, runReferenceSpeed, speed), blend);
            float crouchWeight = Mathf.Clamp01(crouch / .8f);
            float breath = Mathf.Sin(_idlePhase);
            float idle = idleArmSway * (1f - _stride * .65f);

            // 右脚と逆位相で腕を振り、走行では肘を曲げ、しゃがみ中は振幅を抑える
            float swing = Mathf.Cos(_phase) * _stride
                * Mathf.Lerp(walkingArmSwing, runningArmSwing, _armRunWeight)
                * Mathf.Lerp(1f, .55f, crouchWeight);
            Vector3 target = new Vector3(
                .24f + Mathf.Sin(_idlePhase * .5f) * idle,
                .99f + _armRunWeight * .10f + crouchWeight * .06f
                    + breath * idle + Mathf.Max(0f, swing) * .2f,
                .04f + swing + Mathf.Sin(_idlePhase + .7f) * idle);
            _rightHandOffset = Vector3.Lerp(_rightHandOffset, target, blend);
            Vector3 elbow = new Vector3(.43f, 1.15f + _armRunWeight * .05f, -.10f + swing * .45f);
            Transform hand = _bones["RightHand"];
            Vector3 restTarget = model.TransformPoint(_rightHandOffset) - model.up * crouch
                + model.forward * (.1f * _crouchWeight);
            Vector3 pickupWrist = _pickupPosition - _pickupRotation * Vector3.forward * .09f;
            SolveLimb(_bones["RightUpperArm"], _bones["RightLowerArm"], hand,
                Vector3.Lerp(restTarget, pickupWrist, _pickupWeight),
                Vector3.Lerp(model.TransformPoint(elbow) - model.up * crouch,
                    player.CameraPosition + player.CameraRight * .45f - Vector3.up * .45f, _pickupWeight));

            // 手首を少し遅れて揺らし、指も握り切らず緩やかに曲げ伸ばし
            float wristSwing = Mathf.Sin(_phase - .4f) * _stride * 5f + breath * 1.2f
                + _tremor.x * Mathf.Lerp(calmTremorAngle, chasedTremorAngle, _fear) * 2f;
            hand.rotation = Quaternion.AngleAxis(wristSwing, model.right) * hand.rotation;
            hand.rotation = Quaternion.Slerp(hand.rotation, _pickupRotation * _rightHandBasis, _pickupWeight);
            CurlFingers(hand, _rightFingers, Mathf.Lerp(.18f + _armRunWeight * .10f + breath * .018f,
                _pickupGripStrength, _pickupWeight));
        }

        /// <summary>
        /// 視点の下降量に合わせて骨盤を落とし、上体を前傾させてしゃがむ
        /// </summary>
        /// <param name="hips">骨盤</param>
        /// <param name="crouch">視点の下降量</param>
        private void PoseCrouch(Transform hips, float crouch)
        {
            // 完全にしゃがんだ時の視点下降量を基準に、しゃがみの深さを0〜1で求める
            float fullDrop = (_standingEyeHeight - transform.localPosition.y) * (1f - player.CrouchRatio);
            _crouchWeight = Mathf.Clamp01(crouch / Mathf.Clamp(fullDrop, .01f, .8f));
            if (_crouchWeight <= 0f) return;

            // 前傾で下がる分だけ骨盤の下げ幅を減らし、頭が前へ出る分だけ骨盤を後ろへ引く
            float lean = crouchLeanAngle * _crouchWeight;
            float torso = Vector3.Distance(hips.position, _bones["Head"].position);
            float leanRad = lean * Mathf.Deg2Rad;
            float drop = Mathf.Max(0f, crouch - torso * (1f - Mathf.Cos(leanRad)));
            hips.position -= model.up * drop + model.forward * (torso * Mathf.Sin(leanRad) * .85f);

            // 骨盤から胸へ前傾を分散し、首を起こして目線を前へ保つ
            Vector3 axis = model.right;
            hips.rotation = Quaternion.AngleAxis(lean * .3f, axis) * hips.rotation;
            _bones["Spine"].rotation = Quaternion.AngleAxis(lean * .35f, axis) * _bones["Spine"].rotation;
            _bones["Chest"].rotation = Quaternion.AngleAxis(lean * .35f, axis) * _bones["Chest"].rotation;
            _bones["Neck"].rotation = Quaternion.AngleAxis(-lean * .6f, axis) * _bones["Neck"].rotation;
        }

        /// <summary>
        /// 周期の異なるノイズを重ね、規則性のない手の震えを求める
        /// </summary>
        /// <param name="deltaTime">経過秒数</param>
        private void UpdateTremor(float deltaTime)
        {
            _tremorTime += deltaTime * tremorFrequency;
            _tremor = new Vector2(
                (Mathf.PerlinNoise(_tremorTime, 3.1f) - .5f) * 2f,
                (Mathf.PerlinNoise(7.7f, _tremorTime * 1.13f) - .5f) * 2f);
        }

        /// <summary>
        /// 怯えに応じて背を丸め、肩をすくめ、呼吸で胸を上下させる
        /// </summary>
        /// <param name="hips">骨盤</param>
        private void PoseFear(Transform hips)
        {
            // 前傾で胸が視点より前へ出ないよう、骨盤を後ろへ引いて重心を落とす
            float lean = chasedLeanAngle * _fear;
            hips.position -= model.forward * (lean * .007f) + model.up * (_fear * .03f);

            // 背骨へ前傾を分散し、浅く速い呼吸の膨らみを胸へ加える
            float breath = Mathf.Sin(_idlePhase) * Mathf.Lerp(.6f, 1.6f, _fear);
            _bones["Spine"].rotation = Quaternion.AngleAxis(lean * .4f, model.right) * _bones["Spine"].rotation;
            _bones["Chest"].rotation = Quaternion.AngleAxis(lean * .6f - breath, model.right) * _bones["Chest"].rotation;

            // 肩をすくめ、吸気でわずかに持ち上げる
            float shrug = chasedShoulderRaise * _fear + Mathf.Max(0f, breath) * .5f;
            _bones["LeftShoulder"].rotation = Quaternion.AngleAxis(-shrug, model.forward) * _bones["LeftShoulder"].rotation;
            _bones["RightShoulder"].rotation = Quaternion.AngleAxis(shrug, model.forward) * _bones["RightShoulder"].rotation;
        }

        /// <summary>
        /// 実速度から1秒あたりの歩数を求める
        /// </summary>
        /// <param name="speed">水平移動速度</param>
        /// <returns>1秒あたりの歩数</returns>
        private float GetStepsPerSecond(float speed)
        {
            // 歩き未満はしゃがみ歩きの遅い歩調、歩き以上は走りの速い歩調へ補間
            if (speed < walkReferenceSpeed)
                return Mathf.Lerp(walkStepsPerSecond * .72f, walkStepsPerSecond, Mathf.InverseLerp(.4f, walkReferenceSpeed, speed));
            return Mathf.Lerp(walkStepsPerSecond, runStepsPerSecond, Mathf.InverseLerp(walkReferenceSpeed, runReferenceSpeed, speed));
        }

        /// <summary>基準の足首位置を移動させて膝IKを解く</summary>
        /// <param name="side">左右の接頭辞</param>
        /// <param name="restPosition">モデル内の足首位置</param>
        /// <param name="restRotation">モデル内の足首回転</param>
        /// <param name="phase">歩行周期</param>
        private void PoseLeg(string side, Vector3 restPosition, Quaternion restRotation, float phase)
        {
            // 足を前へ戻す遊脚期だけ持ち上げ、後ろへ送る立脚期は接地させる
            // しゃがみ中はすり足気味に低く運ぶ
            float lift = Mathf.Lerp(.06f + _armRunWeight * .06f, .035f, _crouchWeight);
            Vector3 step = new Vector3(0f, Mathf.Max(0f, -Mathf.Sin(phase)) * lift,
                Mathf.Cos(phase) * _footReach) * _stride;

            // しゃがみ中は足を前へ置いて左右に開き、左足を前・右足を後ろに互い違いにする
            float sign = side == "Left" ? -1f : 1f;
            float heel = sign > 0f ? _crouchWeight * (1f - _stride) : 0f;
            Vector3 stance = new Vector3(sign * crouchFootSpread,
                heel * .05f,
                crouchFootForward - sign * crouchFootStagger) * _crouchWeight;

            // 膝はつま先と同じく外へ開き、後ろ足はかかとを浮かせてつま先で支える
            Transform foot = _bones[side + "Foot"];
            Vector3 target = model.TransformPoint(restPosition + stance + step);
            Vector3 pole = target + model.forward * .7f + model.up * .35f
                + model.right * (sign * .3f * _crouchWeight);
            SolveLimb(_bones[side + "UpperLeg"], _bones[side + "LowerLeg"], foot, target, pole);
            foot.rotation = Quaternion.AngleAxis(crouchHeelRaise * heel, model.right) * model.rotation * restRotation;
        }

        /// <summary>骨の長さを保った二関節IKで手首または足首を配置</summary>
        /// <param name="upper">上腕または大腿</param>
        /// <param name="lower">前腕または下腿</param>
        /// <param name="end">手首または足首</param>
        /// <param name="target">末端の目標位置</param>
        /// <param name="pole">肘または膝を向ける位置</param>
        private static void SolveLimb(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
        {
            Vector3 origin = upper.position;
            float a = Vector3.Distance(origin, lower.position);
            float b = Vector3.Distance(lower.position, end.position);
            Vector3 direction = target - origin;
            float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
            direction.Normalize();
            Vector3 bend = Vector3.ProjectOnPlane(pole - origin, direction).normalized;
            float along = (a * a - b * b + distance * distance) / (2f * distance);
            Vector3 elbow = origin + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));

            // 上流から順に回転させ、子ボーンの移動後に前腕を合わせる
            upper.rotation = Quaternion.FromToRotation(lower.position - origin, elbow - origin) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position,
                origin + direction * distance - lower.position) * lower.rotation;
        }

        /// <summary>指の参照と曲げ軸を初期化時に保存</summary>
        /// <param name="side">左右の接頭辞</param>
        /// <param name="chains">保存先の指一覧</param>
        private void CacheFingers(string side, List<FingerChain> chains)
        {
            Transform hand = _bones[side + "Hand"];
            chains.Clear();
            foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            {
                Transform first = _bones[side + finger + "Proximal"];
                Transform second = _bones[side + finger + "Intermediate"];
                chains.Add(new FingerChain
                {
                    Proximal = first,
                    Intermediate = second,
                    Distal = _bones[side + finger + "Distal"],
                    Axis = hand.InverseTransformDirection(Vector3.Cross(second.position - first.position, -model.up).normalized),
                    Strength = finger == "Thumb" ? .55f : 1f
                });
            }
        }

        /// <summary>各指の三関節を掌側へ曲げて円筒の持ち手を包む</summary>
        /// <param name="hand">手首</param>
        /// <param name="chains">初期化済みの指一覧</param>
        /// <param name="strength">握りの強さ</param>
        private static void CurlFingers(Transform hand, List<FingerChain> chains, float strength)
        {
            foreach (FingerChain finger in chains)
            {
                Vector3 axis = hand.TransformDirection(finger.Axis);
                float weight = strength * finger.Strength;
                finger.Proximal.rotation = Quaternion.AngleAxis(35f * weight, axis) * finger.Proximal.rotation;
                finger.Intermediate.rotation = Quaternion.AngleAxis(65f * weight, axis) * finger.Intermediate.rotation;
                finger.Distal.rotation = Quaternion.AngleAxis(55f * weight, axis) * finger.Distal.rotation;
            }
        }
    }
}
