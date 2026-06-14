using System;
using Shinzui.View;
using UnityEngine;
using VContainer.Unity;

namespace Shinzui.Presentation
{
    /// <summary>
    /// 毎フレームプレイヤーのコライダーとゲートコライダーの交差を判定し、
    /// 平面跨ぎを検出した瞬間にシームレスに逆側のゲートへワープさせるPresenter
    /// </summary>
    public class TunnelLoopPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly PlayerView _playerView;
        
        // 連続ワープ防止のためのクールダウン時間（秒）
        private const float WarpCooldown = 0.15f;
        private float _lastWarpTime;
        private Vector3 _prevCameraNearPosition;

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
                _prevCameraNearPosition = _playerView.CameraNearPosition;
            }
        }

        public void Tick()
        {
            if (_playerView == null)
            {
                return;
            }

            // 物理トランスフォームの同期を強制し、コライダーの bounds を最新位置に更新する
            Physics.SyncTransforms();

            // プレイヤーの現在カメラニア面位置と前フレームのニア面位置から、このフレームのニア面位置の移動範囲を作成
            // （高速移動時や低フレームレート時のすり抜けを防止するため）
            Vector3 currNearPos = _playerView.CameraNearPosition;
            Bounds cameraMovementBounds = new Bounds(currNearPos, Vector3.zero);
            cameraMovementBounds.Encapsulate(_prevCameraNearPosition);

            // アクティブなすべてのトンネルゲートを巡回して判定
            for (int i = 0; i < TunnelGateView.ActiveGates.Count; i++)
            {
                var gate = TunnelGateView.ActiveGates[i];
                if (gate == null || gate.Collider == null || gate.TargetGate == null)
                {
                    continue;
                }

                // カメラニア面位置の移動範囲がゲートのコライダーの範囲と交差しているか判定
                if (cameraMovementBounds.Intersects(gate.Collider.bounds))
                {
                    EvaluateAndWarp(gate);
                    break; // 同一フレームでのワープ処理は1回のみにするためループを抜ける
                }
            }

            // 次のフレームのために位置を保存
            _prevCameraNearPosition = currNearPos;
        }

        private void EvaluateAndWarp(TunnelGateView gate)
        {
            if (Time.time - _lastWarpTime < WarpCooldown)
            {
                return;
            }

            var targetGate = gate.TargetGate;
            Vector3 gateCenter = gate.transform.position;
            if (gate.Collider is BoxCollider box)
            {
                gateCenter = gate.transform.TransformPoint(box.center);
            }

            Vector3 gateForward = gate.transform.forward;

            // プレイヤーの現在カメラニア面位置と前フレームのニア面位置
            Vector3 currPos = _playerView.CameraNearPosition;
            Vector3 prevPos = _prevCameraNearPosition;

            // ゲート平面に対する前フレームと現フレームの符号付き距離
            float dPrev = Vector3.Dot(prevPos - gateCenter, gateForward);
            float dCurr = Vector3.Dot(currPos - gateCenter, gateForward);

            // 符号の反転を検出
            bool crossed = (dPrev < 0.0f && dCurr >= 0.0f) || (dPrev > 0.0f && dCurr <= 0.0f);

            if (crossed)
            {
                // 基本のワープ移動量
                Vector3 offset = targetGate.transform.position - gate.transform.position;

                // 境界線上でのチャタリングを防ぐため、進行方向に極小の押し出しを追加
                Vector3 moveDir = (currPos - prevPos).normalized;
                if (moveDir.sqrMagnitude > 0.001f)
                {
                    offset += moveDir * 0.02f;
                }

                // プレイヤーのワープ実行
                _playerView.Warp(offset);

                // 過去位置も同じオフセットで同期
                _prevCameraNearPosition += offset;

                _lastWarpTime = Time.time;
            }
        }

        public void Dispose()
        {
            // リソース解放処理
        }
    }
}