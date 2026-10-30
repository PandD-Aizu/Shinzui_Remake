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
        private Vector3 _handleLocal, _innerGripLocal, _frameGripLocal;
        public int Id { get; private set; }
        public bool Occupied { get; set; }
        /// <summary>蝶番と反対側にある扉の端の向き（ロッカーのローカルX符号）</summary>
        public float FreeSide { get; private set; } = 1f;
        /// <summary>外側から引く扉の端の把手</summary>
        public Vector3 DoorHandle => DoorHinge != null ? DoorHinge.TransformPoint(_handleLocal) : transform.TransformPoint(_handleLocal);
        /// <summary>内側から引き寄せる扉の裏面</summary>
        public Vector3 DoorInnerGrip => DoorHinge != null ? DoorHinge.TransformPoint(_innerGripLocal) : transform.TransformPoint(_innerGripLocal);
        /// <summary>体を引き込むときに掴む開口部の縁</summary>
        public Vector3 FrameGrip => transform.TransformPoint(_frameGripLocal);
        /// <summary>登録し閉じた回転を保存</summary>
        private void OnEnable()
        {
            if (Id == 0) Id = ++_nextId;
            _closed = DoorHinge != null ? DoorHinge.localRotation : Quaternion.identity;
            CacheGripPoints();
            Active.Add(this); Registered?.Invoke(this);
        }

        /// <summary>閉じた扉の実メッシュから把手、裏面、開口部の縁の位置を求める</summary>
        private void CacheGripPoints()
        {
            // 既定値はプレハブの寸法に合わせた開口部
            float hingeX = -.36f, freeX = .36f, front = .41f, back = .37f;
            if (DoorHinge != null)
            {
                hingeX = transform.InverseTransformPoint(DoorHinge.position).x;
                float farthest = 0f;
                front = float.NegativeInfinity; back = float.PositiveInfinity;

                // 扉メッシュの角をロッカー座標へ変換し、蝶番から最も遠い端と厚みを調べる
                foreach (var filter in DoorHinge.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null) continue;
                    Bounds b = filter.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = b.center + Vector3.Scale(b.extents,
                            new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                        Vector3 local = transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                        if (Mathf.Abs(local.x - hingeX) > farthest) { farthest = Mathf.Abs(local.x - hingeX); freeX = local.x; }
                        front = Mathf.Max(front, local.z); back = Mathf.Min(back, local.z);
                    }
                }
                if (float.IsInfinity(front)) { freeX = -hingeX; front = .41f; back = .37f; }
            }
            FreeSide = Mathf.Sign(freeX - hingeX);

            // 外側の把手は端から少し内側、裏面は手首を返さず届く扉の中ほどを掴む
            Vector3 handle = new Vector3(freeX - FreeSide * .06f, 1.05f, front + .025f);
            Vector3 inner = new Vector3(Mathf.Lerp(hingeX, freeX, .55f), 1.15f, back - .025f);
            _handleLocal = DoorHinge != null ? DoorHinge.InverseTransformPoint(transform.TransformPoint(handle)) : handle;
            _innerGripLocal = DoorHinge != null ? DoorHinge.InverseTransformPoint(transform.TransformPoint(inner)) : inner;
            _frameGripLocal = new Vector3(freeX - FreeSide * .02f, 1.3f, back);
        }
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
        /// <summary>手の動きに合わせて扉の開き具合を直接指定する</summary>
        /// <param name="open">閉じた状態を0、全開を1とした割合</param>
        public void DriveDoor(float open)
        {
            if (DoorHinge == null) return;
            if (_closing != null) StopCoroutine(_closing);
            _closing = null;
            if (_motion.IsActive()) _motion.Cancel();
            DoorHinge.localRotation = _closed * Quaternion.Euler(0f, OpenAngle * Mathf.Clamp01(open), 0f);
        }
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
