using UnityEngine;
using Unity.Cinemachine;

namespace Shinzui.View
{
    public partial class PlayerView
    {
        // 入室演出の各動作が終わる時刻
        private const float EntryReachEnd = .4f, EntryPullEnd = .7f, EntryRegrabEnd = 1f, EntryStepEnd = 1.5f;
        private const float EntryCloseStart = 1.7f, EntryDoorClosed = 1.95f, EntryEnd = 2.2f;
        // 内側から手が届く範囲に収めた扉の最大開度
        private const float EntryDoorOpenMax = .7f;

        public bool IsHiding { get; private set; }
        /// <summary>ロッカーへ入る演出の途中か</summary>
        public bool IsEnteringHiding => _entryLocker != null;
        private Transform _hidingCamera;
        private Vector3 _savedCameraLocal;
        private Quaternion _savedCameraRotation;
        private CinemachineBrain _hidingBrain;
        private bool _brainEnabled, _controllerEnabled;
        private float _savedNear;
        private HidingLockerView _entryLocker;
        private PlayerBodyView _entryBody;
        private float _entryTime;
        private Vector3 _entryStartFeet, _entryStartCameraPosition, _entryVelocity;
        private Quaternion _entryStartCameraRotation;
        private float _entryStartYaw;
        private Vector3 _entryCameraTargetLocal;
        /// <summary>カメラと移動をロッカーへ固定</summary>
        /// <param name="anchor">スリット内側の視点</param>
        /// <param name="feet">本体の足元位置</param>
        public void BeginHiding(Transform anchor, Vector3 feet)
        {
            LockHidingControl(anchor);
            Warp(feet - transform.TransformVector(Vector3.up * _footLocalY) - transform.position, anchor.forward);
            if (characterController != null) characterController.enabled = false;
            UpdateHidingCamera(false);
        }

        /// <summary>手で扉を開け、縁を掴んで体を引き込み、内側から扉を閉める演出付きで隠れる</summary>
        /// <param name="locker">入るロッカー</param>
        public void BeginHiding(HidingLockerView locker)
        {
            LockHidingControl(locker.CameraAnchor);
            if (characterController != null) characterController.enabled = false;

            // 現在の足元と視線を演出の始点として保存
            Transform root = locker.transform;
            _entryLocker = locker;
            _entryBody = GetComponentInChildren<PlayerBodyView>();
            _entryTime = 0f;
            _entryVelocity = Vector3.zero;
            _entryStartFeet = root.InverseTransformPoint(transform.position + transform.TransformVector(Vector3.up * _footLocalY));
            _entryStartFeet.y = 0f;
            Vector3 forward = root.InverseTransformDirection(transform.forward);
            _entryStartYaw = 180f + Mathf.DeltaAngle(180f, Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg);
            _entryStartCameraPosition = mainCamera.transform.position;
            _entryStartCameraRotation = mainCamera.transform.rotation;
            _entryCameraTargetLocal = cameraTarget.localPosition;
            UpdateEntry(0f);
        }

        /// <summary>通常カメラを止めて内部視点の制御へ切り替える</summary>
        /// <param name="anchor">スリット内側の視点</param>
        private void LockHidingControl(Transform anchor)
        {
            _savedCameraLocal = mainCamera.transform.localPosition;
            _savedCameraRotation = mainCamera.transform.localRotation;
            _savedNear = mainCamera.nearClipPlane;
            _hidingBrain = mainCamera.GetComponent<CinemachineBrain>();
            _brainEnabled = _hidingBrain != null && _hidingBrain.enabled;
            if (_hidingBrain != null) _hidingBrain.enabled = false;
            _controllerEnabled = characterController != null && characterController.enabled;
            _hidingCamera = anchor; IsHiding = true; mainCamera.nearClipPlane = .02f;
        }

        /// <summary>息止め時はスリットから身を引く</summary>
        /// <param name="holding">息止め中か</param>
        public void UpdateHidingCamera(bool holding)
        {
            if (!IsHiding || _hidingCamera == null) return;
            if (_entryLocker != null) { UpdateEntry(Time.deltaTime); return; }
            mainCamera.transform.SetPositionAndRotation(_hidingCamera.position - _hidingCamera.forward * (holding ? .15f : 0f), _hidingCamera.rotation);
        }

