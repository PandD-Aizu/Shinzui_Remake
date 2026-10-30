using System;
using System.Collections;
using System.Collections.Generic;
using LitMotion;
using UnityEngine;

namespace Shinzui.View
{
    /// <summary>実モデルの蝶番、内部視点、出入り位置を管理</summary>
    public sealed class HidingLockerView : MonoBehaviour
    {
        public static event Action<HidingLockerView> Registered, Removed, InteractionRequested;
        public static readonly HashSet<HidingLockerView> Active = new();
        public Transform DoorHinge, CameraAnchor, ExitPoint, StandPoint;
        public float OpenAngle = -105f;
        private MotionHandle _motion;
        private Quaternion _closed;
        private Coroutine _closing;
        private static int _nextId;
        public int Id { get; private set; }
        public bool Occupied { get; set; }
        /// <summary>登録し閉じた回転を保存</summary>
        private void OnEnable()
        { if (Id == 0) Id = ++_nextId; _closed = DoorHinge != null ? DoorHinge.localRotation : Quaternion.identity; Active.Add(this); Registered?.Invoke(this); }
        /// <summary>生成マップの破棄時に登録を解除</summary>
        private void OnDisable()
        { if (_motion.IsActive()) _motion.Cancel(); Active.Remove(this); Removed?.Invoke(this); }
        /// <summary>プレイヤーの操作を通知</summary>
        public void RequestInteraction() => InteractionRequested?.Invoke(this);
        /// <summary>入退出時に開けてから閉じる</summary>
        public void UseDoor()
        {
            if (_closing != null) StopCoroutine(_closing);
            _closing = StartCoroutine(CloseAfterEntry());
        }
        /// <summary>目撃猶予に合わせて閉じる</summary>
        /// <returns>ドアの進行</returns>
        private IEnumerator CloseAfterEntry()
        { SetDoor(true, .15f); yield return new WaitForSeconds(.2f); SetDoor(false, .4f); _closing = null; }
        /// <summary>敵の検査でドアを開ける</summary>
        public void OpenByEnemy()
        { if (_closing != null) StopCoroutine(_closing); _closing = null; SetDoor(true, .25f); }
        /// <summary>蝶番をローカルY軸で回す</summary>
        /// <param name="open">開けるか</param>
        /// <param name="duration">開閉時間</param>
        private void SetDoor(bool open, float duration)
        {
            if (DoorHinge == null) return;
            if (_motion.IsActive()) _motion.Cancel();
            Quaternion start = DoorHinge.localRotation;
            Quaternion end = _closed * Quaternion.Euler(0f, open ? OpenAngle : 0f, 0f);
            _motion = LMotion.Create(0f, 1f, duration).Bind(t => DoorHinge.localRotation = Quaternion.Slerp(start, end, t));
        }
    }
}
