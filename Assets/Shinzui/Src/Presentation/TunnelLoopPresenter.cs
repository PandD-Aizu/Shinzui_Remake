using System;
using Shinzui.View;
using UnityEngine;
using VContainer.Unity;

namespace Shinzui.Presentation
{
    /// <summary>
    /// 毎フレームプレイヤーのカメラNear平面とゲート開口部を判定し、
    /// ポータル平面への到達を検出した瞬間にシームレスに逆側のゲートへワープさせるPresenter
    /// </summary>
    public class TunnelLoopPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly PlayerView _playerView;

        // 連続ワープ防止のためのクールダウン時間（秒）
        private const float WarpCooldown = 0.15f;
        private float _lastWarpTime;

        // 前フレームのカメラ位置およびニアプレーン四隅・中央
        private Vector3 _prevCameraPosition;
        private Vector3 _prevCameraNearPosition;
        private readonly Vector3[] _prevCorners = new Vector3[4];
        private readonly Vector3[] _currentCorners = new Vector3[4];
        private readonly Vector3[] _frustumCornersScratch = new Vector3[4];
        private bool _isInitialized;

        public TunnelLoopPresenter(PlayerView playerView)
        {
            _playerView = playerView;
            Debug.Log("[TunnelLoopPresenter] Constructor called");
        }

        public void Initialize()
        {
            Debug.Log("[TunnelLoopPresenter] Initialize called");
            if (_playerView != null)
            {
                UpdateCameraSamplePoints(out _prevCameraPosition, out _prevCameraNearPosition, _prevCorners);
                _isInitialized = true;
            }
        }

        public void Tick()
        {
            if (_playerView == null)
            {
                return;
            }

            if (!_isInitialized)
            {
                Initialize();
                return;
            }

            // 物理トランスフォームの同期を強制し、コライダーの bounds を最新位置に更新する
            Physics.SyncTransforms();

            // 現フレームのカメラ位置、ニア面中央、ニア面4隅を取得
            UpdateCameraSamplePoints(out Vector3 currCamPos, out Vector3 currNearPos, _currentCorners);

            // 前フレームから現フレームへの移動範囲を含む包括 Bounds を作成（高速移動時のすり抜け防止）
            Bounds movementBounds = new Bounds(currCamPos, Vector3.zero);
            movementBounds.Encapsulate(_prevCameraPosition);
            movementBounds.Encapsulate(currNearPos);
            movementBounds.Encapsulate(_prevCameraNearPosition);
            for (int k = 0; k < 4; k++)
            {
                movementBounds.Encapsulate(_currentCorners[k]);
                movementBounds.Encapsulate(_prevCorners[k]);
            }

            // アクティブなすべてのトンネルゲートを巡回して判定
            for (int i = 0; i < TunnelGateView.ActiveGates.Count; i++)
            {
                var gate = TunnelGateView.ActiveGates[i];
                if (gate == null || gate.Collider == null || gate.TargetGate == null)
                {
                    continue;
                }

                // 移動範囲がゲートのコライダーの範囲と交差しているか判定
                if (movementBounds.Intersects(gate.Collider.bounds))
                {
                    if (EvaluateAndWarp(gate, currCamPos, currNearPos, _currentCorners, out Vector3 appliedOffset))
                    {
                        // ワープした場合は全サンプル点にoffsetを反映し、次フレームのprev位置として一貫性を保つ
                        currCamPos += appliedOffset;
                        currNearPos += appliedOffset;
                        for (int k = 0; k < 4; k++)
                        {
                            _currentCorners[k] += appliedOffset;
                        }
                        break; // 同一フレームでのワープ処理は1回のみ
                    }
                }
            }

            // 次のフレームのために位置を保存
            _prevCameraPosition = currCamPos;
            _prevCameraNearPosition = currNearPos;
            for (int k = 0; k < 4; k++)
            {
                _prevCorners[k] = _currentCorners[k];
            }
        }

        private void UpdateCameraSamplePoints(out Vector3 camPos, out Vector3 nearCenter, Vector3[] corners)
        {
            camPos = _playerView.CameraPosition;
            nearCenter = _playerView.CameraNearPosition;
            Camera mainCam = _playerView.MainCamera;

            if (mainCam != null)
            {
                mainCam.CalculateFrustumCorners(
                    new Rect(0, 0, 1, 1),
                    mainCam.nearClipPlane,
                    Camera.MonoOrStereoscopicEye.Mono,
                    _frustumCornersScratch);

                for (int k = 0; k < 4; k++)
                {
                    corners[k] = mainCam.transform.TransformPoint(_frustumCornersScratch[k]);
                }
            }
            else
            {
                for (int k = 0; k < 4; k++)
                {
                    corners[k] = nearCenter;
                }
            }
        }