        /// <summary>入室演出の時刻から足元、向き、右手、扉、視点を求めて反映する</summary>
        /// <param name="deltaTime">経過秒数</param>
        private void UpdateEntry(float deltaTime)
        {
            _entryTime += deltaTime;
            float t = _entryTime;
            Transform root = _entryLocker.transform;
            float side = _entryLocker.FreeSide;

            // 把手の正面へ寄り、扉を引きながら下がり、開口部をくぐって奥へ入る
            Vector3 handleStand = new Vector3(side * .32f, 0f, 1f);
            Vector3 pullStand = new Vector3(side * .4f, 0f, 1.1f);
            Vector3 threshold = new Vector3(side * .12f, 0f, .6f);
            Vector3 feet;
            if (t < EntryReachEnd) feet = Vector3.Lerp(_entryStartFeet, handleStand, Ease(t, 0f, EntryReachEnd));
            else if (t < EntryRegrabEnd) feet = Vector3.Lerp(handleStand, pullStand, Ease(t, EntryReachEnd, EntryPullEnd));
            else
            {
                float u = Ease(t, EntryRegrabEnd, EntryStepEnd);
                feet = Vector3.Lerp(Vector3.Lerp(pullStand, threshold, u), Vector3.Lerp(threshold, Vector3.zero, u), u);
            }

            // 扉を引き寄せるときは体ごと少し前へ乗り出す
            feet.z += .12f * Bump(t, EntryStepEnd, EntryEnd - .1f);

            // ロッカーに正対し、中で開いた扉の側へ振り返って外を向く
            float yaw = Mathf.Lerp(_entryStartYaw, 180f, Ease(t, 0f, EntryReachEnd))
                + side * 180f * Ease(t, 1.1f, 1.65f);
            Vector3 bodyForward = root.TransformDirection(new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad)));
            Vector3 feetWorld = root.TransformPoint(feet);
            if (deltaTime > 0f) _entryVelocity = (feetWorld - EntryFeetWorld()) / deltaTime;
            transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(bodyForward, Vector3.up), Vector3.up);
            transform.position = feetWorld - transform.TransformVector(Vector3.up * _footLocalY);

            // 把手を引いて扉を少し開け、離した勢いで開き切り、内側から引いて閉める
            float door = .3f * Ease(t, .42f, EntryPullEnd)
                + (EntryDoorOpenMax - .3f) * EaseOut(t, EntryPullEnd, 1.1f);
            door = Mathf.Lerp(door, 0f, EaseIn(t, EntryCloseStart, EntryDoorClosed));
            _entryLocker.DriveDoor(door);

            // 右手は把手、開口部の縁、扉の裏面の順に掴み替える
            Vector3 hand; float weight, grip;
            if (t < EntryPullEnd)
            {
                hand = _entryLocker.DoorHandle;
                weight = Ease(t, .1f, EntryReachEnd);
                grip = .9f * Ease(t, .32f, .42f);
            }
            else if (t < EntryCloseStart - .2f)
            {
                float move = Ease(t, .72f, .98f);
                hand = Vector3.Lerp(_entryLocker.DoorHandle, _entryLocker.FrameGrip, move) + Vector3.up * (.08f * Mathf.Sin(Mathf.PI * move));
                weight = 1f - Ease(t, 1.2f, 1.4f);
                grip = Mathf.Lerp(.9f, 0f, Ease(t, .7f, .76f)) + .85f * Ease(t, .92f, 1f);
            }
            else
            {
                hand = _entryLocker.DoorInnerGrip;
                weight = Ease(t, EntryCloseStart - .2f, EntryCloseStart) * (1f - Ease(t, EntryDoorClosed, EntryEnd));
                grip = Ease(t, EntryCloseStart - .08f, EntryCloseStart) * Mathf.Lerp(1f, .2f, Ease(t, EntryDoorClosed, EntryEnd));
            }
            Vector3 eye = feetWorld + transform.TransformVector(Vector3.up * _standingHeadHeightFromFeet)
                + transform.forward * .05f;
            Vector3 fingers = hand - eye;
            Vector3 back = Vector3.ProjectOnPlane(transform.right, fingers);
            if (back.sqrMagnitude < .0001f) back = Vector3.up;
            if (_entryBody != null) _entryBody.SetPickupPose(hand, Quaternion.LookRotation(fingers, back), weight, grip);

            // 枠をくぐる屈みは姿勢の視点を下げて体ごと屈ませる
            float duck = .1f * Bump(t, 1.05f, 1.45f);
            if (cameraTarget != transform) cameraTarget.localPosition = _entryCameraTargetLocal + Vector3.down * duck;

            // 前傾や屈みを反映した頭部の目へ視点を置き、首の切り口や胴体を映さず掴む先へ視線を寄せる
            float step = Mathf.Sin(t * Mathf.PI * 3.6f) * .012f * Bump(t, .1f, EntryStepEnd);
            eye = (_entryBody != null ? _entryBody.EyePosition : eye + Vector3.down * duck) + Vector3.up * step;
            Vector3 look = Vector3.Slerp(bodyForward, (hand - eye).normalized, .55f * weight);
            Quaternion view = Quaternion.LookRotation(look, Vector3.up) * Quaternion.Euler(8f, 0f, 0f);

            // 始点の視線から滑らかに移り、扉が閉まったらスリットの視点へ収める
            float start = Ease(t, 0f, .35f), end = Ease(t, 1.85f, EntryEnd);
            Vector3 position = Vector3.Lerp(Vector3.Lerp(_entryStartCameraPosition, eye, start), _hidingCamera.position, end);
            Quaternion rotation = Quaternion.Slerp(Quaternion.Slerp(_entryStartCameraRotation, view, start), _hidingCamera.rotation, end);
            mainCamera.transform.SetPositionAndRotation(position, rotation);
            if (t < EntryEnd) return;

            // 終了時は通常の隠れ状態と同じ足元と向きへ揃える
            FinishEntry();
            Warp(root.position - transform.TransformVector(Vector3.up * _footLocalY) - transform.position, _hidingCamera.forward);
            if (characterController != null) characterController.enabled = false;
            mainCamera.transform.SetPositionAndRotation(_hidingCamera.position, _hidingCamera.rotation);
        }

        /// <summary>直前フレームの足元位置</summary>
        /// <returns>ワールド座標の足元</returns>
        private Vector3 EntryFeetWorld() => transform.position + transform.TransformVector(Vector3.up * _footLocalY);

        /// <summary>入室演出を止め、右手と扉の制御を手放す</summary>
        private void FinishEntry()
        {
            if (_entryLocker == null) return;
            if (_entryBody != null) _entryBody.SetPickupPose(_entryLocker.DoorInnerGrip, Quaternion.identity, 0f, 0f);
            if (cameraTarget != transform) cameraTarget.localPosition = _entryCameraTargetLocal;
            _entryLocker = null;
            _entryBody = null;
            _entryVelocity = Vector3.zero;
        }

        /// <summary>区間内の進行を加減速付きで求める</summary>
        /// <param name="t">現在時刻</param>
        /// <param name="from">開始時刻</param>
        /// <param name="to">終了時刻</param>
        /// <returns>0から1の進行</returns>
        private static float Ease(float t, float from, float to) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, t));

        /// <summary>区間内の進行を勢いよく始めて減速させる</summary>
        /// <param name="t">現在時刻</param>
        /// <param name="from">開始時刻</param>
        /// <param name="to">終了時刻</param>
        /// <returns>0から1の進行</returns>
        private static float EaseOut(float t, float from, float to)
        { float u = 1f - Mathf.InverseLerp(from, to, t); return 1f - u * u * u; }

        /// <summary>区間内の進行をためてから加速させる</summary>
        /// <param name="t">現在時刻</param>
        /// <param name="from">開始時刻</param>
        /// <param name="to">終了時刻</param>
        /// <returns>0から1の進行</returns>
        private static float EaseIn(float t, float from, float to)
        { float u = Mathf.InverseLerp(from, to, t); return u * u; }

        /// <summary>区間の中央で最大になる山形の重み</summary>
        /// <param name="t">現在時刻</param>
        /// <param name="from">開始時刻</param>
        /// <param name="to">終了時刻</param>
        /// <returns>0から1の重み</returns>
        private static float Bump(float t, float from, float to) => Mathf.Sin(Mathf.PI * Mathf.InverseLerp(from, to, t));

        /// <summary>出口へ戻し通常のカメラ制御を復帰</summary>
        /// <param name="exit">出口の足元位置</param>
        /// <param name="forward">出口の向き</param>
        public void EndHiding(Vector3 exit, Vector3 forward)
        {
            if (!IsHiding) return;
            FinishEntry();
            IsHiding = false;
            mainCamera.transform.localPosition = _savedCameraLocal;
            mainCamera.transform.localRotation = _savedCameraRotation;
            mainCamera.nearClipPlane = _savedNear;
            Warp(exit - transform.TransformVector(Vector3.up * _footLocalY) - transform.position, forward);
            if (characterController != null) characterController.enabled = _controllerEnabled;
            if (_hidingBrain != null) _hidingBrain.enabled = _brainEnabled;
            _hidingCamera = null;
        }
    }
}