        private bool EvaluateAndWarp(
            TunnelGateView gate,
            Vector3 currCamPos,
            Vector3 currNearPos,
            Vector3[] currCorners,
            out Vector3 appliedOffset)
        {
            appliedOffset = Vector3.zero;

            if (Time.time - _lastWarpTime < WarpCooldown)
            {
                return false;
            }

            var targetGate = gate.TargetGate;
            Vector3 gateCenter = gate.transform.position;
            Vector3 localBoxCenter = Vector3.zero;
            Vector3 localBoxSize = Vector3.one * 5.0f;
            bool hasBoxCollider = false;

            if (gate.Collider is BoxCollider box)
            {
                gateCenter = gate.transform.TransformPoint(box.center);
                localBoxCenter = box.center;
                localBoxSize = box.size;
                hasBoxCollider = true;
            }

            Vector3 gateForward = gate.transform.forward;

            // カメラの移動ベクトル
            Vector3 camMoveDelta = currCamPos - _prevCameraPosition;

            // サンプル点群（ニア面4隅 + ニア面中央 + カメラ位置）
            Span<Vector3> prevPoints = stackalloc Vector3[6];
            Span<Vector3> currPoints = stackalloc Vector3[6];

            for (int k = 0; k < 4; k++)
            {
                prevPoints[k] = _prevCorners[k];
                currPoints[k] = currCorners[k];
            }
            prevPoints[4] = _prevCameraNearPosition;
            currPoints[4] = currNearPos;
            prevPoints[5] = _prevCameraPosition;
            currPoints[5] = currCamPos;

            bool shouldWarp = false;

            // スウィープ線分ごとにポータル平面との交差および開口部内判定を検証
            for (int i = 0; i < 6; i++)
            {
                Vector3 pPrev = prevPoints[i];
                Vector3 pCurr = currPoints[i];

                float dPrev = Vector3.Dot(pPrev - gateCenter, gateForward);
                float dCurr = Vector3.Dot(pCurr - gateCenter, gateForward);

                // 平面を跨いだか（符号反転、またはニア面が平面手前閾値に到達）
                bool crossed = (dPrev < 0.0f && dCurr >= 0.0f) || (dPrev > 0.0f && dCurr <= 0.0f);

                // 保守的閾値: ニア面がポータル平面に極めて接近した場合（斜め進入でのクリップ防止）
                bool nearTouch = Mathf.Abs(dCurr) <= 0.03f && Mathf.Abs(dPrev) > Mathf.Abs(dCurr);

                if (crossed || nearTouch)
                {
                    // 平面との交点を計算
                    Vector3 intersectPoint;
                    float denom = dPrev - dCurr;
                    if (Mathf.Abs(denom) > 1e-5f)
                    {
                        float t = Mathf.Clamp01(dPrev / denom);
                        intersectPoint = Vector3.Lerp(pPrev, pCurr, t);
                    }
                    else
                    {
                        intersectPoint = pCurr;
                    }

                    // ゲート開口部（BoxColliderローカル範囲）内にあるかチェック
                    if (IsWithinGateOpening(gate, intersectPoint, localBoxCenter, localBoxSize, hasBoxCollider))
                    {
                        // 逆方向移動（ポータルから離れる向き）の誤検出を防止
                        if (camMoveDelta.sqrMagnitude > 1e-6f)
                        {
                            float camDistPrev = Vector3.Dot(_prevCameraPosition - gateCenter, gateForward);
                            float camDistCurr = Vector3.Dot(currCamPos - gateCenter, gateForward);
                            if (Mathf.Abs(camDistCurr) <= Mathf.Abs(camDistPrev) || (camDistPrev * camDistCurr <= 0.0f))
                            {
                                shouldWarp = true;
                                break;
                            }
                        }
                        else
                        {
                            shouldWarp = true;
                            break;
                        }
                    }
                }
            }

            if (shouldWarp)
            {
                // 基本のワープ移動量
                Vector3 offset = targetGate.transform.position - gate.transform.position;

                // 境界線上でのチャタリングを防ぐため、進行方向に極小の押し出しを追加
                Vector3 moveDir = camMoveDelta.normalized;
                if (moveDir.sqrMagnitude < 0.001f)
                {
                    moveDir = _playerView.CameraForward;
                }
                offset += moveDir * 0.02f;

                // プレイヤーのワープ実行
                _playerView.Warp(offset);

                appliedOffset = offset;
                _lastWarpTime = Time.time;
                return true;
            }

            return false;
        }

        private static bool IsWithinGateOpening(
            TunnelGateView gate,
            Vector3 worldPoint,
            Vector3 localBoxCenter,
            Vector3 localBoxSize,
            bool hasBoxCollider)
        {
            if (!hasBoxCollider)
            {
                return true;
            }

            Vector3 localPoint = gate.transform.InverseTransformPoint(worldPoint);
            Vector3 delta = localPoint - localBoxCenter;

            // 開口部（XY平面）の判定。エッジ部分の誤差や斜め進入を考慮してマージンを持たせる
            float marginX = 0.3f;
            float marginY = 0.3f;
            float halfWidth = (localBoxSize.x * 0.5f) + marginX;
            float halfHeight = (localBoxSize.y * 0.5f) + marginY;

            return Mathf.Abs(delta.x) <= halfWidth && Mathf.Abs(delta.y) <= halfHeight;
        }

        public void Dispose()
        {
            // リソース解放処理
        }
    }
}